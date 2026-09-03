using StayOnTarget.Models;
using StayOnTarget.Services.Projections;

namespace StayOnTarget.Tests
{
    [TestClass]
    public class MinPaymentSweepTests
    {
        private ProjectionEngine _engine = null!;

        [TestInitialize]
        public void Setup()
        {
            _engine = new ProjectionEngine();
        }

        [TestMethod]
        public void TestMinPaymentSweep_TriggersEvenWhenAutoSweepIsOff()
        {
            // Arrange
            var today = DateTime.Today;
            var startDate = new DateTime(today.Year, today.Month, 1);
            if (startDate < today) startDate = startDate.AddMonths(1);
            var endDate = startDate.AddMonths(1).AddDays(15);

            var checking = new Account 
            { 
                Id = 1, 
                Name = "Checking", 
                Balance = 1000m, 
                Type = AccountType.Checking, 
                IncludeInTotal = true, 
                BalanceAsOf = startDate, 
                IsPrimary = true 
            };
            
            // CC with $500 debt and $50 min payment floor
            var creditCard = new Account 
            { 
                Id = 2, 
                Name = "CreditCard", 
                Balance = -500m, 
                Type = AccountType.CreditCard, 
                IncludeInTotal = true, 
                BalanceAsOf = startDate,
                CreditCardDetails = new CreditCardDetails 
                { 
                    StatementDay = 10, 
                    MinPayFloor = 50m, 
                    PayPreviousMonthBalanceInFull = false 
                }
            };

            var accounts = new List<Account> { checking, creditCard };

            var allocations = new List<BucketPaycheckAllocation>(); 
            
            // Act - useAutoSweep is FALSE
            var results = _engine.CalculateProjections(
                new List<Transaction>(),
                new List<Transaction>(),
                new List<Transaction>(),
                new List<Transaction>(),
                startDate, endDate, accounts, 
                new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), allocations, new List<PeriodBill>(), new List<PeriodBucket>(), 
                new List<Transaction>(), 
                null, false, false, false).ToList();

            // Assert
            var sweepDate = new DateTime(startDate.Year, startDate.Month, 10);
            // Find the sweep on Aug 10
            var minPaySweep = results.FirstOrDefault(r => 
                r.TransactionDate == sweepDate && 
                r.Description.Contains("Min-Pay Sweep"));

            Assert.IsNotNull(minPaySweep, $"Should have a Min-Pay Sweep on {sweepDate:yyyy-MM-dd}");
            Assert.AreEqual(50m, Math.Abs(minPaySweep.Amount), "Sweep amount should be the min pay floor");
            
            // Verify balances after sweep
            Assert.AreEqual(950m, minPaySweep.AccountBalances["Checking"]);
            Assert.AreEqual(-450m, minPaySweep.AccountBalances["CreditCard"]);
        }

        [TestMethod]
        public void TestMinPaymentSweep_DoesNotTriggerIfAlreadyPaid()
        {
            // Arrange
            var today = DateTime.Today;
            var startDate = new DateTime(today.Year, today.Month, 1);
            if (startDate < today) startDate = startDate.AddMonths(1);
            var endDate = startDate.AddMonths(2);

            var checking = new Account 
            { 
                Id = 1, 
                Name = "Checking", 
                Balance = 1000m, 
                Type = AccountType.Checking, 
                IncludeInTotal = true, 
                BalanceAsOf = startDate, 
                IsPrimary = true 
            };
            
            var creditCard = new Account 
            { 
                Id = 2, 
                Name = "CreditCard", 
                Balance = -500m, 
                Type = AccountType.CreditCard, 
                IncludeInTotal = true, 
                BalanceAsOf = startDate,
                CreditCardDetails = new CreditCardDetails 
                { 
                    StatementDay = 10, 
                    MinPayFloor = 50m, 
                    PayPreviousMonthBalanceInFull = false 
                }
            };

            var accounts = new List<Account> { checking, creditCard };

            // Add a transaction that pays $60 to the CC before the statement date
            var payment = new Transaction
            {
                Id = 100,
                TransactionDate = startDate.AddDays(4),
                Amount = -60m, // Checking reduces by 60
                AccountId = 1,
                ToAccountId = 2,
                Description = "Manual CC Payment"
            };

            // Use allTransactions as the source for CalculateProjections
            var allTransactions = new List<Transaction> { payment };

            var allocations = new List<BucketPaycheckAllocation>(); 
            
            // Act
            var results = _engine.CalculateProjections(
                allTransactions,
                allTransactions,
                allTransactions,
                allTransactions,
                startDate, endDate, accounts, 
                new List<Paycheck>(), new List<Bill>(), new List<BudgetBucket>(), allocations, new List<PeriodBill>(), new List<PeriodBucket>(), 
                allTransactions, 
                null, false, false, false).ToList();

            // Assert
            var firstStatementDate = new DateTime(startDate.Year, startDate.Month, 10);
            var minPaySweepOnFirstMonth = results.FirstOrDefault(r => 
                r.TransactionDate == firstStatementDate && 
                r.Description.Contains("Min-Pay Sweep"));
            
            if (minPaySweepOnFirstMonth != null)
            {
                var eventsInOrder = results.OrderBy(r => r.TransactionDate).Select(r => $"{r.TransactionDate:yyyy-MM-dd} {r.Description}").ToList();
                var log = string.Join("\n", eventsInOrder);
                var paymentEvent = results.FirstOrDefault(r => r.Description == "Manual CC Payment");
                var paymentLog = paymentEvent != null ? $"{paymentEvent.TransactionDate:yyyy-MM-dd} {paymentEvent.Description}" : "Payment NOT FOUND";
                Assert.Fail($"Should NOT have a Min-Pay Sweep on first month. Payment was: {paymentLog}. Events (Ordered by Date):\n{log}");
            }
        }
    }
}
