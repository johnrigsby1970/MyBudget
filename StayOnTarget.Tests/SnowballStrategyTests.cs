using Microsoft.VisualStudio.TestTools.UnitTesting;
using StayOnTarget.Models;
using StayOnTarget.Services.Projections;
using StayOnTarget.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StayOnTarget.Tests;

[TestClass]
public class SnowballStrategyTests
{
    private ProjectionEngine _engine = new ProjectionEngine();

    [TestMethod]
    public void TestSnowball_DefaultProjectionsMatchWhenDisabled()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 5000m, Type = AccountType.Checking, IsPrimary = true, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var creditCard = new Account { Id = 2, Name = "CreditCard", Balance = -1000m, Type = AccountType.CreditCard, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var accounts = new List<Account> { checking, creditCard };

        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 2000m, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 8, 1) }
        };

        var allTransactions = new List<Transaction>();
        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 20);

        // Act
        // Run with Snowball disabled
        var optionsDisabled = new SnowballStrategyOptions { EnableSnowball = false };
        var resultsDisabled = _engine.CalculateProjections(
            allTransactions, new(), new(), allTransactions, startDate, endDate, accounts, paychecks, new(), new(), new(), new(), allTransactions, null, false, false, true, optionsDisabled
        ).ToList();

        // Run with default auto-sweep only (null options)
        var resultsDefault = _engine.CalculateProjections(
            allTransactions, new(), new(), allTransactions, startDate, endDate, accounts, paychecks, new(), new(), new(), new(), allTransactions, null, false, false, true, null
        ).ToList();

        // Assert
        Assert.AreEqual(resultsDefault.Count, resultsDisabled.Count);
        for (int i = 0; i < resultsDefault.Count; i++)
        {
            Assert.AreEqual(resultsDefault[i].Amount, resultsDisabled[i].Amount);
            Assert.AreEqual(resultsDefault[i].Balance, resultsDisabled[i].Balance);
            Assert.AreEqual(resultsDefault[i].Description, resultsDisabled[i].Description);
        }
    }

    [TestMethod]
    public void TestSnowball_DebtOrdering_LowestBalanceFirst()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 10000m, Type = AccountType.Checking, IsPrimary = true, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var smallDebt = new Account { Id = 2, Name = "SmallDebt", Balance = -1000m, Type = AccountType.PersonalLoan, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var largeDebt = new Account { Id = 3, Name = "LargeDebt", Balance = -5000m, Type = AccountType.PersonalLoan, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var accounts = new List<Account> { checking, smallDebt, largeDebt };

        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 0m, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 8, 15) }
        };

        var options = new SnowballStrategyOptions
        {
            EnableSnowball = true,
            PrimaryTarget = SurplusAllocationTarget.PayDownDebt,
            DebtSortStrategy = SnowballSortStrategy.LowestBalanceFirst,
            SurplusSweepPercentage = 1.0m,
            CheckingSafetyThresholdPct = 0m
        };

        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 20);

        // Act
        var results = _engine.CalculateProjections(
            new(), new(), new(), new(), startDate, endDate, accounts, paychecks, new(), new(), new(), new(), new(), null, false, false, true, options, startDate
        ).ToList();

        // Assert
        // Boundary is Aug 14. Should sweep SmallDebt first.
        var sweepDate = new DateTime(2026, 8, 14);
        var smallSweep = results.FirstOrDefault(r => r.Description.Contains("SmallDebt"));
        var largeSweep = results.FirstOrDefault(r => r.Description.Contains("LargeDebt"));

        Assert.IsNotNull(smallSweep, $"Small debt should be swept. Found: {string.Join(", ", results.Select(r => r.Description))}");
        Assert.IsNotNull(largeSweep, "Large debt should be swept");

        // Verify ordering: smallSweep should be before largeSweep in the list
        int smallIndex = results.IndexOf(smallSweep);
        int largeIndex = results.IndexOf(largeSweep);
        Assert.IsTrue(smallIndex < largeIndex, "Small debt should be processed before large debt (Snowball)");
    }

    [TestMethod]
    public void TestSnowball_DebtOrdering_HighestInterestFirst_Avalanche()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 10000m, Type = AccountType.Checking, IsPrimary = true, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        
        // Debt with small balance but low interest
        var smallDebtLowInterest = new Account { 
            Id = 2, 
            Name = "SmallDebtLowInterest", 
            Balance = -1000m, 
            Type = AccountType.PersonalLoan, 
            IncludeInTotal = true, 
            BalanceAsOf = new DateTime(2026, 8, 1),
            AccountAprHistory = new List<AccountAprHistory> { new AccountAprHistory { AnnualPercentageRate = 0.05m, AsOfDate = new DateTime(2026, 1, 1) } }
        };
        
        // Debt with large balance but high interest
        var largeDebtHighInterest = new Account { 
            Id = 3, 
            Name = "LargeDebtHighInterest", 
            Balance = -5000m, 
            Type = AccountType.PersonalLoan, 
            IncludeInTotal = true, 
            BalanceAsOf = new DateTime(2026, 8, 1),
            AccountAprHistory = new List<AccountAprHistory> { new AccountAprHistory { AnnualPercentageRate = 0.25m, AsOfDate = new DateTime(2026, 1, 1) } }
        };
        
        var accounts = new List<Account> { checking, smallDebtLowInterest, largeDebtHighInterest };

        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 0m, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 8, 15) }
        };

        var options = new SnowballStrategyOptions
        {
            EnableSnowball = true,
            PrimaryTarget = SurplusAllocationTarget.PayDownDebt,
            DebtSortStrategy = SnowballSortStrategy.HighestInterestFirst,
            SurplusSweepPercentage = 1.0m,
            CheckingSafetyThresholdPct = 0m
        };

        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 20);

        // Act
        var results = _engine.CalculateProjections(
            new(), new(), new(), new(), startDate, endDate, accounts, paychecks, new(), new(), new(), new(), new(), null, false, false, true, options, startDate
        ).ToList();

        // Assert
        var lowIntSweep = results.FirstOrDefault(r => r.Description.Contains("SmallDebtLowInterest"));
        var highIntSweep = results.FirstOrDefault(r => r.Description.Contains("LargeDebtHighInterest"));

        Assert.IsNotNull(lowIntSweep, "Low interest debt should be swept");
        Assert.IsNotNull(highIntSweep, "High interest debt should be swept");

        // Verify ordering: high interest debt should be before low interest debt (Avalanche)
        int highIndex = results.IndexOf(highIntSweep);
        int lowIndex = results.IndexOf(lowIntSweep);
        Assert.IsTrue(highIndex < lowIndex, "High interest debt should be processed before low interest debt (Avalanche)");
    }

    [TestMethod]
    public void TestSnowball_PartialPayments_MultipleDebts()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 1500m, Type = AccountType.Checking, IsPrimary = true, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var debt1 = new Account { Id = 2, Name = "Debt1", Balance = -1000m, Type = AccountType.PersonalLoan, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var debt2 = new Account { Id = 3, Name = "Debt2", Balance = -1000m, Type = AccountType.PersonalLoan, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var accounts = new List<Account> { checking, debt1, debt2 };

        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Next Month Payday", AccountId = 1, ExpectedAmount = 0m, Frequency = Frequency.Monthly, StartDate = new DateTime(2026, 9, 1) }
        };

        var options = new SnowballStrategyOptions
        {
            EnableSnowball = true,
            PrimaryTarget = SurplusAllocationTarget.PayDownDebt,
            DebtSortStrategy = SnowballSortStrategy.LowestBalanceFirst,
            SurplusSweepPercentage = 1.0m,
            CheckingSafetyThresholdPct = 0.333333m // 1500 * 0.3333 = 500. Available surplus = 1000.
        };

        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 31);

        // Act
        var results = _engine.CalculateProjections(
            new(), new(), new(), new(), startDate, endDate, accounts, paychecks, new(), new(), new(), new(), new(), null, false, false, true, options, startDate
        ).ToList();

        // Assert
        var sweep1 = results.FirstOrDefault(r => r.Description.Contains("Snowball: Debt1"));
        var sweep2 = results.FirstOrDefault(r => r.Description.Contains("Snowball: Debt2"));

        Assert.IsNotNull(sweep1, "Debt1 should be paid off");
        Assert.AreEqual(-1000m, sweep1.Amount, "Debt1 should be paid in full (1000)");
        Assert.IsNull(sweep2, "Debt2 should NOT have a sweep entry because pool was exhausted");
    }

    [TestMethod]
    public void TestSnowball_Avalanche_HighestInterestFirst_Mortgage()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 10000m, Type = AccountType.Checking, IsPrimary = true, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        
        // Mortgage with high balance but medium interest
        var mortgage = new Account { 
            Id = 2, 
            Name = "Mortgage", 
            Balance = -200000m, 
            Type = AccountType.Mortgage, 
            IncludeInTotal = true, 
            BalanceAsOf = new DateTime(2026, 8, 1),
            MortgageDetails = new MortgageDetails { InterestRate = 0.04m }
        };
        
        // Personal loan with small balance but higher interest
        var personalLoan = new Account { 
            Id = 3, 
            Name = "PersonalLoan", 
            Balance = -5000m, 
            Type = AccountType.PersonalLoan, 
            IncludeInTotal = true, 
            BalanceAsOf = new DateTime(2026, 8, 1),
            AccountAprHistory = new List<AccountAprHistory> { new AccountAprHistory { AnnualPercentageRate = 0.08m, AsOfDate = new DateTime(2026, 1, 1) } }
        };
        
        var accounts = new List<Account> { checking, mortgage, personalLoan };

        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 0m, Frequency = Frequency.Monthly, StartDate = new DateTime(2026, 9, 1) }
        };

        var options = new SnowballStrategyOptions
        {
            EnableSnowball = true,
            PrimaryTarget = SurplusAllocationTarget.PayDownDebt,
            DebtSortStrategy = SnowballSortStrategy.HighestInterestFirst,
            SurplusSweepPercentage = 1.0m,
            CheckingSafetyThresholdPct = 0m
        };

        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 31);

        // Act
        var results = _engine.CalculateProjections(
            new(), new(), new(), new(), startDate, endDate, accounts, paychecks, new(), new(), new(), new(), new(), null, false, false, true, options, startDate
        ).ToList();

        // Assert
        var sweeps = results.Where(r => r.Description.Contains("Snowball")).ToList();
        var mortgageSweep = sweeps.FirstOrDefault(r => r.Description.Contains("Mortgage"));
        var personalSweep = sweeps.FirstOrDefault(r => r.Description.Contains("PersonalLoan"));

        Assert.IsNotNull(mortgageSweep, "Mortgage should be swept");
        Assert.IsNotNull(personalSweep, "Personal loan should be swept");

        // Verify ordering: personal loan (8%) should be before mortgage (4%)
        int personalIndex = sweeps.IndexOf(personalSweep);
        int mortgageIndex = sweeps.IndexOf(mortgageSweep);
        Assert.IsTrue(personalIndex < mortgageIndex, $"Personal loan (8%, index {personalIndex}) should be processed before mortgage (4%, index {mortgageIndex}) in Avalanche strategy");
    }

    [TestMethod]
    public void TestSnowball_RothLimitCaps_AcrossYearBoundaries()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 10000m, Type = AccountType.Checking, IsPrimary = true, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 12, 1) };
        var roth = new Account { Id = 2, Name = "My Roth IRA", Balance = 0m, Type = AccountType.Brokerage, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 12, 1) };
        var brokerage = new Account { Id = 3, Name = "Taxable Brokerage", Balance = 0m, Type = AccountType.Brokerage, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 12, 1) };
        var accounts = new List<Account> { checking, roth, brokerage };

        // Ensure we have income so the checking balance doesn't run out
        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 10000m, Frequency = Frequency.Monthly, StartDate = new DateTime(2027, 1, 1) }
        };

        var options = new SnowballStrategyOptions
        {
            EnableSnowball = true,
            PrimaryTarget = SurplusAllocationTarget.InvestSurplus,
            InvestmentStrategy = InvestmentStrategy.PrioritizeRothLimits,
            AnnualRothIraContributionLimit = 7000m,
            SurplusSweepPercentage = 1.0m,
            CheckingSafetyThresholdPct = 0.25m // Keep 25% in checking
        };

        var startDate = new DateTime(2026, 12, 1);
        var endDate = new DateTime(2027, 2, 15);

        // Act
        var results = _engine.CalculateProjections(
            new(), new(), new(), new(), startDate, endDate, accounts, paychecks, new(), new(), new(), new(), new(), null, false, false, true, options, startDate
        ).ToList();

        // Assert
        // First boundary: Dec 31, 2026. 
        // Checking: 10000. Threshold: 2500. Available: 7500. Sweep: 7500. 
        // Roth gets 7000. Limit 7000. 
        var sweep2026Roth = results.Where(r => r.Description == "Invest (Roth): My Roth IRA" && r.TransactionDate.Year == 2026).ToList();
        Assert.AreEqual(7000m, sweep2026Roth.Sum(s => -s.Amount), "Should contribute exactly the limit for 2026");

        // Second boundary: Jan 31, 2027. 
        var sweep2027Roth = results.Where(r => r.Description == "Invest (Roth): My Roth IRA" && r.TransactionDate.Year == 2027).ToList();
        Assert.AreEqual(7000m, sweep2027Roth.Sum(s => -s.Amount), "Should contribute exactly the limit to Roth for 2027");

        var sweep2027Brokerage = results.Where(r => r.Description == "Invest: Taxable Brokerage" && r.TransactionDate.Year == 2027).ToList();
        Assert.IsTrue(sweep2027Brokerage.Sum(s => -s.Amount) > 0, "Should sweep remainder to general investment for 2027");
    }
}
