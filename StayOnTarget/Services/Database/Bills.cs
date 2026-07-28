using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    public async Task<IEnumerable<Bill>> GetAllBillsAsync()
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<Bill>("SELECT * FROM Bills WHERE IsActive = 1");
    }

    public async Task UpsertBillAsync(Bill bill)
    {
        await using var conn = _db.GetConnection();
        var param = new
        {
            bill.Id,
            bill.Name,
            bill.ExpectedAmount,
            bill.Frequency,
            bill.DueDay,
            bill.AccountId,
            bill.ToAccountId,
            NextDueDate = bill.NextDueDate?.ToString("yyyy-MM-dd"),
            bill.Category,
            bill.IsActive,
            bill.IsPrincipalOnly
        };
        if (bill.Id == 0)
        {
            await conn.ExecuteAsync(@"INSERT INTO Bills (Name, ExpectedAmount, Frequency, DueDay, AccountId, ToAccountId, NextDueDate, Category, IsActive, IsPrincipalOnly) 
                           VALUES (@Name, @ExpectedAmount, @Frequency, @DueDay, @AccountId, @ToAccountId, @NextDueDate, @Category, @IsActive, @IsPrincipalOnly)", param);
        }
        else
        {
            await conn.ExecuteAsync(@"UPDATE Bills SET Name=@Name, ExpectedAmount=@ExpectedAmount, Frequency=@Frequency, 
                           DueDay=@DueDay, AccountId=@AccountId, ToAccountId=@ToAccountId, NextDueDate=@NextDueDate, Category=@Category, IsActive=@IsActive, IsPrincipalOnly=@IsPrincipalOnly WHERE Id=@Id", param);
        }
    }   
    
    public async Task DeleteBillAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync("UPDATE Transactions SET BillId=null WHERE BillId = @id", new { id }); //Disassociate the transaction from the bill
        await conn.ExecuteAsync("DELETE FROM PeriodBills WHERE BillId = @id", new { id });
        await conn.ExecuteAsync("UPDATE Bills SET IsActive = 0 WHERE Id = @id", new { id });
    }
}