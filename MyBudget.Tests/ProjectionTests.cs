using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using MyBudget.Models;
using MyBudget.Services;
using MyBudget.ViewModels;

namespace MyBudget.Tests
{
    [TestClass]
    public class ProjectionTests
    {
        private ProjectionEngine _engine = null!;

        [TestInitialize]
        public void Setup()
        {
            _engine = new ProjectionEngine();
        }

        [TestMethod]
        public void TestPaycheckAssociation_OverridesProjectedPaycheck()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 1, 1) }
            };
            var paychecks = new List<Paycheck>
            {
                new Paycheck { Id = 1, Name = "Salary", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 20), AccountId = 1 }
            };
            
            // Ad-hoc transaction associated with the paycheck on 2026-02-20
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction 
                { 
                    Id = 101, 
                    Description = "Actual Salary", 
                    Amount = 2100, 
                    Date = new DateTime(2026, 2, 20), 
                    PaycheckId = 1, 
                    PaycheckOccurrenceDate = new DateTime(2026, 2, 20),
                    ToAccountId = 1
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, paychecks, new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            // We expect one paycheck entry on 2/20. Since we have an ad-hoc override, the "Pay: Salary" should be missing and replaced by "Actual Salary".
            var salaryEntries = results.Where(r => r.Date == new DateTime(2026, 2, 20)).ToList();
            
            Assert.AreEqual(1, salaryEntries.Count, "Should only have one entry for the paycheck date");
            Assert.AreEqual("Actual Salary", salaryEntries[0].Description);
            Assert.AreEqual(2100, salaryEntries[0].Amount);
        }

        [TestMethod]
        public void TestPaycheckHeuristic_Removed()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 1, 1) }
            };
            var paychecks = new List<Paycheck>
            {
                new Paycheck { Id = 1, Name = "Salary", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 20), AccountId = 1 }
            };
            
            // Ad-hoc transaction NOT associated with the paycheck, but has same date and description-based "Pay: Salary"
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction 
                { 
                    Id = 101, 
                    Description = "Pay: Salary", 
                    Amount = 2100, 
                    Date = new DateTime(2026, 2, 20), 
                    ToAccountId = 1
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, paychecks, new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            // Since the heuristic is removed, we expect BOTH the projected "Pay: Salary" and the ad-hoc "Pay: Salary".
            var salaryEntries = results.Where(r => r.Date == new DateTime(2026, 2, 20)).ToList();
            
            Assert.AreEqual(2, salaryEntries.Count, "Should have two entries for the paycheck date because heuristic was removed");
            Assert.IsTrue(salaryEntries.Any(r => r.Description == "Expected Pay: Salary" && r.Amount == 2000), "Missing projected paycheck");
            Assert.IsTrue(salaryEntries.Any(r => r.Description == "Pay: Salary" && r.Amount == 2100), "Missing ad-hoc transaction");
        }

        [TestMethod]
        public void TestBillAccountRobustness_UsesAccountId()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 1, 1) },
                new Account { Id = 2, Name = "Savings", Balance = 5000, IncludeInTotal = false, BalanceAsOf = new DateTime(2026, 1, 1) }
            };
            var bills = new List<Bill>
            {
                new Bill { Id = 1, Name = "Rent", ExpectedAmount = 500, Frequency = Frequency.Monthly, DueDay = 5, AccountId = 1 }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, new List<Paycheck>(), bills, new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), new List<AdHocTransaction>()).ToList();

            // Assert
            var rentEntry = results.FirstOrDefault(r => r.Description.Contains("Rent"));
            Assert.IsNotNull(rentEntry);
            Assert.AreEqual(-500, rentEntry.Amount);
            // Balance should be 1000 - 500 = 500 (Savings is not included in total)
            Assert.AreEqual(500, rentEntry.Balance);
            Assert.AreEqual(500m, rentEntry.AccountBalances["Checking"]);
            Assert.AreEqual(5000m, rentEntry.AccountBalances["Savings"]);
        }

        [TestMethod]
        public void TestInterestAccrual_MortgageBalanceIncreases()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account 
                { 
                    Id = 1, 
                    Name = "Mortgage", 
                    Balance = 200000, 
                    Type = AccountType.Mortgage, 
                    IncludeInTotal = true, 
                    BalanceAsOf = new DateTime(2026, 2, 1),
                    MortgageDetails = new MortgageDetails
                    {
                        InterestRate = 6.0m, // 0.5% monthly
                        PaymentDate = new DateTime(2026, 2, 1)
                    }
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 10); // Should trigger one interest event

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), new List<AdHocTransaction>()).ToList();

            // Assert
            var interestEntry = results.FirstOrDefault(r => r.Description.Contains("Interest: Mortgage"));
            Assert.IsNotNull(interestEntry, "Should have an interest entry");
            
            // 200,000 * 0.06 / 12 = 1000
            Assert.AreEqual(1000m, interestEntry.Amount);
            Assert.AreEqual(201000m, interestEntry.AccountBalances["Mortgage"]);
            // Debts are subtracted from the total balance
            Assert.AreEqual(-201000m, interestEntry.Balance);
        }

        [TestMethod]
        public void TestDailyGrowth_SavingsBalanceIncreases()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account 
                { 
                    Id = 1, 
                    Name = "Savings", 
                    Balance = 10000, 
                    Type = AccountType.Savings, 
                    IncludeInTotal = true, 
                    BalanceAsOf = new DateTime(2026, 2, 1),
                    AnnualGrowthRate = 3.65m // 0.01% daily
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 2, 11); // 10 days of growth

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), new List<AdHocTransaction>()).ToList();

            // Assert
            // After 10 days, 10,000 * 0.0001 * 10 = 10
            // The ProjectionEngine calculates growth between events or at the end of the projection period.
            // Since there are no events other than the end of the projection, we should see the balance in the last item.
            
            var lastItem = results.Last();
            Assert.AreEqual(10010m, lastItem.AccountBalances["Savings"]);
            Assert.AreEqual(10010m, lastItem.Balance);
        }

        [TestMethod]
        public void TestPeriodNet_CalculatedCorrected()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 2, 1) }
            };
            var paychecks = new List<Paycheck>
            {
                new Paycheck { Id = 1, Name = "Pay1", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 1), AccountId = 1 },
                new Paycheck { Id = 2, Name = "Pay2", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 15), AccountId = 1 }
            };
            var bills = new List<Bill>
            {
                new Bill { Id = 1, Name = "Bill1", ExpectedAmount = 500, Frequency = Frequency.Monthly, DueDay = 5, AccountId = 1 }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, paychecks, bills, new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), new List<AdHocTransaction>()).ToList();

            // Assert
            // Period 1: 2/1 to 2/14. Events: Pay1 (2000), Bill1 (-500). Net = 1500.
            var pay1Entry = results.FirstOrDefault(r => r.Description == "Expected Pay: Pay1");
            Assert.IsNotNull(pay1Entry);
            Assert.AreEqual(1500m, pay1Entry.PeriodNet);

            // Period 2: 2/15 onwards. Events: Pay1 (2000), Pay2 (2000). Total = 4000.
            // Since they are on the same day, Pay: Pay1 should be the first item and have the PeriodNet.
            var pay1SecondOccurrence = results.FirstOrDefault(r => r.Description == "Expected Pay: Pay1" && r.Date == new DateTime(2026, 2, 15));
            Assert.IsNotNull(pay1SecondOccurrence);
            Assert.AreEqual(4000m, pay1SecondOccurrence.PeriodNet);

            var pay2Entry = results.FirstOrDefault(r => r.Description == "Expected Pay: Pay2" && r.Date == new DateTime(2026, 2, 15));
            Assert.IsNotNull(pay2Entry);
            Assert.IsNull(pay2Entry.PeriodNet);
        }

        [TestMethod]
        public void TestAdHocInterest_OverridesProjectedInterest()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account 
                { 
                    Id = 1, 
                    Name = "Mortgage", 
                    Balance = 200000, 
                    Type = AccountType.Mortgage, 
                    IncludeInTotal = true, 
                    BalanceAsOf = new DateTime(2026, 2, 1),
                    MortgageDetails = new MortgageDetails
                    {
                        InterestRate = 6.0m,
                        PaymentDate = new DateTime(2026, 2, 15)
                    }
                }
            };

            // Ad-hoc transaction on the same date as projected interest (2/15)
            // It has ToAccountId = 1, so it should be treated as interest/rebalance
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction
                {
                    Id = 101,
                    Description = "Actual Interest",
                    Amount = 950,
                    Date = new DateTime(2026, 2, 15),
                    ToAccountId = 1,
                    IsRebalance = true
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            // We expect "Actual Interest" to exist and "Interest: Mortgage" to be missing.
            var interestEntries = results.Where(r => r.Date == new DateTime(2026, 2, 15)).ToList();
            
            Assert.AreEqual(1, interestEntries.Count, "Should only have one entry on the interest date");
            Assert.AreEqual("Actual Interest", interestEntries[0].Description);
            Assert.AreEqual(950, interestEntries[0].Amount);
            Assert.AreEqual(200950m, interestEntries[0].AccountBalances["Mortgage"]);
            // Debts are subtracted from total balance
            Assert.AreEqual(-200950m, interestEntries[0].Balance);
        }

        [TestMethod]
        public void TestBucketReduction_AdHocReducesProjectedBucket()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 2, 1) }
            };
            var paychecks = new List<Paycheck>
            {
                new Paycheck { Id = 1, Name = "Pay1", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 1), AccountId = 1 }
            };
            var buckets = new List<BudgetBucket>
            {
                new BudgetBucket { Id = 1, Name = "Groceries", ExpectedAmount = 500, AccountId = 1 }
            };

            // Ad-hoc transaction for this bucket in this period
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction
                {
                    Id = 101,
                    Description = "Store Purchase",
                    Amount = 200,
                    Date = new DateTime(2026, 2, 5),
                    AccountId = 1,
                    BucketId = 1
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 2, 14); // One period

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, paychecks, new List<Bill>(), buckets, new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            // Bucket Groceries should be reduced by 200. Original 500 - 200 = 300.
            var bucketEntry = results.FirstOrDefault(r => r.Description.Contains("Bucket: Groceries"));
            Assert.IsNotNull(bucketEntry, "Should have a bucket entry");
            Assert.AreEqual(-300m, bucketEntry.Amount, "Bucket amount should be reduced by ad-hoc spending");

            // Total balance impact should be:
            // 1000 (starting) + 2000 (paycheck) - 200 (ad-hoc) - 300 (remaining bucket) = 2500
            var lastEntry = results.Last();
            Assert.AreEqual(2500m, lastEntry.Balance);
        }

        [TestMethod]
        public void TestBucketReduction_AdHocExceedsProjectedBucket()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 2, 1) }
            };
            var paychecks = new List<Paycheck>
            {
                new Paycheck { Id = 1, Name = "Pay1", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 1), AccountId = 1 }
            };
            var buckets = new List<BudgetBucket>
            {
                new BudgetBucket { Id = 1, Name = "Groceries", ExpectedAmount = 500, AccountId = 1 }
            };

            // Ad-hoc transaction exceeding this bucket
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction
                {
                    Id = 101,
                    Description = "Big Grocery Run",
                    Amount = 600,
                    Date = new DateTime(2026, 2, 5),
                    AccountId = 1,
                    BucketId = 1
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 2, 14);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, paychecks, new List<Bill>(), buckets, new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            // Bucket Groceries should be reduced to 0 because spending (600) >= projected (500).
            var bucketEntry = results.FirstOrDefault(r => r.Description.Contains("Bucket: Groceries"));
            Assert.IsNotNull(bucketEntry, "Should have a bucket entry");
            Assert.AreEqual(0m, bucketEntry.Amount, "Bucket amount should be reduced to 0 when spending exceeds budget");

            // Total balance impact should be:
            // 1000 (starting) + 2000 (paycheck) - 600 (ad-hoc) - 0 (remaining bucket) = 2400
            var lastEntry = results.Last();
            Assert.AreEqual(2400m, lastEntry.Balance);
        }
        [TestMethod]
        public void TestBucketReduction_UserScenario()
        {
            // Arrange
            // Bucket "Grayson" with $50 allotted.
            // Paychecks on 2/19/2026 and 3/5/2026.
            // Ad-hoc transaction for $500 on 2/20/2026 associated with "Grayson" bucket.
            
            var accounts = new List<Account>
            {
                new Account { Id = 1, Name = "Checking", Balance = 1000, IncludeInTotal = true, BalanceAsOf = new DateTime(2026, 1, 1) }
            };
            var paychecks = new List<Paycheck>
            {
                new Paycheck { Id = 1, Name = "Pay1", ExpectedAmount = 2000, Frequency = Frequency.BiWeekly, StartDate = new DateTime(2026, 2, 19), AccountId = 1 }
            };
            var buckets = new List<BudgetBucket>
            {
                new BudgetBucket { Id = 1, Name = "Grayson", ExpectedAmount = 50, AccountId = 1 }
            };
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction { Id = 101, Description = "Grayson Ad-Hoc", Amount = 500, Date = new DateTime(2026, 2, 20), BucketId = 1, AccountId = 1 }
            };

            var startDate = new DateTime(2026, 2, 19);
            var endDate = new DateTime(2026, 3, 10);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, paychecks, new List<Bill>(), buckets, new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            // Paycheck on 2/19. Next on 3/5.
            // Bucket Grayson on 2/19 should be reduced by ad-hoc on 2/20 (same period).
            // Since 500 > 50, Grayson bucket on 2/19 should be 0.
            
            var graysonBucketEntry = results.FirstOrDefault(r => r.Description.Contains("Bucket: Grayson") && r.Date == new DateTime(2026, 2, 19));
            Assert.IsNotNull(graysonBucketEntry, "Grayson bucket entry should exist");
            Assert.AreEqual(0m, graysonBucketEntry.Amount, "Grayson bucket amount should be reduced to 0 because ad-hoc exceeds it");
        }

        [TestMethod]
        public void TestAdHocMortgagePayment_ReducesDebt()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account 
                { 
                    Id = 1, 
                    Name = "Mortgage", 
                    Balance = 200000, 
                    Type = AccountType.Mortgage, 
                    IncludeInTotal = true, 
                    BalanceAsOf = new DateTime(2026, 2, 1),
                    MortgageDetails = new MortgageDetails
                    {
                        InterestRate = 0, // No interest for this test to keep it simple
                        Escrow = 400,
                        MortgageInsurance = 100
                    }
                },
                new Account
                {
                    Id = 2,
                    Name = "Checking",
                    Balance = 10000,
                    Type = AccountType.Checking,
                    IncludeInTotal = true,
                    BalanceAsOf = new DateTime(2026, 2, 1)
                }
            };

            // Ad-hoc transaction: Payment of 1500 to the mortgage account.
            // Expected principal reduction = 1500 - 400 (escrow) - 100 (insurance) = 1000.
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction
                {
                    Id = 101,
                    Description = "Mortgage Payment",
                    Amount = 1500,
                    Date = new DateTime(2026, 2, 15),
                    AccountId = 2, // From Checking
                    ToAccountId = 1 // To Mortgage
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            var paymentEntry = results.FirstOrDefault(r => r.Description == "Mortgage Payment");
            Assert.IsNotNull(paymentEntry, "Should have a mortgage payment entry");
            
            // Checking should decrease by 1500
            Assert.AreEqual(8500m, paymentEntry.AccountBalances["Checking"], "Checking balance should decrease by full payment amount");
            
            // Mortgage should decrease by 1000 (principal)
            Assert.AreEqual(199000m, paymentEntry.AccountBalances["Mortgage"], "Mortgage balance should decrease by principal amount");
        }

        [TestMethod]
        public void TestAdHocMortgageRebalance_IncreasesDebt()
        {
            // Arrange
            var accounts = new List<Account>
            {
                new Account 
                { 
                    Id = 1, 
                    Name = "Mortgage", 
                    Balance = 200000, 
                    Type = AccountType.Mortgage, 
                    IncludeInTotal = true, 
                    BalanceAsOf = new DateTime(2026, 2, 1),
                },
                new Account
                {
                    Id = 2,
                    Name = "Checking",
                    Balance = 10000,
                    Type = AccountType.Checking,
                    IncludeInTotal = true,
                    BalanceAsOf = new DateTime(2026, 2, 1)
                }
            };

            // Ad-hoc transaction: Rebalance of 1500 to the mortgage account.
            // Expected debt increase = 1500.
            var adHocs = new List<AdHocTransaction>
            {
                new AdHocTransaction
                {
                    Id = 101,
                    Description = "Mortgage Rebalance",
                    Amount = 1500,
                    Date = new DateTime(2026, 2, 15),
                    ToAccountId = 1, // To Mortgage
                    IsRebalance = true
                }
            };

            var startDate = new DateTime(2026, 2, 1);
            var endDate = new DateTime(2026, 3, 1);

            // Act
            var results = _engine.CalculateProjections(startDate, endDate, accounts, new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), new List<PeriodBill>(), new List<PeriodBucket>(), adHocs).ToList();

            // Assert
            var rebalanceEntry = results.FirstOrDefault(r => r.Description == "Mortgage Rebalance");
            Assert.IsNotNull(rebalanceEntry, "Should have a mortgage rebalance entry");
            
            // Mortgage should increase by 1500
            Assert.AreEqual(201500m, rebalanceEntry.AccountBalances["Mortgage"], "Mortgage balance should increase by rebalance amount");
            
            // Total balance: Checking (10000) - Mortgage (201500) = -191500
            Assert.AreEqual(-191500m, rebalanceEntry.Balance, "Total balance should reflect the rebalance");
        }
    }
}
