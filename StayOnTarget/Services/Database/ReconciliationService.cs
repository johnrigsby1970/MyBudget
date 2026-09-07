using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public class ReconciliationService {
    private readonly BudgetService _budgetService;

    public ReconciliationService(BudgetService budgetService) {
        _budgetService = budgetService;
    }

    public async Task<(decimal EndingBalance, DateTime? LastReconciliationDate, DateTime? LastTransactionDate, decimal BeginningBalance, bool
            HasTransactionPriorToLastReconcile)>
        CalculateRunningBalanceAsync(int accountId, IEnumerable<TransactionViewModel> transactions) {
        try {
            var hasTransactionPriorToLastReconcile = false;
            var account = (await _budgetService.GetAllAccountsAsync()).FirstOrDefault(a => a.Id == accountId);
            if (account == null) {
                return (0, null, null, 0, false);
            }

            var openingBalanceState = await _budgetService.GetAccountBalanceOpeningStateAsync(accountId);
            var latestRecon = await _budgetService.GetLatestValidReconciliationAsync(accountId);

            decimal rawStartingBalance = latestRecon?.ReconciledBalance ?? account.Balance;

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

                if (transaction.AccountId == accountId) {
                    if (account.IsLiability) {
                        balance += amount;
                    }
                    else {
                        balance -= amount;
                    }

                    transaction.RunningBalance = balance;
                }

                if (transaction.ToAccountId == accountId) {
                    bool isPrincipalOnly = transaction.IsPrincipalOnly;
                    bool isRebalance = transaction.IsRebalance;
                    bool isInterestOnly = transaction.IsInterestOnly;

                    if (account.IsLoanAccount) {
                        if (isRebalance || isInterestOnly) {
                            balance += amount;
                        }
                        else {
                            decimal principal = amount;
                            if (!isPrincipalOnly && account.MortgageDetails != null) {
                                principal = amount - account.MortgageDetails.Escrow -
                                            account.MortgageDetails.MortgageInsurance;
                                if (principal < 0) principal = 0;
                            }

                            balance -= principal;
                        }
                    }
                    else if (account.Type == AccountType.CreditCard) {
                        if (isRebalance || isInterestOnly) {
                            balance += amount;
                        }
                        else {
                            balance -= amount;
                        }
                    }
                    else if (account.Type == AccountType.PersonalLoan) {
                        if (isRebalance || isInterestOnly) {
                            balance += amount;
                        }
                        else {
                            balance -= amount;
                        }
                    }
                    else {
                        balance += amount;
                    }

                    transaction.RunningBalance = balance;
                }
            }
            DateTime? reconciliationDate = latestRecon?.ReconciledAsOfDate 
                                           ?? openingBalanceState.openingBalanceDate 
                                           ?? account.BalanceAsOf;
            
            var lastTransactionDate = orderedTransactions.LastOrDefault()?.TransactionDate;
            
            return (balance, reconciliationDate, lastTransactionDate, beginningBalance, hasTransactionPriorToLastReconcile);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error calculating running balance for account ID {AccountId}.", accountId);
            return (0, null, null, 0, false);
        }
    }

    public async Task ReconcileAccountAsync(int accountId, List<TransactionViewModel> reconciledTransactions,
        decimal finalBalance, DateTime asOfDate) {
        try {
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

            foreach (var transaction in reconciledTransactions) {
                var changed = false;

                if (transaction.IsReconciled && reconciliationCompleted) {
                    if (transaction.AccountId == accountId) {
                        transaction.FromAccountReconciliationId = reconciliation.Id;
                    }
                    else if (transaction.ToAccountId == accountId) {
                        transaction.ToAccountReconciliationId = reconciliation.Id;
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

            if (pendingUpdates.Any()) {
                await _budgetService.UpdateTransactionsForReconciliationAsync(pendingUpdates);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error reconciling account ID {AccountId}.", accountId);
            throw;
        }
    }
    
    public async Task ClearAccountAsync(int accountId, List<TransactionViewModel> clearedTransactions) {
        try {
            var pendingUpdates = new List<TransactionViewModel>();

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

            if (pendingUpdates.Any()) {
                await _budgetService.UpdateTransactionsForReconciliationAsync(pendingUpdates);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error clearing account transactions for account ID {AccountId}.", accountId);
            throw;
        }
    }
}