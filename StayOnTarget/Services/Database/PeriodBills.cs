using Dapper;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService{
    public async Task<IEnumerable<PeriodBill>> GetPeriodBillsAsync(DateTime periodDate)
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<PeriodBill>(@"
            SELECT pb.*, b.Name as BillName 
            FROM PeriodBills pb 
            JOIN Bills b ON pb.BillId = b.Id 
            WHERE pb.PeriodDate = @periodDate", new { periodDate = periodDate.ToString("yyyy-MM-dd") });
    }

    public async Task<IEnumerable<PeriodBill>> GetAllPeriodBillsAsync()
    {
        await using var conn = _db.GetConnection();
        return await conn.QueryAsync<PeriodBill>(@"
            SELECT pb.*, b.Name as BillName 
            FROM PeriodBills pb 
            JOIN Bills b ON pb.BillId = b.Id");
    }

    public async Task UpsertPeriodBillAsync(PeriodBill pb)
    {
        await using var conn = _db.GetConnection();
        var param = new
        {
            pb.Id,
            pb.BillId,
            PeriodDate = pb.PeriodDate.ToString("yyyy-MM-dd"),
            DueDate = pb.DueDate.ToString("yyyy-MM-dd"),
            pb.ActualAmount,
            pb.IsPaid,
            FitId = pb.FitId.ToString()
        };
        if (pb.Id == 0)
        {
            await conn.ExecuteAsync(@"INSERT INTO PeriodBills (BillId, PeriodDate, DueDate, ActualAmount, IsPaid, FitId) 
                           VALUES (@BillId, @PeriodDate, @DueDate, @ActualAmount, @IsPaid, @FitId)", param);
        }
        else
        {
            await conn.ExecuteAsync(@"UPDATE PeriodBills SET BillId=@BillId, PeriodDate=@PeriodDate, DueDate=@DueDate, 
                           ActualAmount=@ActualAmount, IsPaid=@IsPaid WHERE Id=@Id", param);
        }
    }
    
    public async Task DeletePeriodBillAsync(int id)
    {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync("DELETE FROM PeriodBills WHERE Id = @id AND IsPaid = 0", new { id });
    }

}