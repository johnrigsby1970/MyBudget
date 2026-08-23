using Dapper;
using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<AccountAprHistory>> GetAccountAprHistoriesAsync(int accountId) {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            return await conn.QueryAsync<AccountAprHistory>(@"
                SELECT aah.*
                FROM AccountAprHistory aah
                WHERE aah.AccountId = @accountId
                ORDER BY aah.AsOfDate DESC", new { accountId });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting account APR histories for account ID {AccountId}[cite: 24].", accountId);
            return Enumerable.Empty<AccountAprHistory>();
        }
    }

    public async Task UpsertAccountAprHistoryAsync(AccountAprHistory aah) {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var param = new {
                aah.Id,
                aah.AccountId,
                AsOfDate = aah.AsOfDate.ToString("yyyy-MM-dd"),
                aah.AnnualPercentageRate,
                aah.CashAdvanceRate,
                aah.BalanceTransferRate
            };

            if (aah.Id == 0) {
                await conn.ExecuteAsync(@"
                    INSERT INTO AccountAprHistory (AccountId, AsOfDate, AnnualPercentageRate, CashAdvanceRate, BalanceTransferRate)
                    VALUES (@AccountId, @AsOfDate, @AnnualPercentageRate, @CashAdvanceRate, @BalanceTransferRate)",
                    param);
            }
            else {
                await conn.ExecuteAsync(@"
                    UPDATE AccountAprHistory 
                    SET AccountId = @AccountId, 
                        AsOfDate = @AsOfDate, 
                        AnnualPercentageRate = @AnnualPercentageRate,
                        CashAdvanceRate = @CashAdvanceRate, 
                        BalanceTransferRate = @BalanceTransferRate 
                    WHERE Id = @Id",
                    param);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error upserting account APR history with ID {AprHistoryId}[cite: 24].", aah.Id);
            throw;
        }
    }

    public async Task DeleteAccountAprHistoryAsync(int id) {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            await conn.ExecuteAsync("DELETE FROM AccountAprHistory WHERE Id = @id", new { id });
        }
        catch (Exception ex) {
            Log.Error(ex, "Error deleting account APR history with ID {AprHistoryId}[cite: 24].", id);
            throw;
        }
    }
}