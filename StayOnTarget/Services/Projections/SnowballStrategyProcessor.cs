using System;
using System.Collections.Generic;
using System.Linq;
using StayOnTarget.Models;
using StayOnTarget.ViewModels;

namespace StayOnTarget.Services.Projections;

public static class SnowballStrategyProcessor
{
    public static void ProcessSurplus(
        DateTime sweepDate,
        SnowballStrategyOptions options,
        List<Account> accounts,
        Dictionary<int, decimal> accountBalances,
        Dictionary<int, string> accountNames,
        int primaryCheckingId,
        ref decimal runningBalance,
        HashSet<int> includedTotalAccounts,
        Dictionary<int, decimal> rothContributionsByYear,
        List<ProjectionItem> projectionList)
    {
        if (!options.EnableSnowball) return;

        decimal checkingBalance = accountBalances[primaryCheckingId];
        decimal threshold = options.CheckingSafetyThresholdPct * checkingBalance;
        if (threshold < 0) threshold = 0;

        decimal availableSurplus = checkingBalance - threshold;
        if (availableSurplus <= 0.01m) return;

        decimal sweepPool = availableSurplus * options.SurplusSweepPercentage;
        if (sweepPool <= 0.01m) return;

        // 1. Debt Snowball Execution
        if (options.PrimaryTarget is SurplusAllocationTarget.PayDownDebt or SurplusAllocationTarget.Hybrid)
        {
            var debtAccounts = accounts.Where(a => a.Type is AccountType.CreditCard or AccountType.PersonalLoan or AccountType.Mortgage or AccountType.Auto)
                                       .Where(a => accountBalances[a.Id] < -0.01m)
                                       .ToList();

            if (options.DebtSortStrategy == SnowballSortStrategy.LowestBalanceFirst)
            {
                // Closest to $0 balance means highest balance value since they are negative
                debtAccounts = debtAccounts.OrderByDescending(a => accountBalances[a.Id]).ToList();
            }
            else // HighestInterestFirst (Avalanche)
            {
                debtAccounts = debtAccounts.OrderByDescending(a => {
                    decimal rate = 0;
                    if (a.Type == AccountType.Mortgage && a.MortgageDetails != null) 
                        rate = a.MortgageDetails.InterestRate;
                    else if (a.AccountAprHistory != null && a.AccountAprHistory.Any())
                        rate = a.AccountAprHistory.OrderByDescending(h => h.AsOfDate).First().AnnualPercentageRate;
                    
                    return rate;
                }).ToList();
            }

            foreach (var debt in debtAccounts)
            {
                if (sweepPool <= 0) break;

                decimal currentDebtBalance = -accountBalances[debt.Id];
                decimal payAmount = Math.Min(sweepPool, currentDebtBalance);

                if (payAmount > 0.01m)
                {
                    accountBalances[primaryCheckingId] -= payAmount;
                    accountBalances[debt.Id] += payAmount;
                    sweepPool -= payAmount;

                    runningBalance = accounts.Where(a => includedTotalAccounts.Contains(a.Id))
                        .Sum(a => accountBalances[a.Id]);

                    projectionList.Add(new ProjectionItem
                    {
                        TransactionDate = sweepDate,
                        Description = $"Snowball: {accountNames[debt.Id]}",
                        FromAccountId = primaryCheckingId,
                        ToAccountId = debt.Id,
                        Amount = -payAmount,
                        Balance = runningBalance,
                        IsSynthetic = true,
                        AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value),
                        InOrOutOfMoneyAccount = true
                    });
                }
            }
        }

        // 2. Investment Allocation Execution
        if (sweepPool > 0.01m && options.PrimaryTarget is SurplusAllocationTarget.InvestSurplus or SurplusAllocationTarget.Hybrid)
        {
            var investmentAccounts = accounts.Where(a => a.Type is AccountType.Brokerage or AccountType.Retirement401k or AccountType.Investment)
                                             .ToList();

            // Roth Prioritization
            if (options.InvestmentStrategy == InvestmentStrategy.PrioritizeRothLimits)
            {
                var rothAccounts = investmentAccounts.Where(a => a.Name.Contains("Roth", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var roth in rothAccounts)
                {
                    if (sweepPool <= 0) break;

                    int year = sweepDate.Year;
                    if (!rothContributionsByYear.ContainsKey(year)) rothContributionsByYear[year] = 0;

                    decimal remainingLimit = options.AnnualRothContributionLimit - rothContributionsByYear[year];
                    if (remainingLimit > 0)
                    {
                        decimal investAmount = Math.Min(sweepPool, remainingLimit);
                        if (investAmount > 0.01m)
                        {
                            accountBalances[primaryCheckingId] -= investAmount;
                            accountBalances[roth.Id] += investAmount;
                            sweepPool -= investAmount;
                            rothContributionsByYear[year] += investAmount;

                            runningBalance = accounts.Where(a => includedTotalAccounts.Contains(a.Id))
                                .Sum(a => accountBalances[a.Id]);

                            projectionList.Add(new ProjectionItem
                            {
                                TransactionDate = sweepDate,
                                Description = $"Invest (Roth): {accountNames[roth.Id]}",
                                FromAccountId = primaryCheckingId,
                                ToAccountId = roth.Id,
                                Amount = -investAmount,
                                Balance = runningBalance,
                                IsSynthetic = true,
                                AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value),
                                InOrOutOfMoneyAccount = true
                            });
                        }
                    }
                }
            }

            // Yield Optimization / Remaining Investment
            if (sweepPool > 0.01m && investmentAccounts.Any())
            {
                var nonRothInvestmentAccounts = investmentAccounts
                    .Where(a => !a.Name.Contains("Roth", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var targetInvestment = (nonRothInvestmentAccounts.Any() ? nonRothInvestmentAccounts : investmentAccounts)
                    .OrderByDescending(a => a.AnnualGrowthRate)
                    .First();

                decimal investAmount = sweepPool;
                accountBalances[primaryCheckingId] -= investAmount;
                accountBalances[targetInvestment.Id] += investAmount;
                
                // IMPORTANT: Consume the pool so it's not reused or reported incorrectly
                sweepPool = 0; 

                runningBalance = accounts.Where(a => includedTotalAccounts.Contains(a.Id))
                    .Sum(a => accountBalances[a.Id]);

                projectionList.Add(new ProjectionItem
                {
                    TransactionDate = sweepDate,
                    Description = $"Invest: {accountNames[targetInvestment.Id]}",
                    FromAccountId = primaryCheckingId,
                    ToAccountId = targetInvestment.Id,
                    Amount = -investAmount,
                    Balance = runningBalance,
                    IsSynthetic = true,
                    AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value),
                    InOrOutOfMoneyAccount = true
                });
            }
        }
    }
}
