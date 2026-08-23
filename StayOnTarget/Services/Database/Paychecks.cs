using Dapper;
using StayOnTarget.Models;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    public async Task<IEnumerable<Paycheck>> GetAllPaychecksAsync()
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            return await conn.QueryAsync<Paycheck>("SELECT * FROM Paychecks ORDER BY Name");
        }
        catch (Exception ex) {
            Log.Error(ex, "Error getting all paychecks[cite: 23].");
            return Enumerable.Empty<Paycheck>();
        }
    }
    
    public async Task UpsertPaycheckAsync(Paycheck paycheck)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();

            var param = new
            {
                paycheck.Id,
                paycheck.Name,
                paycheck.ExpectedAmount,
                paycheck.Frequency,
                StartDate = paycheck.StartDate.ToString("yyyy-MM-dd"),
                EndDate = paycheck.EndDate?.ToString("yyyy-MM-dd"),
                paycheck.AccountId,
                IsBalanced = paycheck.IsBalanced ? 1 : 0
            };

            if (paycheck.Id == 0)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Paychecks (Name, ExpectedAmount, Frequency, StartDate, EndDate, AccountId, IsBalanced) 
                    VALUES (@Name, @ExpectedAmount, @Frequency, @StartDate, @EndDate, @AccountId, @IsBalanced)", param);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE Paychecks 
                    SET Name = @Name, 
                        ExpectedAmount = @ExpectedAmount, 
                        Frequency = @Frequency, 
                        StartDate = @StartDate, 
                        EndDate = @EndDate, 
                        AccountId = @AccountId, 
                        IsBalanced = @IsBalanced 
                    WHERE Id = @Id", param);
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error upserting paycheck with ID {PaycheckId}[cite: 23].", paycheck.Id);
            throw;
        }
    }  
    
    public async Task DeletePaycheckAsync(int id)
    {
        try {
            await using var conn = _db.GetConnection();
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();

            try
            {
                await conn.ExecuteAsync("UPDATE Transactions SET PaycheckId = NULL WHERE PaycheckId = @id", new { id }, tx);
                
                await conn.ExecuteAsync("DELETE FROM Paychecks WHERE Id = @id", new { id }, tx);

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex) {
            Log.Error(ex, "Error deleting paycheck with ID {PaycheckId}[cite: 23].", id);
            throw;
        }
    }
}