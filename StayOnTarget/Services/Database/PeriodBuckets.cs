using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    public async Task<IEnumerable<PeriodBucket>> GetPeriodBucketsAsync(DateTime periodDate)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<PeriodBucket>(@"
            SELECT pb.*, b.Name as BucketName 
            FROM PeriodBuckets pb 
            JOIN Buckets b ON pb.BucketId = b.Id 
            WHERE pb.PeriodDate = @periodDate", new { periodDate = periodDate.ToString("yyyy-MM-dd") });
    }
    
    public async Task<IEnumerable<PeriodBucket>> GetPeriodBucketsIncludingMonthlyAsync(DateTime periodDate)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var month = new DateTime(periodDate.Year, periodDate.Month, 1);
        return await conn.QueryAsync<PeriodBucket>(@"
            SELECT pb.*, b.Name as BucketName 
            FROM PeriodBuckets pb 
            JOIN Buckets b ON pb.BucketId = b.Id 
            WHERE pb.PeriodDate = @periodDate OR pb.PeriodDate = @month", 
            new { periodDate = periodDate.ToString("yyyy-MM-dd"), month = month.ToString("yyyy-MM-dd") });
    }

    public async Task<IEnumerable<PeriodBucket>> GetAllPeriodBucketsAsync()
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        return await conn.QueryAsync<PeriodBucket>(@"
            SELECT pb.*, b.Name as BucketName 
            FROM PeriodBuckets pb 
            JOIN Buckets b ON pb.BucketId = b.Id");
    }

    public async Task UpsertPeriodBucketAsync(PeriodBucket pb)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var param = new
        {
            pb.Id,
            pb.BucketId,
            PeriodDate = pb.PeriodDate.ToString("yyyy-MM-dd"),
            pb.ActualAmount,
            IsPaid = pb.IsPaid ? 1 : 0,
            FitId = pb.FitId.ToString()
        };

        if (pb.Id == 0)
        {
            await conn.ExecuteAsync(@"
                INSERT INTO PeriodBuckets (BucketId, PeriodDate, ActualAmount, IsPaid, FitId) 
                VALUES (@BucketId, @PeriodDate, @ActualAmount, @IsPaid, @FitId)", param);
        }
        else
        {
            await conn.ExecuteAsync(@"
                UPDATE PeriodBuckets 
                SET BucketId = @BucketId, 
                    PeriodDate = @PeriodDate, 
                    ActualAmount = @ActualAmount, 
                    IsPaid = @IsPaid,
                    FitId = @FitId 
                WHERE Id = @Id", param);
        }
    }

    public async Task DeletePeriodBucketAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        await conn.ExecuteAsync("DELETE FROM PeriodBuckets WHERE Id = @id AND IsPaid = 0", new { id });
    }
}