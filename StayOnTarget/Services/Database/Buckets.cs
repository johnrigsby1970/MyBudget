using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    // Bucket Operations
    public async Task<IEnumerable<BudgetBucket>> GetAllBucketsAsync()
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<BudgetBucket>("SELECT * FROM Buckets");
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

    public async Task DeleteBucketAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync("UPDATE Transactions SET BucketId=null WHERE BucketId = @id", new { id }); //Disassociate the transaction from the bucket
        await conn.ExecuteAsync("DELETE FROM PeriodBuckets WHERE BucketId = @id", new { id });
        await conn.ExecuteAsync("DELETE FROM Buckets WHERE Id = @id", new { id });
    }  
}