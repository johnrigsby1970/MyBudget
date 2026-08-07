using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;
using StayOnTarget.Helpers;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    /// <summary>
    /// Looks back 90 days in historical transactions to find the most recent SubCategoryId
    /// assigned to a matching normalized description.
    /// </summary>
    public async Task<int?> GetSuggestedSubCategoryIdAsync(
        string rawDescription, 
        DateTime? referenceDate = null,
        SqliteConnection? cn = null,
        IDbTransaction? tx = null)
    {
        if (string.IsNullOrWhiteSpace(rawDescription)) 
            return null;

        // Clean/normalize the input description using your existing normalizer
        string normalized = TransactionMatcher.NormalizeName(rawDescription);
        if (string.IsNullOrWhiteSpace(normalized)) 
            return null;

        // Cutoff window: 90 days prior to referenceDate (or today)
        var endDate = referenceDate ?? DateTime.Today;
        var cutoffDateStr = endDate.AddDays(-90).ToString("yyyy-MM-dd");
        var endDateStr = endDate.ToString("yyyy-MM-dd");

        cn ??= tx?.Connection as SqliteConnection;
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try
        {
            if (isLocalConn && conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // Query the most recent matching transaction within the 90-day window that has a valid SubCategoryId
            const string sql = @"
                SELECT SubCategoryId 
                FROM Transactions 
                WHERE NormalizedDescription = @normalized 
                  AND SubCategoryId IS NOT NULL 
                  AND SubCategoryId > 0
                  AND TransactionDate >= @cutoffDateStr
                  AND TransactionDate <= @endDateStr
                ORDER BY TransactionDate DESC, Id DESC 
                LIMIT 1;";

            return await conn.QueryFirstOrDefaultAsync<int?>(
                sql, 
                new { normalized, cutoffDateStr, endDateStr }, 
                tx);
        }
        finally
        {
            if (isLocalConn)
            {
                await conn.DisposeAsync();
            }
        }
    }
}