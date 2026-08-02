using StayOnTarget.Models;

namespace StayOnTarget.Services;

public class ReconciliationService {
    private readonly BudgetService _budgetService;

    public ReconciliationService(BudgetService budgetService) {
        _budgetService = budgetService;
    }

    public async Task<IEnumerable<Transaction>> GetUnreconciledTransactions(int accountId, bool isFromAccount) {
        var allTransactions = await _budgetService.GetAllUnreconciledTransactionsAsync();

        if (isFromAccount) {
            // Money leaving the account (AccountId matches)
            return allTransactions
                .Where(t => t.AccountId == accountId && !t.FromAccountReconciledId.HasValue)
                .OrderBy(t => t.TransactionDate)
                .ToList();
        }
        else {
            // Money entering the account (ToAccountId matches)
            return allTransactions
                .Where(t => t.ToAccountId == accountId && !t.ToAccountReconciledId.HasValue)
                .OrderBy(t => t.TransactionDate)
                .ToList();
        }
    }
    
    public async Task<(decimal EndingBalance, DateTime? LastTransactionDate, decimal BeginningBalance)> CalculateRunningBalanceAsync(int accountId, IEnumerable<ReconciliationTransaction> transactions) {
        var account = (await _budgetService.GetAllAccountsAsync()).FirstOrDefault(a => a.Id == accountId);
        if (account == null) {
            return (0, null, 0);
        }
var openingBalanceState = await _budgetService.GetAccountBalanceOpeningStateAsync(accountId);
        // Start with the latest reconciliation or the account balance
        var latestRecon = await _budgetService.GetLatestValidReconciliationAsync(accountId);
        decimal balance = latestRecon?.ReconciledBalance ?? account.Balance;
        decimal beginningBalance = balance;
        DateTime startDate = latestRecon?.ReconciledAsOfDate ?? (openingBalanceState.openingBalanceDate ?? account.BalanceAsOf);

        // Apply transactions after the reconciliation date
        var orderedTransactions = transactions.Where(t => t.TransactionDate >= startDate).OrderBy(t => t.TransactionDate).ToList();

        foreach (var transaction in orderedTransactions) {
            decimal amount = Math.Abs(transaction.Amount);
            bool isDebitAccount = account.IsLiability;

            // Money leaving the account
            if (transaction.AccountId == accountId) {
                if (isDebitAccount) {
                    balance += amount; // Debt increases
                }
                else {
                    balance -= amount; // Asset decreases
                }

                transaction.RunningBalance = balance;
            }

            // Money entering the account
            if (transaction.ToAccountId == accountId) {
                bool isPrincipalOnly = transaction.IsPrincipalOnly;
                bool isRebalance = transaction.IsRebalance;
                bool isInterestOnly = transaction.IsInterestOnly;

                if (account.IsLoanAccount) {
                    if (isRebalance || isInterestOnly)
                        balance += amount;
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
                    if (isRebalance || isInterestOnly)
                        balance += amount;
                    else
                        balance -= amount; // Payment reduces credit card balance
                }
                else if (account.Type == AccountType.PersonalLoan) {
                    if (isPrincipalOnly)
                        balance -= amount;
                    else if (isRebalance)
                        balance += amount;
                    else
                        balance += amount;
                }
                else {
                    balance += amount; // Asset increases
                }

                transaction.RunningBalance = balance;
            }
        }

        var lastTransactionDate = orderedTransactions.LastOrDefault()?.TransactionDate;
        return (balance, lastTransactionDate, beginningBalance);
    }

    public async Task ReconcileAccountAsync(int accountId, IEnumerable<ReconciliationTransaction> reconciledTransactions, decimal finalBalance,
        DateTime asOfDate) {
        // Create the reconciliation record
        var reconciliation = new AccountReconciliation {
            AccountId = accountId,
            ReconciledAsOfDate = asOfDate,
            ReconciledBalance = finalBalance,
            ReconciledOnDate = DateTime.Today,
            IsInvalidated = false
        };

        await _budgetService.UpsertAccountReconciliationAsync(reconciliation);

        // Update transactions with the reconciliation ID
        foreach (var transaction in reconciledTransactions.Where(t => t.IsReconciled)) {
            if (transaction.AccountId == accountId) {
                transaction.FromAccountReconciledId = reconciliation.Id;
            }
            else if (transaction.ToAccountId == accountId) {
                transaction.ToAccountReconciledId = reconciliation.Id;
            }
            //no dates or amounts are changing.
             await _budgetService.UpdateTransactionForReconciliationAsync(transaction); 
        }
    }
}