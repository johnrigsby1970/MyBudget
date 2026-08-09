using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService 
{
    // Bucket Operations
    public async Task<IEnumerable<BudgetBucket>> GetAllBucketsAsync(bool includeArchived = false) 
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<BudgetBucket>(
            "SELECT * FROM Buckets WHERE IsActive = 1 AND (IsArchived = 0 OR @includeArchived = 1) ORDER BY Name",
            new { includeArchived = includeArchived ? 1 : 0 });
    }

    public async Task UpsertBucketAsync(BudgetBucket bucket, List<int>? subCategoryIds) 
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            if (bucket.Id == 0) 
            {
                bucket.Id = await conn.ExecuteScalarAsync<int>(@"
                    INSERT INTO Buckets (Name, ExpectedAmount, AccountId, PaycheckId, Type, TargetBalance, CurrentBalance, InitialBalance) 
                    VALUES (@Name, @ExpectedAmount, @AccountId, @PaycheckId, @Type, @TargetBalance, @CurrentBalance, @InitialBalance);
                    SELECT last_insert_rowid();", bucket, tx);
            }
            else 
            {
                await conn.ExecuteAsync(@"
                    UPDATE Buckets 
                    SET Name = @Name, 
                        ExpectedAmount = @ExpectedAmount, 
                        AccountId = @AccountId, 
                        PaycheckId = @PaycheckId,
                        Type = @Type,
                        TargetBalance = @TargetBalance,
                        CurrentBalance = @CurrentBalance,
                        InitialBalance = @InitialBalance
                    WHERE Id = @Id", bucket, tx);
            }

            // Subcategory mapping handling
            if (subCategoryIds != null) 
            {
                await conn.ExecuteAsync(
                    "UPDATE SubCategories SET DefaultBucketId = NULL WHERE DefaultBucketId = @BucketId",
                    new { BucketId = bucket.Id }, tx);

                if (subCategoryIds.Any()) 
                {
                    await conn.ExecuteAsync(
                        "UPDATE SubCategories SET DefaultBucketId = @BucketId WHERE Id IN @Ids",
                        new { BucketId = bucket.Id, Ids = subCategoryIds }, tx);
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Process a transaction against an AccumulatingDrawdown bucket, updating its CurrentBalance.
    /// </summary>
    public async Task ApplyDrawdownAsync(int bucketId, decimal amount, IDbTransaction? tx = null)
    {
        var conn = tx?.Connection ?? _db.GetConnection();
        bool isLocalConn = tx == null;

        try
        {
            // Cast to DbConnection to access OpenAsync and DisposeAsync safely
            if (isLocalConn && conn is DbConnection dbConn)
            {
                if (dbConn.State != ConnectionState.Open)
                {
                    await dbConn.OpenAsync();
                }
            }
            else if (isLocalConn && conn.State != ConnectionState.Open)
            {
                conn.Open();
            }

            await conn.ExecuteAsync(@"
            UPDATE Buckets 
            SET CurrentBalance = CurrentBalance - @amount 
            WHERE Id = @bucketId AND Type = 2", 
                new { bucketId, amount }, tx);
        }
        finally
        {
            if (isLocalConn)
            {
                if (conn is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    conn.Dispose();
                }
            }
        }
    }

    public async Task ArchiveBucketAsync(int id) 
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Buckets SET IsArchived = 1 WHERE Id = @id", new { id });
    }

    public async Task UnArchiveBucketAsync(int id) 
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Buckets SET IsArchived = 0 WHERE Id = @id", new { id });
    }

    public async Task SetBucketInactiveAsync(int id) 
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE Buckets SET IsActive = 0 WHERE Id = @id", new { id });
    }

    public async Task DeleteBucketAsync(int id) 
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        try 
        {
            if (await IsBucketInUseAsync(id, conn, tx)) 
            {
                await conn.ExecuteAsync("UPDATE Buckets SET IsArchived = 1 WHERE Id = @id", new { id }, tx);
            }
            else 
            {
                await conn.ExecuteAsync("DELETE FROM Buckets WHERE Id = @id", new { id }, tx);
            }

            await tx.CommitAsync();
        }
        catch 
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> IsBucketInUseAsync(int bucketId, SqliteConnection? cn = null, IDbTransaction? tx = null) 
    {
        cn ??= tx?.Connection as SqliteConnection;
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try 
        {
            if (isLocalConn && conn.State != ConnectionState.Open) 
            {
                await conn.OpenAsync();
            }

            var periodBuckets = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PeriodBuckets WHERE BucketId = @bucketId",
                new { bucketId }, tx);
            if (periodBuckets > 0) return true;

            var transactions = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Transactions WHERE BucketId = @bucketId",
                new { bucketId }, tx);
            if (transactions > 0) return true;

            return false;
        }
        finally 
        {
            if (isLocalConn) 
            {
                await conn.DisposeAsync();
            }
        }
    }
    
    public async Task FundPeriodBucketAsync(int bucketId, DateTime periodDate, decimal amount)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            // 1. Upsert the PeriodBucket entry as IsPaid = true
            await conn.ExecuteAsync(@"
            INSERT INTO PeriodBuckets (BucketId, PeriodDate, ActualAmount, IsPaid)
            VALUES (@bucketId, @periodDate, @amount, 1)
            ON CONFLICT(BucketId, PeriodDate) DO UPDATE SET
                ActualAmount = @amount,
                IsPaid = 1",
                new { bucketId, periodDate = periodDate.ToString("yyyy-MM-dd"), amount }, tx);

            // 2. Re-sync the master Bucket's CurrentBalance
            await RecalculateBucketBalanceAsync(bucketId, tx);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}