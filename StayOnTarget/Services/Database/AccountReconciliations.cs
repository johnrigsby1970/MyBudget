using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<AccountReconciliation>> GetAllAccountReconciliationsAsync() {
        await using var conn = _db.GetConnection();
        var reconciliations = (await conn.QueryAsync<AccountReconciliation>("SELECT * FROM AccountReconciliations"))
            .ToList();

        // Populate account names for UI
        var accounts = (await GetAllAccountsAsync()).ToDictionary(a => a.Id, a => a.Name);

        foreach (var recon in reconciliations) {
            if (accounts.TryGetValue(recon.AccountId, out var accountName)) {
                recon.AccountName = accountName;
            }
        }

        return reconciliations;
    }

    public async Task<AccountReconciliation?> GetLatestValidReconciliationAsync(int accountId) {
        await using var conn = _db.GetConnection();
        return await conn.QueryFirstOrDefaultAsync<AccountReconciliation>(
            @"SELECT * FROM AccountReconciliations
              WHERE AccountId = @accountId AND IsInvalidated = 0
              ORDER BY ReconciledAsOfDate DESC
              LIMIT 1",
            new { accountId });
    }

    public async Task UpsertAccountReconciliationAsync(AccountReconciliation reconciliation) {
        await using var conn = _db.GetConnection();
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
                SET AccountId=@AccountId, ReconciledAsOfDate=@ReconciledAsOfDate,
                    ReconciledBalance=@ReconciledBalance, ReconciledOnDate=@ReconciledOnDate,
                    IsInvalidated=@IsInvalidated
                WHERE Id=@Id", param);
        }
    }

    private async Task
        InvalidateReconciliationsAfterDateAsync(int accountId, DateTime date, IDbTransaction? tx = null) {
        foreach (var r in await GetInvalidateReconciliationsAfterDateAsync(accountId, date, tx)) {
            await DeleteAccountReconciliationAsync(r, tx);
        }
    }

    private async Task<bool> WillInvalidateReconciliationsAfterDateAsync(
        int accountId,
        DateTime date,
        List<int>? reconciliationsToIgnore = null,
        SqliteConnection? cn = null,
        IDbTransaction? tx = null) {
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try {
            if (isLocalConn && conn.State != ConnectionState.Open) {
                await conn.OpenAsync();
            }

            string sql = @"
            SELECT COUNT(*) FROM AccountReconciliations
            WHERE AccountId = @accountId 
              AND date(ReconciledAsOfDate) >= @date";

            bool hasIgnores = reconciliationsToIgnore != null && reconciliationsToIgnore.Any();

            if (hasIgnores) {
                sql += " AND Id NOT IN @reconciliationsToIgnore";
            }

            object param = hasIgnores
                ? new { accountId, date = date.ToString("yyyy-MM-dd"), reconciliationsToIgnore }
                : new { accountId, date = date.ToString("yyyy-MM-dd") };

            var recordsImpacted = await conn.ExecuteScalarAsync<int>(sql, param, tx);

            return recordsImpacted > 0;
        }
        finally {
            if (isLocalConn) {
                await conn.DisposeAsync();
            }
        }
    }

    private async Task<List<int>> GetInvalidateReconciliationsAfterDateAsync(
        int accountId,
        DateTime date,
        IDbTransaction? tx = null,
        List<int>? reconciliationsToIgnore = null) {
        reconciliationsToIgnore ??= new List<int>();

        bool isLocalConn = tx?.Connection == null;
        // Typing conn as DbConnection gives access to DisposeAsync()
        System.Data.Common.DbConnection conn = tx?.Connection as System.Data.Common.DbConnection
                                               ?? _db.GetConnection();

        try {
            if (isLocalConn && conn.State != ConnectionState.Open) {
                await conn.OpenAsync();
            }

            string sql = @"
            SELECT Id FROM AccountReconciliations
            WHERE AccountId = @accountId 
              AND date(ReconciledAsOfDate) >= @date";

            bool hasIgnores = reconciliationsToIgnore.Any();
            if (hasIgnores) {
                sql += " AND Id NOT IN @reconciliationsToIgnore";
            }

            object param = hasIgnores
                ? new { accountId, date = date.ToString("yyyy-MM-dd"), reconciliationsToIgnore }
                : new { accountId, date = date.ToString("yyyy-MM-dd") };

            var reconciliations = (await conn.QueryAsync<int>(sql, param, tx)).ToList();

            return reconciliations;
        }
        finally {
            if (isLocalConn) {
                await conn.DisposeAsync();
            }
        }
    }

    private async Task DeleteAccountReconciliationAsync(int id, IDbTransaction? tx = null) {
        var conn = tx?.Connection ?? _db.GetConnection();

        // First, clear any transaction references to this reconciliation
        await conn.ExecuteAsync(@"
            UPDATE Transactions
            SET ReconciliationId = NULL
            WHERE ReconciliationId = @id", new { id }, tx);

        // Then delete the reconciliation
        await conn.ExecuteAsync("DELETE FROM AccountReconciliations WHERE Id = @id", new { id }, tx);
    }

    /// <summary>
    /// Updates IsCleared and ReconciliationId for a batch of transactions inside a single 
    /// connection and transaction context. Optimized specifically for the reconciliation workflow.
    /// </summary>
    public async Task<bool> UpdateTransactionsForReconciliationAsync(IEnumerable<Transaction> transactions) {
        var txList = transactions.ToList();
        if (!txList.Any()) return true;

        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        try {
            foreach (var transaction in txList) {
                // 1. Check if the From-side was previously reconciled with a DIFFERENT reconciliation ID
                if (transaction.AccountId.HasValue) {
                    var oldRows = (await conn.QueryAsync<dynamic>(@"
                    SELECT AccountId, TransactionDate 
                    FROM Transactions 
                    WHERE AccountId = @AccountId 
                      AND TransactionId = @TransactionId 
                      AND ReconciliationId IS NOT NULL 
                      AND ReconciliationId <> @ReconciliationId",
                        new {
                            AccountId = transaction.AccountId,
                            TransactionId = transaction.TransactionId.ToString(),
                            ReconciliationId = transaction.FromAccountReconciledId
                        }, tx)).ToList();

                    if (oldRows.Any()) {
                        // Invalidate downstream reconciliations if swapping reconciliation IDs
                        await InvalidateReconciliationsAfterDateAsync(
                            transaction.AccountId.Value,
                            transaction.TransactionDate,
                            tx: tx);

                        if (transaction.FromAccountReconciledId.HasValue) {
                            transaction.FromAccountReconciledId = null;
                        }
                    }
                }

                // 2. Check if the To-side was previously reconciled with a DIFFERENT reconciliation ID
                if (transaction.ToAccountId.HasValue) {
                    var oldRows = (await conn.QueryAsync<dynamic>(@"
                    SELECT AccountId, TransactionDate 
                    FROM Transactions 
                    WHERE AccountId = @AccountId 
                      AND TransactionId = @TransactionId 
                      AND ReconciliationId IS NOT NULL 
                      AND ReconciliationId <> @ReconciliationId",
                        new {
                            AccountId = transaction.ToAccountId,
                            TransactionId = transaction.TransactionId.ToString(),
                            ReconciliationId = transaction.ToAccountReconciledId
                        }, tx)).ToList();

                    if (oldRows.Any()) {
                        await InvalidateReconciliationsAfterDateAsync(
                            transaction.ToAccountId.Value,
                            transaction.TransactionDate,
                            tx: tx);

                        if (transaction.ToAccountReconciledId.HasValue) {
                            transaction.ToAccountReconciledId = null;
                        }
                    }
                }

                // 3. Execute the targeted update for the From-side
                if (transaction.AccountId.HasValue) {
                    await conn.ExecuteAsync(@"
                    UPDATE Transactions 
                    SET ReconciliationId = @ReconciliationId, 
                        IsCleared = @IsCleared 
                    WHERE AccountId = @AccountId 
                      AND TransactionId = @TransactionId",
                        new {
                            AccountId = transaction.AccountId,
                            ReconciliationId = transaction.FromAccountReconciledId,
                            TransactionId = transaction.TransactionId.ToString(),
                            IsCleared = (transaction.FromAccountIsCleared?? false) ? 1 : 0
                        }, tx);
                }

                // 4. Execute the targeted update for the To-side
                if (transaction.ToAccountId.HasValue) {
                    await conn.ExecuteAsync(@"
                    UPDATE Transactions 
                    SET ReconciliationId = @ReconciliationId, 
                        IsCleared = @IsCleared 
                    WHERE AccountId = @AccountId 
                      AND TransactionId = @TransactionId",
                        new {
                            AccountId = transaction.ToAccountId,
                            ReconciliationId = transaction.ToAccountReconciledId,
                            TransactionId = transaction.TransactionId.ToString(),
                            IsCleared = (transaction.ToAccountIsCleared?? false) ? 1 : 0
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
}