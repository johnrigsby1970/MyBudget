using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    public async Task<IEnumerable<Paycheck>> GetAllPaychecksAsync()
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<Paycheck>("SELECT * FROM Paychecks");
    }
    
    public  async Task UpsertPaycheckAsync(Paycheck paycheck)
    {
        await using var conn = _db.GetConnection();
        var param = new
        {
            paycheck.Id,
            paycheck.Name,
            paycheck.ExpectedAmount,
            paycheck.Frequency,
            StartDate = paycheck.StartDate.ToString("yyyy-MM-dd"),
            EndDate = paycheck.EndDate?.ToString("yyyy-MM-dd"),
            paycheck.AccountId,
            paycheck.IsBalanced
        };
        if (paycheck.Id == 0)
        {
            await conn.ExecuteAsync(@"INSERT INTO Paychecks (Name, ExpectedAmount, Frequency, StartDate, EndDate, AccountId, IsBalanced) 
                           VALUES (@Name, @ExpectedAmount, @Frequency, @StartDate, @EndDate, @AccountId, @IsBalanced)", param);
        }
        else
        {
            await conn.ExecuteAsync(@"UPDATE Paychecks SET Name=@Name, ExpectedAmount=@ExpectedAmount, Frequency=@Frequency, 
                           StartDate=@StartDate, EndDate=@EndDate, AccountId=@AccountId, IsBalanced=@IsBalanced WHERE Id=@Id", param);
        }
    }  
    
    public async Task DeletePaycheckAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync("UPDATE Transactions SET PaycheckId=null WHERE PaycheckId = @id", new { id }); //Disassociate the transaction from the paycheck
        await conn.ExecuteAsync("DELETE FROM Paychecks WHERE Id = @id", new { id });
    }
}