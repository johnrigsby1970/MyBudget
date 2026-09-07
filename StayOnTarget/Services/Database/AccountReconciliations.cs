using System.Data;
using Dapper;
using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<AccountReconciliation>> GetAllAccountReconciliationsAsync() {
        try {
            await using var conn = _db.GetConnection();
            var reconciliations = (await conn.QueryAsync<AccountReconciliation>("SELECT * FROM AccountReconciliations"))
                .ToList();

            var accounts = (await GetAllAccountsAsync()).ToDictionary(a => a.Id, a => a.Name);

            foreach (var recon in reconciliations) {
                if (accounts.TryGetValue(recon.AccountId, out var accountName)) {
                    recon.AccountName = accountName;
                }
            }

            return reconciliations;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting all account reconciliations[cite: 23].");
            return Enumerable.Empty<AccountReconciliation>();
        }
    }

    public async Task<AccountReconciliation?> GetReconciliationByAsOfDateAsync(int accountId, DateTime asOfDate) {
        try {
            await using var conn = _db.GetConnection();
            return await conn.QueryFirstOrDefaultAsync<AccountReconciliation>(
                @"SELECT * FROM AccountReconciliations
              WHERE AccountId = @accountId 
                AND ReconciledAsOfDate = @asOfDate 
                AND IsInvalidated = 0
              LIMIT 1",
                new { accountId, asOfDate = asOfDate.ToString("yyyy-MM-dd") });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting reconciliation for account ID {AccountId} on {AsOfDate}.", accountId, asOfDate);
            return null;
        }
    }
    
    public async Task<AccountReconciliation?> GetLatestValidReconciliationAsync(int accountId) {
        try {
            await using var conn = _db.GetConnection();
            return await conn.QueryFirstOrDefaultAsync<AccountReconciliation>(
                @"SELECT * FROM AccountReconciliations
                  WHERE AccountId = @accountId AND IsInvalidated = 0
                  ORDER BY ReconciledAsOfDate DESC
                  LIMIT 1",
                new { accountId });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting latest valid reconciliation for account ID {AccountId}[cite: 23].", accountId);
            return null;
        }
    }

    public async Task UpsertAccountReconciliationAsync(AccountReconciliation reconciliation) {
        try {
            await using var conn = _db.GetConnection();

            // Check for an existing valid record on the same date when creating a new reconciliation
            if (reconciliation.Id == 0) {
                var existing = await GetReconciliationByAsOfDateAsync(reconciliation.AccountId, reconciliation.ReconciledAsOfDate);
                if (existing != null) {
                    // Reuse existing ID to merge/overwrite
                    reconciliation.Id = existing.Id;
                }
            }

            var param = new {
                reconciliation.Id,
                reconciliation.AccountId,
                ReconciledAsOfDate = reconciliation.ReconciledAsOfDate.ToString("yyyy-MM-dd"),
                reconciliation.ReconciledBalance,
                ReconciledOnDate = reconciliation.ReconciledOnDate.ToString("yyyy-MM-dd"),
                IsInvalidated = reconciliation.IsInvalidated ? 1 : 0
            };

            if (reconciliation.Id == 0) {
                reconciliation.Id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO AccountReconciliations (AccountId, ReconciledAsOfDate, ReconciledBalance, ReconciledOnDate, IsInvalidated)
                VALUES (@AccountId, @ReconciledAsOfDate, @ReconciledBalance, @ReconciledOnDate, @IsInvalidated);
                SELECT last_insert_rowid();", param);
            }
            else {
                await conn.ExecuteAsync(@"
                UPDATE AccountReconciliations
                SET AccountId = @AccountId, 
                    ReconciledAsOfDate = @ReconciledAsOfDate,
                    ReconciledBalance = @ReconciledBalance, 
                    ReconciledOnDate = @ReconciledOnDate,
                    IsInvalidated = @IsInvalidated
                WHERE Id = @Id", param);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error upserting account reconciliation with ID {ReconciliationId}.", reconciliation.Id);
            throw;
        }
    }

    private async Task DeleteAccountReconciliationAsync(int id, IDbTransaction? tx = null) {
        try {
            var conn = tx?.Connection ?? _db.GetConnection();

            await conn.ExecuteAsync(@"
                UPDATE Transactions
                SET ReconciliationId = NULL
                WHERE ReconciliationId = @id", new { id }, tx);

            await conn.ExecuteAsync("DELETE FROM AccountReconciliations WHERE Id = @id", new { id }, tx);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error deleting account reconciliation with ID {ReconciliationId}[cite: 23].", id);
            throw;
        }
    }

    /// <summary>
    /// Updates IsCleared and ReconciliationId for a batch of transactions inside a single 
    /// connection and transaction context. Optimized specifically for the reconciliation workflow.
    /// </summary>
    public async Task<bool> UpdateTransactionsForReconciliationAsync(IEnumerable<Transaction> transactions) {
        try {
            var txList = transactions.ToList();
            if (!txList.Any()) return true;

            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            try {
                foreach (var transaction in txList) {
                    if (transaction.AccountId.HasValue) {
                        await conn.ExecuteAsync(@"
                    UPDATE Transactions 
                    SET ReconciliationId = @ReconciliationId, 
                        IsCleared = @IsCleared 
                    WHERE AccountId = @AccountId 
                      AND TransactionId = @TransactionId",
                            new {
                                AccountId = transaction.AccountId,
                                ReconciliationId = transaction.FromAccountReconciliationId,
                                TransactionId = transaction.TransactionId.ToString(),
                                IsCleared = (transaction.FromAccountIsCleared ?? false) ? 1 : 0
                            }, tx);
                    }

                    if (transaction.ToAccountId.HasValue) {
                        await conn.ExecuteAsync(@"
                    UPDATE Transactions 
                    SET ReconciliationId = @ReconciliationId, 
                        IsCleared = @IsCleared 
                    WHERE AccountId = @ToAccountId 
                      AND TransactionId = @TransactionId",
                            new {
                                ToAccountId = transaction.ToAccountId,
                                ReconciliationId = transaction.ToAccountReconciliationId,
                                TransactionId = transaction.TransactionId.ToString(),
                                IsCleared = (transaction.ToAccountIsCleared ?? false) ? 1 : 0
                            }, tx);
                    }
                }

                await tx.CommitAsync();
                return true;
            }
            catch {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error updating transactions for reconciliation batch[cite: 23].");
            throw;
        }
    }
}