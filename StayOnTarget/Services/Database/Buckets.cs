using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // Bucket Operations
    public async Task<IEnumerable<BudgetBucket>> GetAllBucketsAsync(bool includeArchived = false)
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<BudgetBucket>("SELECT * FROM Buckets WHERE IsActive = 1 AND (IsArchived=0 OR IsArchived = @includeArchived)", new { includeArchived=(includeArchived ? 1: 0) });
    }

    public async Task UpsertBucketAsync(BudgetBucket bucket)
    {
        using var conn = _db.GetConnection();
        if (bucket.Id == 0)
        {
            await conn.ExecuteAsync(@"INSERT INTO Buckets (Name, ExpectedAmount, AccountId, PaycheckId) 
                           VALUES (@Name, @ExpectedAmount, @AccountId, @PaycheckId)", bucket);
        }
        else
        {
            await conn.ExecuteAsync(@"UPDATE Buckets SET Name=@Name, ExpectedAmount=@ExpectedAmount, AccountId=@AccountId, PaycheckId=@PaycheckId WHERE Id=@Id", bucket);
        }
    }
    
    public async Task ArchiveBucketAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"UPDATE Buckets SET IsArchived=1 WHERE Id=@id", new { id });
    }
    
    public async Task UnArchiveBucketAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync(@"UPDATE Buckets SET IsArchived=0 WHERE Id=@id", new { id });
    }

    
    public async Task SetBucketInactiveAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync("UPDATE Buckets SET IsActive = 0 WHERE Id = @id", new { id });
    }
    
    public async Task DeleteBucketAsync(int id)
    {
        if (await IsBucketInUseAsync(id)) {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync("UPDATE Buckets SET IsArchived = 1 WHERE Id = @id", new { id });
        }
        else {
            await using var conn = _db.GetConnection();
            await conn.ExecuteAsync("DELETE FROM Buckets WHERE Id = @id", new { id });
        }
    }
    
    public async Task<bool> IsBucketInUseAsync(int bucketId)
    {
        await using var conn = _db.GetConnection();
        
        // Check Bills (AccountId or ToAccountId)
        var periodBuckets = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM PeriodBuckets WHERE BucketId = @bucketId", 
            new { bucketId });
        if (periodBuckets > 0) return true;
        
        
        // Check Transactions (AccountId or ToAccountId)
        var transactions = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM Transactions WHERE BucketId = @bucketId", 
            new { bucketId });
        if (transactions > 0) return true;
        
        return false;
    }
}