using MyBudget.Models;
using MyBudget.ViewModels;

namespace MyBudget.Services;

public interface IProjectionEngine {
    IEnumerable<ProjectionItem> CalculateProjections(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<Account> accounts,
        IEnumerable<Paycheck> paychecks,
        IEnumerable<Bill> bills,
        IEnumerable<BudgetBucket> buckets,
        IEnumerable<PeriodBill> periodBills,
        IEnumerable<PeriodBucket> periodBuckets,
        IEnumerable<Transaction> transactions);
}

public class ProjectionEngine : IProjectionEngine {
    public enum ProjectionEventType {
        Paycheck,
        Bill,
        Transfer,
        Bucket,
        Transaction,
        Interest,
        Growth,
        Final
    }

    public IEnumerable<ProjectionItem> CalculateProjections(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<Account> accounts,
        IEnumerable<Paycheck> paychecks,
        IEnumerable<Bill> bills,
        IEnumerable<BudgetBucket> buckets,
        IEnumerable<PeriodBill> periodBills,
        IEnumerable<PeriodBucket> periodBuckets,
        IEnumerable<Transaction> transactions) {
        var list = new List<ProjectionItem>();
        DateTime current = startDate;

        var accountBalances = accounts.ToDictionary(a => a.Id, a => a.Balance);
        var accountNames = accounts.ToDictionary(a => a.Id, a => a.Name);

        var includedTotalAccounts = new HashSet<int>(accounts.Where(a => a.IncludeInTotal).Select(a => a.Id));

        var unbalancedPaychecks = paychecks.Where(p => !p.IsBalanced).ToList();
        if (unbalancedPaychecks.Any()) {
            DateTime earliestUnbalanced = unbalancedPaychecks.Min(p => p.StartDate);
            if (earliestUnbalanced < current) {
                current = earliestUnbalanced;
            }
        }

        var events =
            new List<(DateTime Date, decimal Amount, string Description, int? FromAccountId, int? ToAccountId, int?
                BucketId, int? PaycheckId, DateTime? PaycheckOccurrenceDate, ProjectionEventType Type, bool
                IsPrincipalOnly, bool IsRebalance)>();

        // 1. Paychecks
        var cashAccount = accounts.FirstOrDefault(a => a.Name == "Household Cash");
        foreach (var pay in paychecks) {
            DateTime nextPay = pay.StartDate;
            DateTime endPay = pay.StartDate;
            endPay = pay.Frequency switch {
                Frequency.Weekly => endPay.AddDays(7),
                Frequency.BiWeekly => endPay.AddDays(14),
                Frequency.Monthly => endPay.AddMonths(1),
                _ => endPay.AddYears(100)
            };

            while (nextPay < endDate) {
                if (nextPay >= current && (pay.EndDate == null || nextPay <= pay.EndDate)) {
                    // Association mechanism: check if a transaction overrides this paycheck occurrence
                    var transactionOverride = transactions.FirstOrDefault(a =>
                        a.PaycheckId == pay.Id && a.Date >= nextPay &&
                        a.Date < endPay); //&& a.PaycheckOccurrenceDate?.Date == nextPay.Date);

                    if (transactionOverride == null) {
                        int? toAccountId = pay.AccountId ?? cashAccount?.Id;
                        events.Add((nextPay, pay.ExpectedAmount, $"Expected Pay: {pay.Name}", null, toAccountId, null,
                            pay.Id, nextPay, ProjectionEventType.Paycheck, false, false));
                    }
                }

                nextPay = pay.Frequency switch {
                    Frequency.Weekly => nextPay.AddDays(7),
                    Frequency.BiWeekly => nextPay.AddDays(14),
                    Frequency.Monthly => nextPay.AddMonths(1),
                    _ => nextPay.AddYears(100)
                };
                endPay = nextPay;
                endPay = pay.Frequency switch {
                    Frequency.Weekly => endPay.AddDays(7),
                    Frequency.BiWeekly => endPay.AddDays(14),
                    Frequency.Monthly => endPay.AddMonths(1),
                    _ => endPay.AddYears(100)
                };
            }
        }

        // 2. Bills & Transfers
        var primaryChecking = accounts.FirstOrDefault(a => a.Type == AccountType.Checking)?.Id;

        foreach (var bill in bills) {
            DateTime nextDue = bill.NextDueDate ?? current;
            if (bill.NextDueDate == null) {
                nextDue = new DateTime(current.Year, current.Month,
                    Math.Min(bill.DueDay, DateTime.DaysInMonth(current.Year, current.Month)));
                if (nextDue < current) nextDue = nextDue.AddMonths(1);
            }

            while (nextDue < endDate) {
                var pb = periodBills.FirstOrDefault(p => p.BillId == bill.Id && p.DueDate.Date == nextDue.Date);
                decimal amountToUse = (pb != null) ? pb.ActualAmount : bill.ExpectedAmount;
                string paidSuffix = (pb != null && pb.IsPaid) ? " (PAID)" : "";

                int? fromAccId = bill.AccountId ?? primaryChecking;
                if (bill.ToAccountId.HasValue) {
                    events.Add((nextDue, amountToUse, $"Transfer: {bill.Name}{paidSuffix}", fromAccId,
                        bill.ToAccountId.Value, null, null, null, ProjectionEventType.Transfer, false, false));
                }
                else {
                    events.Add((nextDue, -amountToUse, $"Bill: {bill.Name}{paidSuffix}", fromAccId, null, null, null,
                        null, ProjectionEventType.Bill, false, false));
                }

                nextDue = bill.Frequency switch {
                    Frequency.Monthly => nextDue.AddMonths(1),
                    Frequency.Yearly => nextDue.AddYears(1),
                    Frequency.Weekly => nextDue.AddDays(7),
                    Frequency.BiWeekly => nextDue.AddDays(14),
                    _ => nextDue.AddYears(100)
                };
            }
        }

        // 3. Buckets
        foreach (var bucket in buckets) {
            foreach (var pay in paychecks) {
                DateTime nextPay = pay.StartDate;
                while (nextPay < endDate) {
                    if (nextPay >= current && (pay.EndDate == null || nextPay <= pay.EndDate)) {
                        var pb = periodBuckets.FirstOrDefault(p =>
                            p.BucketId == bucket.Id && p.PeriodDate.Date == nextPay.Date);
                        decimal amountToUse = (pb != null) ? pb.ActualAmount : bucket.ExpectedAmount;
                        string paidSuffix = (pb != null && pb.IsPaid) ? " (PAID)" : "";

                        int? fromAccId = bucket.AccountId ?? primaryChecking;
                        events.Add((nextPay, -amountToUse, $"Bucket: {bucket.Name}{paidSuffix}", fromAccId, null,
                            bucket.Id, null, null, ProjectionEventType.Bucket, false, false));
                    }

                    nextPay = pay.Frequency switch {
                        Frequency.Weekly => nextPay.AddDays(7),
                        Frequency.BiWeekly => nextPay.AddDays(14),
                        Frequency.Monthly => nextPay.AddMonths(1),
                        _ => nextPay.AddYears(100)
                    };
                }
            }
        }

        // 4. Transactions
        foreach (var transaction in transactions) {
            // We need to collect ALL transactions that could affect balances from the earliest BalanceAsOf
            events.Add((transaction.Date, transaction.Amount, transaction.Description, transaction.AccountId, transaction.ToAccountId, transaction.BucketId,
                transaction.PaycheckId, transaction.PaycheckOccurrenceDate, ProjectionEventType.Transaction, transaction.IsPrincipalOnly,
                transaction.IsRebalance));
        }

        // 5. Interest & Growth
        foreach (var acc in accounts) {
            if (acc.Type == AccountType.Mortgage && acc.MortgageDetails != null) {
                DateTime nextInterest = acc.MortgageDetails.PaymentDate;
                if (nextInterest == DateTime.MinValue) nextInterest = startDate;
                while (nextInterest < startDate) nextInterest = nextInterest.AddMonths(1);

                while (nextInterest < endDate) {
                    // Check if there is a transaction to this account on this date
                    // The user said: "If a transaction has a toaccountifd that is an interest accruing account, 
                    // then the record is either interest or a rebalance of the account and should be treated as the interest for that period."
                    var hasInterestTransaction =
                        transactions.Any(a => a.ToAccountId == acc.Id && a.Date.Date == nextInterest.Date);

                    if (!hasInterestTransaction) {
                        events.Add((nextInterest, 0, $"Interest: {acc.Name}", acc.Id, null, null, null, null,
                            ProjectionEventType.Interest, false, false));
                    }

                    nextInterest = nextInterest.AddMonths(1);
                }
            }
        }

        var sortedEvents = events.OrderBy(e => e.Date).ThenByDescending(e => e.Type == ProjectionEventType.Paycheck)
            .ToList();

        // 6. Growth tracking
        var accountBalanceDates = accounts.ToDictionary(a => a.Id, a => a.BalanceAsOf);
        var accumulatedGrowth = accounts.ToDictionary(a => a.Id, a => 0m);

        // Recalculate accountBalances based on BalanceAsOf and events before 'current'
        foreach (var acc in accounts) {
            // Reset to DB balance
            accountBalances[acc.Id] = acc.Balance;

            // Apply events that happened between acc.BalanceAsOf and current
            var priorEvents = sortedEvents.Where(e => e.Date >= acc.BalanceAsOf && e.Date < current).ToList();
            foreach (var e in priorEvents) {
                decimal amountChange = Math.Abs(e.Amount);
                if (e.FromAccountId == acc.Id) {
                    if (acc.Type == AccountType.Mortgage || acc.Type == AccountType.PersonalLoan) {
                        accountBalances[acc.Id] += amountChange;
                    }
                    else {
                        accountBalances[acc.Id] -= amountChange;
                    }
                }

                if (e.ToAccountId == acc.Id) {
                    bool isMortgage = acc.Type == AccountType.Mortgage;
                    bool isPersonalLoan = acc.Type == AccountType.PersonalLoan;
                    bool isPrincipalOnly = e.IsPrincipalOnly;
                    bool isRebalance = e.IsRebalance;

                    // Robust interest/rebalance detection as per requirements:
                    // "If a transaction has a toaccountifd that is an interest accruing account, 
                    // then the record is either interest or a rebalance of the account and should be treated as the interest for that period."
                    bool isInterestOrRebalance = isMortgage && (e.Type == ProjectionEventType.Transaction && isRebalance);

                    if (isInterestOrRebalance) {
                        accountBalances[acc.Id] += amountChange;
                    }
                    else if (isMortgage) {
                        decimal principal = amountChange;
                        if (!isPrincipalOnly && acc.MortgageDetails != null) {
                            principal = amountChange - acc.MortgageDetails.Escrow -
                                        acc.MortgageDetails.MortgageInsurance;
                            if (principal < 0) principal = 0;
                        }

                        accountBalances[acc.Id] -= principal;
                    }
                    else if (isPersonalLoan && (e.Type == ProjectionEventType.Transaction && isPrincipalOnly)) {
                        accountBalances[acc.Id] -= amountChange;
                    }
                    else if (isPersonalLoan && (e.Type == ProjectionEventType.Transaction && isRebalance)) {
                        // Explicit rebalance for personal loan increases debt
                        accountBalances[acc.Id] += amountChange;
                    }
                    else if (isPersonalLoan) {
                        // Standard personal loan payment (treated as transfer for now unless amortization is added)
                        accountBalances[acc.Id] += amountChange;
                    }
                    else {
                        accountBalances[acc.Id] += amountChange;
                    }
                }
            }
        }

        decimal runningBalance = accounts.Where(a => includedTotalAccounts.Contains(a.Id)).Sum(a => {
            var bal = accountBalances[a.Id];
            return (a.Type == AccountType.Mortgage || a.Type == AccountType.PersonalLoan) ? -bal : bal;
        });

        DateTime lastDate = current;

        var futureEvents = sortedEvents.Where(e => e.Date >= current).ToList();
        var paycheckDates = futureEvents
            .Where(e => e.Type == ProjectionEventType.Paycheck ||
                        (e.Type == ProjectionEventType.Transaction && e.PaycheckId.HasValue)).Select(e => e.Date).Distinct()
            .OrderBy(d => d).ToList();

        // Ensure the projection start date is considered a period boundary if no paycheck falls on it
        if (!paycheckDates.Any() || paycheckDates[0] > current) {
            paycheckDates.Insert(0, current);
        }

        // Track bucket spending per period
        var bucketSpending = new Dictionary<(DateTime PeriodDate, int BucketId), decimal>();
        foreach (var transaction in transactions) {
            if (transaction.BucketId.HasValue) {
                // Find which period this transaction falls into
                DateTime periodDate = paycheckDates.LastOrDefault(d => d <= transaction.Date);
                if (periodDate != DateTime.MinValue) {
                    var key = (periodDate, transaction.BucketId.Value);
                    if (!bucketSpending.ContainsKey(key)) bucketSpending[key] = 0;
                    bucketSpending[key] += Math.Abs(transaction.Amount);
                }
            }
        }

        foreach (var e in futureEvents) {
            // Process each day between projection events to account for daily value growth
            int days = (e.Date - lastDate).Days; // Number of whole days to bridge from previous event to current event
            if (days > 0) {
                for (int d = 0; d < days; d++) // Simulate day-by-day to compound growth and handle balance start dates
                {
                    DateTime dayDate = lastDate.AddDays(d); // The specific simulated day

                    // Consider only accounts that accrue growth; mortgages are excluded (handled in a dedicated section)
                    foreach (var acc in accounts.Where(a => a.AnnualGrowthRate > 0 && a.Type != AccountType.Mortgage)) {
                        // Do not accrue before the account’s known starting balance date
                        if (dayDate < accountBalanceDates[acc.Id]) continue;

                        // Compute one-day growth using simple daily rate from annual percentage
                        decimal dailyRate = acc.AnnualGrowthRate / 100m / 365m; // e.g., 5% → 0.05/365 per day
                        decimal growth = accountBalances[acc.Id] * dailyRate; // Compound on current balance

                        // Accumulate sub-cent growth to avoid rounding on every day
                        accumulatedGrowth[acc.Id] += growth;

                        // When at least ±$0.01 has accumulated, post it to the account balance
                        if (accumulatedGrowth[acc.Id] >= 0.01m || accumulatedGrowth[acc.Id] <= -0.01m) {
                            decimal toAdd = Math.Round(accumulatedGrowth[acc.Id],
                                2); // Round to cents (banker’s rounding)
                            accountBalances[acc.Id] += toAdd; // Apply the posted cents to the balance

                            // Keep the global running total in sync for accounts included in overall totals
                            if (includedTotalAccounts.Contains(acc.Id)) {
                                // For debt accounts, a positive "growth" increases what you owe → decreases net total
                                if (acc.Type == AccountType.Mortgage || acc.Type == AccountType.PersonalLoan) {
                                    runningBalance -= toAdd;
                                }
                                else {
                                    runningBalance += toAdd;
                                }
                            }

                            // Remove what was posted; leave any remaining fractional cents in the accumulator
                            accumulatedGrowth[acc.Id] -= toAdd;
                        }
                    }
                }
            }

            lastDate = e.Date; // Advance the timeline to the current event date

            // Apply Interest for Mortgage
            if (e.Type == ProjectionEventType.Interest && e.FromAccountId.HasValue) {
                var acc = accounts.FirstOrDefault(a => a.Id == e.FromAccountId.Value);
                if (acc != null && acc.Type == AccountType.Mortgage && acc.MortgageDetails != null) {
                    decimal monthlyRate = (acc.MortgageDetails.InterestRate / 100m) / 12m;
                    decimal interest = Math.Round(accountBalances[acc.Id] * monthlyRate, 2);
                    accountBalances[acc.Id] += interest;
                    if (includedTotalAccounts.Contains(acc.Id)) {
                        runningBalance -= interest;
                    }

                    // Create a projection item for the interest added
                    list.Add(new ProjectionItem {
                        Date = e.Date,
                        Description = e.Description,
                        Amount = interest,
                        Balance = runningBalance,
                        AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value)
                    });
                    continue; // Already added projection item
                }
            }

            // Check if this is an transaction interest/rebalance for an interest-accruing account
            // As per requirements: "If a transaction has a toaccountifd that is an interest accruing account, 
            // then the record is either interest or a rebalance of the account and should be treated as the interest for that period."
            if (e.ToAccountId.HasValue && e.Type == ProjectionEventType.Transaction) {
                var toAcc = accounts.FirstOrDefault(a => a.Id == e.ToAccountId.Value);
                if (toAcc != null && toAcc.Type == AccountType.Mortgage) {
                    // This transaction will be processed by the generic balance application below (as a deposit to the mortgage account, which increases the debt).
                    // We don't need to do anything special here, just ensuring we don't accidentally treat it as a regular transfer.
                }
            }

            // Apply balances
            if (e.Type == ProjectionEventType.Bucket && e.BucketId.HasValue) {
                // Find how much has been spent from this bucket in this period so far
                DateTime periodDate = paycheckDates.LastOrDefault(d => d <= e.Date);
                if (periodDate != DateTime.MinValue) {
                    var key = (periodDate, e.BucketId.Value);
                    decimal spent = bucketSpending.ContainsKey(key) ? bucketSpending[key] : 0;
                    // projected amount is negative, spent is positive.
                    // We reduce the absolute value of the projected amount.
                    decimal projectedAmount = Math.Abs(e.Amount);
                    decimal remainingToProject = Math.Max(0, projectedAmount - spent);

                    // We need to update the balance application.
                    // Let's adjust e.Amount for the following logic.
                    // Note: e is a tuple, we can't modify it easily. We'll use a local variable.
                }
            }

            decimal currentEventAmount = e.Amount;
            if (e.Type == ProjectionEventType.Bucket && e.BucketId.HasValue) {
                DateTime periodDate = paycheckDates.LastOrDefault(d => d <= e.Date);
                if (periodDate != DateTime.MinValue) {
                    var key = (periodDate, e.BucketId.Value);
                    decimal spent = bucketSpending.ContainsKey(key) ? bucketSpending[key] : 0;
                    decimal projectedAmount = Math.Abs(e.Amount);
                    currentEventAmount = -Math.Max(0, projectedAmount - spent);
                }
            }

            if (e.FromAccountId.HasValue && accountBalances.ContainsKey(e.FromAccountId.Value)) {
                var fromAcc = accounts.FirstOrDefault(a => a.Id == e.FromAccountId.Value);
                decimal amountChange = Math.Abs(currentEventAmount);

                // For Mortgage/PersonalLoan, we normally shouldn't be pulling FROM them unless it's a rebalance or something.
                // But if we do, it increases the debt balance.
                if (fromAcc != null &&
                    (fromAcc.Type == AccountType.Mortgage || fromAcc.Type == AccountType.PersonalLoan)) {
                    accountBalances[e.FromAccountId.Value] += amountChange;
                    if (includedTotalAccounts.Contains(e.FromAccountId.Value)) {
                        runningBalance -= amountChange;
                    }
                }
                else {
                    accountBalances[e.FromAccountId.Value] -= amountChange;
                    if (includedTotalAccounts.Contains(e.FromAccountId.Value)) {
                        runningBalance -= amountChange;
                    }
                }
            }

            if (e.ToAccountId.HasValue && accountBalances.ContainsKey(e.ToAccountId.Value)) {
                var toAcc = accounts.FirstOrDefault(a => a.Id == e.ToAccountId.Value);
                decimal amountChange = Math.Abs(currentEventAmount);

                bool isMortgagePayment = toAcc != null && toAcc.Type == AccountType.Mortgage;
                bool isPersonalLoanPayment = toAcc != null && toAcc.Type == AccountType.PersonalLoan;
                bool isPrincipalOnly = e.IsPrincipalOnly;
                bool isRebalance = e.IsRebalance;

                // As per requirements: "If a transaction has a toaccountifd that is an interest accruing account, 
                // then the record is either interest or a rebalance of the account and should be treated as the interest for that period."
                bool isInterestOrRebalance = isMortgagePayment && (e.Type == ProjectionEventType.Transaction && isRebalance);

                if (isInterestOrRebalance) {
                    // Increases the debt
                    accountBalances[e.ToAccountId.Value] += amountChange;
                    if (includedTotalAccounts.Contains(e.ToAccountId.Value)) {
                        runningBalance -= amountChange;
                    }
                }
                else if (isMortgagePayment) {
                    // Reduces the debt
                    decimal principal = amountChange;
                    if (!isPrincipalOnly && toAcc!.MortgageDetails != null) {
                        principal = amountChange - toAcc.MortgageDetails.Escrow -
                                    toAcc.MortgageDetails.MortgageInsurance;
                        if (principal < 0) principal = 0;
                    }

                    accountBalances[e.ToAccountId.Value] -= principal;
                    if (includedTotalAccounts.Contains(e.ToAccountId.Value)) {
                        runningBalance += principal;
                    }
                }
                else if (isPersonalLoanPayment && (e.Type == ProjectionEventType.Transaction && isPrincipalOnly)) {
                    // Transaction principal only payment to a personal loan
                    accountBalances[e.ToAccountId.Value] -= amountChange;
                    if (includedTotalAccounts.Contains(e.ToAccountId.Value)) {
                        runningBalance += amountChange;
                    }
                }
                else if (isPersonalLoanPayment && (e.Type == ProjectionEventType.Transaction && isRebalance)) {
                    // Transaction rebalance increases debt
                    accountBalances[e.ToAccountId.Value] += amountChange;
                    if (includedTotalAccounts.Contains(e.ToAccountId.Value)) {
                        runningBalance -= amountChange;
                    }
                }
                else if (toAcc != null &&
                         (toAcc.Type == AccountType.Mortgage || toAcc.Type == AccountType.PersonalLoan)) {
                    // Standard payment/transfer to debt increases balance (deposits) unless it's a payment.
                    accountBalances[e.ToAccountId.Value] += amountChange;
                    if (includedTotalAccounts.Contains(e.ToAccountId.Value)) {
                        runningBalance -= amountChange;
                    }
                }
                else {
                    // Regular account, increase balance
                    accountBalances[e.ToAccountId.Value] += amountChange;
                    if (includedTotalAccounts.Contains(e.ToAccountId.Value)) {
                        runningBalance += amountChange;
                    }
                }
            }

            // If neither is set but amount is positive, assume it's income to primary? No, better be robust.
            // If it's a bill/bucket and no AccountId, it already used primaryChecking.
            // If it's a transaction and no accounts, it might just be a note or something, but usually it should have an account.

            var item = new ProjectionItem {
                Date = e.Date,
                Description = e.Description,
                PaycheckId = e.PaycheckId,
                Amount = currentEventAmount,
                Balance = runningBalance,
                AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value)
            };

            list.Add(item);
        }

        // Net per period
        for (int i = 0; i < paycheckDates.Count; i++) {
            DateTime start = paycheckDates[i];
            DateTime next = (i + 1 < paycheckDates.Count) ? paycheckDates[i + 1] : endDate;

            // Find ALL items in this period
            var periodItems = list.Where(item => item.Date >= start && item.Date < next).ToList();
            if (periodItems.Any()) {
                // Assign PeriodNet to the VERY FIRST item in the period
                // This might not be a paycheck if other events happen on the same date before the paycheck
                // but sortedEvents handles paychecks first on same date.
                periodItems.First().PeriodNet = periodItems.Sum(item => item.Amount);
            }
        }

        // Add a final item if there's remaining growth or just to show the final state
        if (lastDate < endDate) {
            int remainingDays = (endDate - lastDate).Days;
            if (remainingDays > 0) {
                for (int d = 0; d < remainingDays; d++) {
                    DateTime dayDate = lastDate.AddDays(d);
                    foreach (var acc in accounts.Where(a => a.AnnualGrowthRate > 0 && a.Type != AccountType.Mortgage)) {
                        if (dayDate < accountBalanceDates[acc.Id]) continue;
                        decimal dailyRate = acc.AnnualGrowthRate / 100m / 365m;
                        decimal growth = accountBalances[acc.Id] * dailyRate;
                        accumulatedGrowth[acc.Id] += growth;
                        if (accumulatedGrowth[acc.Id] >= 0.01m || accumulatedGrowth[acc.Id] <= -0.01m) {
                            decimal toAdd = Math.Round(accumulatedGrowth[acc.Id], 2);
                            accountBalances[acc.Id] += toAdd;
                            if (includedTotalAccounts.Contains(acc.Id)) {
                                if (acc.Type == AccountType.Mortgage || acc.Type == AccountType.PersonalLoan) {
                                    runningBalance -= toAdd;
                                }
                                else {
                                    runningBalance += toAdd;
                                }
                            }

                            accumulatedGrowth[acc.Id] -= toAdd;
                        }
                    }
                }
            }

            list.Add(new ProjectionItem {
                Date = endDate,
                Description = "End of Projection",
                Amount = 0,
                Balance = runningBalance,
                AccountBalances = accountBalances.ToDictionary(kv => accountNames[kv.Key], kv => kv.Value)
            });
        }

        return list;
    }
}