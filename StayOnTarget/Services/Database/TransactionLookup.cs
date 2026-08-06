using Dapper;
using StayOnTarget.Helpers;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    /// <summary>
    /// Looks back 90 days in historical transactions to find the most recent SubCategoryId
    /// assigned to a matching normalized description.
    /// </summary>
    public async Task<int?> GetSuggestedSubCategoryIdAsync(string rawDescription, DateTime? referenceDate = null)
    {
        if (string.IsNullOrWhiteSpace(rawDescription)) 
            return null;

        // Clean/normalize the input description using your existing normalizer
        string normalized = TransactionMatcher.NormalizeName(rawDescription);
        if (string.IsNullOrWhiteSpace(normalized)) 
            return null;

        // Cutoff date: 90 days prior to transaction date (or today)
        var endDate = referenceDate ?? DateTime.Today;
        var cutoffDate = endDate.AddDays(-90).ToString("yyyy-MM-dd");

        await using var conn = _db.GetConnection();

        // Query the most recent matching transaction from the past 90 days that has a valid SubCategoryId
        const string sql = @"
            SELECT SubCategoryId 
            FROM Transactions 
            WHERE NormalizedDescription = @normalized 
              AND SubCategoryId IS NOT NULL 
              AND SubCategoryId > 0
              AND TransactionDate >= @cutoffDate
            ORDER BY TransactionDate DESC, Id DESC 
            LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<int?>(sql, new { normalized, cutoffDate });
    }
}