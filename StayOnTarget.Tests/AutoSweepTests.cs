using Microsoft.VisualStudio.TestTools.UnitTesting;
using StayOnTarget.Models;
using StayOnTarget.Services.Projections;
using StayOnTarget.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StayOnTarget.Tests;

[TestClass]
public class AutoSweepTests
{
    private ProjectionEngine _engine = new ProjectionEngine();

    [TestMethod]
    public void TestAutoSweep_ClearsCreditCardBalanceAtEndOfPeriod()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 5000m, Type = AccountType.Checking, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1), IsPrimary = true };
        var creditCard = new Account { Id = 2, Name = "CreditCard", Balance = 0m, Type = AccountType.CreditCard, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var accounts = new List<Account> { checking, creditCard };

        // Period 1: August 1 to August 14. Paycheck on August 1 and August 15.
        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 2000m, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 8, 1) }
        };

        var allTransactions = new List<Transaction>();
        foreach (var pay in paychecks) {
            allTransactions.Add(new Transaction { PaycheckId = pay.Id, TransactionDate = pay.StartDate, Amount = pay.ExpectedAmount, Description = "Initial Pay", AccountId = pay.AccountId });
        }

        // Transaction in the middle of Period 1
        var transactions = new List<Transaction>
        {
            new Transaction { TransactionDate = new DateTime(2026, 8, 5), Amount = -1000m, AccountId = 2, Description = "Spending" }
        };
        allTransactions.AddRange(transactions);

        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 8, 20);

        // Act
        var results = _engine.CalculateProjections(
            allTransactions, new(), new(), allTransactions, startDate, endDate, accounts, paychecks, new(), new(), new(), new(), allTransactions, null, false, false, true
        ).ToList();

        // Assert
        var sweepDate = new DateTime(2026, 8, 14);
        var sweepEntry = results.FirstOrDefault(r => r.IsSynthetic && r.Description.Contains("Auto-Sweep: CreditCard") && r.TransactionDate == sweepDate);
        
        Assert.IsNotNull(sweepEntry, "Auto-Sweep entry should exist on August 14");
        Assert.AreEqual(-1000m, sweepEntry.Amount, "Sweep amount should be -1000 (payment of 1000 debt)");
        
        // Verify balances in the sweep entry
        Assert.AreEqual(0m, sweepEntry.AccountBalances["CreditCard"], "CreditCard balance should be 0 after sweep");
        // Initial Checking: 5000 + Paycheck: 2000 - Sweep: 1000 = 6000
        Assert.AreEqual(6000m, sweepEntry.AccountBalances["Checking"], "Checking balance should be reduced by sweep amount");
    }

    [TestMethod]
    public void TestAutoSweep_HandlesMultiplePeriods()
    {
        // Arrange
        var checking = new Account { Id = 1, Name = "Checking", Balance = 5000m, Type = AccountType.Checking, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1), IsPrimary = true };
        var creditCard = new Account { Id = 2, Name = "CreditCard", Balance = 0m, Type = AccountType.CreditCard, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 8, 1) };
        var accounts = new List<Account> { checking, creditCard };

        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 2000m, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 8, 1) }
        };

        var allTransactions = new List<Transaction>();
        foreach (var pay in paychecks) {
            allTransactions.Add(new Transaction { PaycheckId = pay.Id, TransactionDate = pay.StartDate, Amount = pay.ExpectedAmount, Description = "Initial Pay", AccountId = pay.AccountId });
        }

        var transactions = new List<Transaction>
        {
            new Transaction { TransactionDate = new DateTime(2026, 8, 5), Amount = -1000m, AccountId = 2, Description = "Spending P1" },
            new Transaction { TransactionDate = new DateTime(2026, 8, 20), Amount = -500m, AccountId = 2, Description = "Spending P2" }
        };
        allTransactions.AddRange(transactions);

        var startDate = new DateTime(2026, 8, 1);
        var endDate = new DateTime(2026, 9, 1);

        // Act
        var results = _engine.CalculateProjections(
            allTransactions, new(), new(), allTransactions, startDate, endDate, accounts, paychecks, new(), new(), new(), new(), allTransactions, null, false, false, true
        ).ToList();

        // Assert
        var sweep1 = results.FirstOrDefault(r => r.IsSynthetic && r.TransactionDate == new DateTime(2026, 8, 14));
        var sweep2 = results.FirstOrDefault(r => r.IsSynthetic && r.TransactionDate == new DateTime(2026, 8, 28));

        Assert.IsNotNull(sweep1, "First sweep should exist");
        Assert.IsNotNull(sweep2, "Second sweep should exist");
        
        Assert.AreEqual(-1000m, sweep1.Amount);
        Assert.AreEqual(-500m, sweep2.Amount);
        
        Assert.AreEqual(0m, sweep1.AccountBalances["CreditCard"]);
        Assert.AreEqual(0m, sweep2.AccountBalances["CreditCard"]);
    }

    [TestMethod]
    public void TestAutoSweep_DoesNotOccurInThePast()
    {
        // Arrange
        // Current date is 2026-07-28
        var today = new DateTime(2026, 7, 28);
        var checking = new Account { Id = 1, Name = "Checking", Balance = 5000m, Type = AccountType.Checking, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 7, 1), IsPrimary = true };
        var creditCard = new Account { Id = 2, Name = "CreditCard", Balance = 0m, Type = AccountType.CreditCard, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 7, 1) };
        var accounts = new List<Account> { checking, creditCard };

        // Paychecks: one in the past (July 14), one in the future (August 11) relative to current projection start July 1
        // But relative to TODAY (July 28), the July 14 boundary is in the past.
        var paychecks = new List<Paycheck>
        {
            new Paycheck { Id = 1, Name = "Payday", AccountId = 1, ExpectedAmount = 2000m, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 7, 14) }
        };

        var allTransactions = new List<Transaction>();
        // Spending on July 5
        allTransactions.Add(new Transaction { TransactionDate = new DateTime(2026, 7, 5), Amount = -1000m, AccountId = 2, Description = "Past Spending" });

        var startDate = new DateTime(2026, 7, 1);
        var endDate = new DateTime(2026, 8, 15);

        // Act
        var results = _engine.CalculateProjections(
            allTransactions, new(), new(), allTransactions, startDate, endDate, accounts, paychecks, new(), new(), new(), new(), allTransactions, null, false, false, true, null, today
        ).ToList();

        // Assert
        // Boundary is July 13 (one day before July 14 paycheck)
        var pastSweepDate = new DateTime(2026, 7, 13);
        var pastSweep = results.FirstOrDefault(r => r.IsSynthetic && r.TransactionDate == pastSweepDate);
        
        Assert.IsNull(pastSweep, "Auto-Sweep should NOT occur for dates in the past (before July 28)");
        
        // Next boundary is July 27. July 27 is also in the past (relative to July 28)
        var pastSweepDate2 = new DateTime(2026, 7, 27);
        var pastSweep2 = results.FirstOrDefault(r => r.IsSynthetic && r.TransactionDate == pastSweepDate2);
        Assert.IsNull(pastSweep2, "Auto-Sweep should NOT occur on July 27 as it is in the past");

        // Next boundary is August 10. August 10 is in the future.
        var futureSweepDate = new DateTime(2026, 8, 10);
        var futureSweep = results.FirstOrDefault(r => r.IsSynthetic && r.TransactionDate == futureSweepDate);
        Assert.IsNotNull(futureSweep, "Auto-Sweep SHOULD occur for dates in the future (August 10)");
    }
}
