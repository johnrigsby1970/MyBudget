using System.Data;
using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<AccountReconciliation>> GetAllAccountReconciliationsAsync() {
        await using var conn = _db.GetConnection();
        var reconciliations = (await conn.QueryAsync<AccountReconciliation>("SELECT * FROM AccountReconciliations")).ToList();

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

    private async Task InvalidateReconciliationsAfterDateAsync(int accountId, DateTime date, IDbTransaction? tx = null) {
        foreach (var r in await GetInvalidateReconciliationsAfterDateAsync(accountId, date, tx)) {
            await DeleteAccountReconciliationAsync(r, tx);
        }
    }

    private async Task<bool> WillInvalidateReconciliationsAfterDateAsync(int accountId, DateTime date,
        List<int>? reconciliationsToIgnore = null) {
        // Ensure we have at least an empty list to avoid Dapper mapping errors
        reconciliationsToIgnore ??= new List<int>();

        await using var conn = _db.GetConnection();

        // Use a conditional or a dummy value if the list is empty
        string sql = @"
        SELECT Count(*) From AccountReconciliations
        WHERE AccountId = @accountId 
        AND ReconciledAsOfDate >= @date";

        // Only add the NOT IN clause if there are items to ignore
        if (reconciliationsToIgnore.Any()) {
            sql += " AND Id NOT IN @reconciliationsToIgnore";
        }

        var recordsImpacted = await conn.ExecuteScalarAsync<int>(sql,
            new { accountId, date = date.ToString("yyyy-MM-dd"), reconciliationsToIgnore });

        return recordsImpacted > 0;
    }

    private async Task<List<int>> GetInvalidateReconciliationsAfterDateAsync(int accountId, DateTime date, IDbTransaction? tx = null,
        List<int>? reconciliationsToIgnore = null) {
        // Ensure we have at least an empty list to avoid Dapper mapping errors
        reconciliationsToIgnore ??= new List<int>();

        var conn = tx?.Connection ?? _db.GetConnection();

        // Use a conditional or a dummy value if the list is empty
        string sql = @"
        SELECT Id From AccountReconciliations
        WHERE AccountId = @accountId 
        AND ReconciledAsOfDate >= @date";

        // Only add the NOT IN clause if there are items to ignore
        if (reconciliationsToIgnore.Any()) {
            sql += " AND Id NOT IN @reconciliationsToIgnore";
        }

        var reconciliations = (await conn.QueryAsync<int>(
            sql,
            new { accountId, date = date.ToString("yyyy-MM-dd"), reconciliationsToIgnore }, tx)).ToList();

        return reconciliations;
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
}