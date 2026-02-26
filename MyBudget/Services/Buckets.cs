using Dapper;
using MyBudget.Data;
using MyBudget.Models;
using System.Collections.Generic;
using System.Linq;

namespace MyBudget.Services;

public partial class BudgetService
{
    // Bucket Operations
    public IEnumerable<BudgetBucket> GetAllBuckets()
    {
        using var conn = _db.GetConnection();
        return conn.Query<BudgetBucket>("SELECT * FROM Buckets");
    }

    public void UpsertBucket(BudgetBucket bucket)
    {
        using var conn = _db.GetConnection();
        if (bucket.Id == 0)
        {
            conn.Execute(@"INSERT INTO Buckets (Name, ExpectedAmount, AccountId, PayCheckId) 
                           VALUES (@Name, @ExpectedAmount, @AccountId, @PayCheckId)", bucket);
        }
        else
        {
            conn.Execute(@"UPDATE Buckets SET Name=@Name, ExpectedAmount=@ExpectedAmount, AccountId=@AccountId, PayCheckId=@PayCheckId WHERE Id=@Id", bucket);
        }
    }

    public void DeleteBucket(int id)
    {
        using var conn = _db.GetConnection();
        conn.Execute("UPDATE AdHocTransactions SET BucketId=null WHERE BucketId = @id", new { id }); //Disassociate the transaction from the bucket
        conn.Execute("DELETE FROM PeriodBuckets WHERE BucketId = @id", new { id });
        conn.Execute("DELETE FROM Buckets WHERE Id = @id", new { id });
    }  
}