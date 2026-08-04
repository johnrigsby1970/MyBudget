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

        // -------------------------------------------------------------
        // Step 0: Calculate Available Sweep Pool Based on Strategy Options
        // -------------------------------------------------------------
        decimal threshold = options.CheckingSafetyBufferAmount;
        if (options.CheckingSafetyThresholdPct > 0m)
        {
            decimal pctThreshold = options.CheckingSafetyThresholdPct * checkingBalance;
            threshold = Math.Max(threshold, pctThreshold);
        }
        if (threshold < 0) threshold = 0;

        decimal availableSurplus = checkingBalance - threshold;
        if (availableSurplus <= 0.01m) return;

        decimal sweepPool = 0m;

        switch (options.SurplusMethod)
        {
            case SnowballStrategyOptions.SurplusCalculationMethod.FixedMonthlyAmount:
                sweepPool = Math.Min(availableSurplus, options.FixedMonthlySurplusAmount);
                break;

            case SnowballStrategyOptions.SurplusCalculationMethod.Hybrid:
                decimal pctSweep = availableSurplus * options.SurplusSweepPercentage;
                sweepPool = Math.Min(pctSweep, options.FixedMonthlySurplusAmount);
                break;

            case SnowballStrategyOptions.SurplusCalculationMethod.PercentageOfChecking:
            default:
                sweepPool = availableSurplus * options.SurplusSweepPercentage;
                break;
        }

        if (sweepPool <= 0.01m) return;

        // -------------------------------------------------------------
        // 1. Debt Snowball Execution
        // -------------------------------------------------------------
        if (options.PrimaryTarget is SurplusAllocationTarget.PayDownDebt or SurplusAllocationTarget.Hybrid)
        {
            var debtAccounts = accounts.Where(a => a.IsLiability)
                                       .Where(a => !options.ExcludedAccountIds.Contains(a.Id)) // Honor Excluded Accounts
                                       .Where(a => accountBalances[a.Id] < -0.01m)
                                       .ToList();

            if (options.DebtSortStrategy == SnowballSortStrategy.LowestBalanceFirst)
            {
                // Closest to $0 balance first (balances are negative)
                debtAccounts = debtAccounts.OrderByDescending(a => accountBalances[a.Id]).ToList();
            }
            else // HighestInterestFirst (Avalanche)
            {
                debtAccounts = debtAccounts.OrderByDescending(a => {
                    decimal rate = 0;
                    if (a.IsLoanAccount && a.MortgageDetails != null) 
                        rate = a.MortgageDetails.InterestRate;
                    else if (a.AccountAprHistory != null && a.AccountAprHistory.Any())
                        rate = a.AccountAprHistory.OrderByDescending(h => h.AsOfDate).First().AnnualPercentageRate;
                    
                    return rate;
                }).ToList();
            }

            foreach (var debt in debtAccounts)
            {
                if (sweepPool <= 0.01m) break;

                decimal currentDebtBalance = -accountBalances[debt.Id];
                decimal payAmount = Math.Min(sweepPool, currentDebtBalance);

                if (payAmount > 0.01m)
                {
                    accountBalances[primaryCheckingId] -= payAmount;
                    accountBalances[debt.Id] += payAmount;
                    sweepPool -= payAmount;

                    runningBalance = accounts.Where(a => includedTotalAccounts.Contains(a.Id))
                        .Sum(a => accountBalances[a.Id]);

                    projectionList.Add(new ProjectionItem {
                        Type = ProjectionEngine.ProjectionEventType.Snowball,
                        TransactionDate = sweepDate,
                        Description = $"Snowball: {accountNames[debt.Id]}",
                        FromAccountId = primaryCheckingId,
                        ToAccountId = debt.Id,
                        Amount = Math.Abs(payAmount),
                        Balance = runningBalance,
                        IsSynthetic = true,
                        AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value),
                        InOrOutOfMoneyAccount = true
                    });
                }
            }
        }

        // -------------------------------------------------------------
        // 2. Investment Allocation Execution
        // -------------------------------------------------------------
        if (sweepPool > 0.01m && options.PrimaryTarget is SurplusAllocationTarget.InvestSurplus or SurplusAllocationTarget.Hybrid)
        {
            // Expand filter to capture all investment/retirement account types
            var investmentAccounts = accounts.Where(a => a.Type is AccountType.Brokerage 
                                                        or AccountType.Retirement401k 
                                                        or AccountType.Roth401k
                                                        or AccountType.Investment 
                                                        or AccountType.IRA 
                                                        or AccountType.RothIRA)
                                             .Where(a => !options.ExcludedAccountIds.Contains(a.Id))
                                             .ToList();

            // Roth Prioritization
            if (options.InvestmentStrategy == InvestmentStrategy.PrioritizeRothLimits)
            {
                var rothAccounts = investmentAccounts.Where(a => a.Type == AccountType.RothIRA).ToList();
                foreach (var roth in rothAccounts)
                {
                    if (sweepPool <= 0.01m) break;

                    int year = sweepDate.Year;
                    if (!rothContributionsByYear.ContainsKey(year)) rothContributionsByYear[year] = 0;

                    decimal remainingLimit = options.AnnualRothIraContributionLimit - rothContributionsByYear[year];
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

                            projectionList.Add(new ProjectionItem {
                                Type = ProjectionEngine.ProjectionEventType.Roth,
                                TransactionDate = sweepDate,
                                Description = $"Invest (Roth): {accountNames[roth.Id]}",
                                FromAccountId = primaryCheckingId,
                                ToAccountId = roth.Id,
                                Amount = Math.Abs(investAmount),
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
                    .Where(a => a.Type != AccountType.RothIRA && a.Type != AccountType.Roth401k)
                    .ToList();

                var targetInvestment = (nonRothInvestmentAccounts.Any() ? nonRothInvestmentAccounts : investmentAccounts)
                    .OrderByDescending(a => a.AnnualGrowthRate)
                    .First();

                decimal investAmount = sweepPool;
                accountBalances[primaryCheckingId] -= investAmount;
                accountBalances[targetInvestment.Id] += investAmount;
                
                sweepPool = 0; 

                runningBalance = accounts.Where(a => includedTotalAccounts.Contains(a.Id))
                    .Sum(a => accountBalances[a.Id]);

                projectionList.Add(new ProjectionItem {
                    Type = ProjectionEngine.ProjectionEventType.Snowball,
                    TransactionDate = sweepDate,
                    Description = $"Invest: {accountNames[targetInvestment.Id]}",
                    FromAccountId = primaryCheckingId,
                    ToAccountId = targetInvestment.Id,
                    Amount = Math.Abs(investAmount),
                    Balance = runningBalance,
                    IsSynthetic = true,
                    AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value),
                    InOrOutOfMoneyAccount = true
                });
            }
        }
    }
}