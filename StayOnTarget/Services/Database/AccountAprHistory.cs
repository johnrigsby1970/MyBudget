using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<AccountAprHistory>> GetAccountAprHistoriesAsync(int accountId) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<AccountAprHistory>(@"
            SELECT aah.*
            FROM AccountAprHistory aah
            WHERE aah.AccountId = @accountId
            ORDER BY aah.AsOfDate DESC", new { accountId });
    }

    public async Task UpsertAccountAprHistoryAsync(AccountAprHistory aah) {
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

    public async Task DeleteAccountAprHistoryAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync("DELETE FROM AccountAprHistory WHERE Id = @id", new { id });
    }
}