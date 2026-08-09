using StayOnTarget.Models;

namespace StayOnTarget.Services;

public class ReconciliationService {
    private readonly BudgetService _budgetService;

    public ReconciliationService(BudgetService budgetService) {
        _budgetService = budgetService;
    }

    // public async Task<IEnumerable<Transaction>> GetUnreconciledTransactions(int accountId, bool isFromAccount) {
    //     var allTransactions = await _budgetService.GetAllUnreconciledTransactionsAsync(accountId);
    //
    //     if (isFromAccount) {
    //         // Money leaving the account (AccountId matches)
    //         return allTransactions
    //             .Where(t => t.AccountId == accountId && !t.FromAccountReconciledId.HasValue)
    //             .OrderBy(t => t.TransactionDate)
    //             .ToList();
    //     }
    //     else {
    //         // Money entering the account (ToAccountId matches)
    //         return allTransactions
    //             .Where(t => t.ToAccountId == accountId && !t.ToAccountReconciledId.HasValue)
    //             .OrderBy(t => t.TransactionDate)
    //             .ToList();
    //     }
    // }

    public async Task<(decimal EndingBalance, DateTime? LastTransactionDate, decimal BeginningBalance, bool
            HasTransactionPriorToLastReconcile)>
        CalculateRunningBalanceAsync(int accountId, IEnumerable<TransactionViewModel> transactions) {
        var hasTransactionPriorToLastReconcile = false;
        var account = (await _budgetService.GetAllAccountsAsync()).FirstOrDefault(a => a.Id == accountId);
        if (account == null) {
            return (0, null, 0, false);
        }

        var openingBalanceState = await _budgetService.GetAccountBalanceOpeningStateAsync(accountId);
        var latestRecon = await _budgetService.GetLatestValidReconciliationAsync(accountId);

        // 1. Get raw database starting balance
        decimal rawStartingBalance = latestRecon?.ReconciledBalance ?? account.Balance;

        // 2. Normalize: Liabilities are inverted to positive debt balances for display/calculation loops
        decimal balance = account.IsLiability ? Math.Abs(rawStartingBalance) : rawStartingBalance;
        decimal beginningBalance = balance;

        DateTime startDate = latestRecon?.ReconciledAsOfDate ??
                             (openingBalanceState.openingBalanceDate ?? account.BalanceAsOf);

        var earliestTransaction = transactions.OrderBy(t => t.TransactionDate).FirstOrDefault();
        if (earliestTransaction != null && earliestTransaction.TransactionDate < startDate) {
            startDate = earliestTransaction.TransactionDate;
            hasTransactionPriorToLastReconcile = true;
        }

        var orderedTransactions = transactions
            .Where(t => t.TransactionDate >= startDate)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        foreach (var transaction in orderedTransactions) {
            decimal amount = Math.Abs(transaction.Amount);

            // --- OUTBOUND: Money leaving this account ---
            if (transaction.AccountId == accountId) {
                if (account.IsLiability) {
                    balance += amount; // Purchasing on credit/loans INCREASES total debt
                }
                else {
                    balance -= amount; // Spending from checking/savings DECREASES asset balance
                }

                transaction.RunningBalance = balance;
            }

            // --- INBOUND: Money entering this account ---
            if (transaction.ToAccountId == accountId) {
                bool isPrincipalOnly = transaction.IsPrincipalOnly;
                bool isRebalance = transaction.IsRebalance;
                bool isInterestOnly = transaction.IsInterestOnly;

                if (account.IsLoanAccount) {
                    if (isRebalance || isInterestOnly) {
                        balance += amount; // Fee or interest adjustment INCREASES loan balance
                    }
                    else {
                        decimal principal = amount;
                        if (!isPrincipalOnly && account.MortgageDetails != null) {
                            principal = amount - account.MortgageDetails.Escrow -
                                        account.MortgageDetails.MortgageInsurance;
                            if (principal < 0) principal = 0;
                        }

                        balance -= principal; // Loan payment DECREASES principal balance
                    }
                }
                else if (account.Type == AccountType.CreditCard) {
                    if (isRebalance || isInterestOnly) {
                        balance += amount; // Finance charge INCREASES credit card debt
                    }
                    else {
                        balance -= amount; // Payment DECREASES credit card debt
                    }
                }
                else if (account.Type == AccountType.PersonalLoan) {
                    if (isRebalance || isInterestOnly) {
                        balance += amount; // Charge INCREASES debt
                    }
                    else {
                        balance -= amount; // Payment DECREASES loan balance
                    }
                }
                else {
                    balance += amount; // Deposit INCREASES asset balance
                }

                transaction.RunningBalance = balance;
            }
        }

        var lastTransactionDate = orderedTransactions.LastOrDefault()?.TransactionDate;
        return (balance, lastTransactionDate, beginningBalance, hasTransactionPriorToLastReconcile);
    }

    public async Task ReconcileAccountAsync(int accountId, List<TransactionViewModel> reconciledTransactions,
        decimal finalBalance, DateTime asOfDate) {
    
        bool reconciliationCompleted = false;

        var reconciliation = new AccountReconciliation {
            AccountId = accountId,
            ReconciledAsOfDate = asOfDate,
            ReconciledBalance = finalBalance,
            ReconciledOnDate = DateTime.Today,
            IsInvalidated = false
        };

        if (reconciledTransactions.Any(x => x.IsReconciled)) {
            await _budgetService.UpsertAccountReconciliationAsync(reconciliation);
            reconciliationCompleted = true;
        }

        var pendingUpdates = new List<TransactionViewModel>();

        // Prepare state changes in memory
        foreach (var transaction in reconciledTransactions) {
            var changed = false;

            if (transaction.IsReconciled && reconciliationCompleted) {
                if (transaction.AccountId == accountId) {
                    transaction.FromAccountReconciledId = reconciliation.Id;
                }
                else if (transaction.ToAccountId == accountId) {
                    transaction.ToAccountReconciledId = reconciliation.Id;
                }
            }

            if (transaction.AccountId == accountId) {
                changed = transaction.FromAccountIsCleared != transaction.IsCleared;
                transaction.FromAccountIsCleared = transaction.IsCleared;
            }
            else if (transaction.ToAccountId == accountId) {
                changed = transaction.ToAccountIsCleared != transaction.IsCleared;
                transaction.ToAccountIsCleared = transaction.IsCleared;
            }

            if (changed || transaction.IsReconciled) {
                pendingUpdates.Add(transaction);
            }
        }

        // Single batch update call to BudgetService
        if (pendingUpdates.Any()) {
            await _budgetService.UpdateTransactionsForReconciliationAsync(pendingUpdates);
        }
    }
    
    public async Task ClearAccountAsync(int accountId, List<TransactionViewModel> clearedTransactions) {
        var pendingUpdates = new List<TransactionViewModel>();

        // Prepare state changes in memory
        foreach (var transaction in clearedTransactions) {
            var changed = false;
            
            if (transaction.AccountId == accountId) {
                changed = transaction.FromAccountIsCleared != transaction.IsCleared;
                transaction.FromAccountIsCleared = transaction.IsCleared;
            }
            else if (transaction.ToAccountId == accountId) {
                changed = transaction.ToAccountIsCleared != transaction.IsCleared;
                transaction.ToAccountIsCleared = transaction.IsCleared;
            }

            if (changed || transaction.IsReconciled) {
                pendingUpdates.Add(transaction);
            }
        }

        // Single batch update call to BudgetService
        if (pendingUpdates.Any()) {
            await _budgetService.UpdateTransactionsForReconciliationAsync(pendingUpdates);
        }
    }
}