using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    public async Task<IEnumerable<Bill>> GetAllBillsAsync(bool includeArchived = false)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        return await conn.QueryAsync<Bill>("SELECT * FROM Bills WHERE IsActive = 1 AND (IsArchived=0 OR IsArchived = @includeArchived)", new { includeArchived=(includeArchived ? 1: 0) });
    }

    public async Task UpsertBillAsync(Bill bill)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
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
    
    public async Task ArchiveBillAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Bills SET IsArchived=1 WHERE Id=@id", new { id });
    }
    
    public async Task UnArchiveBillAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Bills SET IsArchived=0 WHERE Id=@id", new { id });
    }
    
    public async Task SetBillInactiveAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE Bills SET IsActive = 0 WHERE Id = @id", new { id });
    }
    
    public async Task DeleteBillAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        
        try {
        if (await IsBillInUseAsync(id)) {
            await conn.ExecuteAsync("UPDATE Bills SET IsArchived = 1 WHERE Id = @id", new { id });
        }
        else {
            await conn.ExecuteAsync("DELETE FROM Bills WHERE Id = @id", new { id });
        }
        await tx.CommitAsync();
        }
        catch {
            await tx.RollbackAsync();
            throw;
        }
    }
    
    public async Task<bool> IsBillInUseAsync(int billId, SqliteConnection? cn = null, IDbTransaction? tx = null)
    {
        cn ??= tx?.Connection as SqliteConnection;
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try {
            if (isLocalConn && conn.State != ConnectionState.Open) {
                await conn.OpenAsync();
            }
        
        // Check Bills (AccountId or ToAccountId)
        var periodBills = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM PeriodBills WHERE BillId = @billId", 
            new { billId });
        if (periodBills > 0) return true;
        
        
        // Check Transactions (AccountId or ToAccountId)
        var transactions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE BillId = @billId", 
            new { billId });
        if (transactions > 0) return true;
        
        return false;
        }
        finally {
            if (isLocalConn) {
                await conn.DisposeAsync();
            }
        }
    }
}