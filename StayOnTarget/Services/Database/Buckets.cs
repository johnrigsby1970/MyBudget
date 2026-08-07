using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService {
    // Bucket Operations
    public async Task<IEnumerable<BudgetBucket>> GetAllBucketsAsync(bool includeArchived = false) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<BudgetBucket>(
            "SELECT * FROM Buckets WHERE IsActive = 1 AND (IsArchived = 0 OR @includeArchived = 1) ORDER BY Name",
            new { includeArchived = includeArchived ? 1 : 0 });
    }

    public async Task UpsertBucketAsync(BudgetBucket bucket, List<int>? subCategoryIds) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
        if (bucket.Id == 0) {
            bucket.Id = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO Buckets (Name, ExpectedAmount, AccountId, PaycheckId) 
                VALUES (@Name, @ExpectedAmount, @AccountId, @PaycheckId);
                SELECT last_insert_rowid();", bucket, tx);
        }
        else {
            await conn.ExecuteAsync(@"
                UPDATE Buckets 
                SET Name = @Name, 
                    ExpectedAmount = @ExpectedAmount, 
                    AccountId = @AccountId, 
                    PaycheckId = @PaycheckId 
                WHERE Id = @Id", bucket, tx);
        }

        //if screen doesn't allow entering subcategories, possibly during onboarding
        if (subCategoryIds != null) {
            // If subcategory -> envelope is Many-to-One (BucketId column on SubCategory table)
            // 1. Clear existing assignments for this bucket
            await conn.ExecuteAsync(
                "UPDATE SubCategories SET DefaultBucketId = NULL WHERE DefaultBucketId = @BucketId",
                new { BucketId = bucket.Id }, tx);

            // 2. Assign selected subcategories to this bucket
            if (subCategoryIds.Any()) {
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

    public async Task ArchiveBucketAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Buckets SET IsArchived = 1 WHERE Id = @id", new { id });
    }

    public async Task UnArchiveBucketAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync(@"UPDATE Buckets SET IsArchived = 0 WHERE Id = @id", new { id });
    }

    public async Task SetBucketInactiveAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await conn.ExecuteAsync("UPDATE Buckets SET IsActive = 0 WHERE Id = @id", new { id });
    }

    public async Task DeleteBucketAsync(int id) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        try {
            if (await IsBucketInUseAsync(id, conn, tx)) {
                await conn.ExecuteAsync("UPDATE Buckets SET IsArchived = 1 WHERE Id = @id", new { id }, tx);
            }
            else {
                await conn.ExecuteAsync("DELETE FROM Buckets WHERE Id = @id", new { id }, tx);
            }

            await tx.CommitAsync();
        }
        catch {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> IsBucketInUseAsync(int bucketId, SqliteConnection? cn = null, IDbTransaction? tx = null) {
        cn ??= tx?.Connection as SqliteConnection;
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try {
            if (isLocalConn && conn.State != ConnectionState.Open) {
                await conn.OpenAsync();
            }

            // Check PeriodBuckets
            var periodBuckets = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM PeriodBuckets WHERE BucketId = @bucketId",
                new { bucketId }, tx);
            if (periodBuckets > 0) return true;

            // Check Transactions
            var transactions = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Transactions WHERE BucketId = @bucketId",
                new { bucketId }, tx);
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