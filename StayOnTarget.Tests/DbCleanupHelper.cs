using Dapper;
using StayOnTarget.Data;

namespace StayOnTarget.Tests;

/// <summary>
/// One-shot cleanup for the test DB after the UpsertTransactionSafetyTests
/// exposed the missing ToRecordId bug and left duplicate inbound rows.
/// Run this ONCE to restore the database, then delete or ignore it.
/// </summary>
[TestClass]
public class DbCleanupHelper
{
    private const string DbPath = @"C:\Users\JohnRigsby\AppData\Local\StayOnTarget\test.db";
    private const string DbPassword = "StayOnTargetWantsToKeepYouSafe";

    [TestMethod]
    public async Task CleanupDuplicateInboundRows()
    {
        var dbCtx = new DatabaseContext(DbPath, DbPassword);
        await using var conn = dbCtx.GetConnection();
        await conn.OpenAsync();

        // Find TransactionId groups with more than 2 rows (the expected max for a transfer)
        var groups = (await conn.QueryAsync<dynamic>(@"
            SELECT TransactionId, COUNT(*) as cnt
            FROM Transactions
            GROUP BY TransactionId
            HAVING COUNT(*) > 2")).ToList();

        Console.WriteLine($"Found {groups.Count} TransactionId groups with more than 2 rows.");

        int totalDeleted = 0;

        await using var tx = conn.BeginTransaction();
        try
        {
            foreach (var g in groups)
            {
                string txId = (string)g.TransactionId;
                int count = (int)g.cnt;

                // Get all rows for this TransactionId, ordered by Id ascending
                var rows = (await conn.QueryAsync<dynamic>(
                    "SELECT Id, Amount FROM Transactions WHERE TransactionId = @txId ORDER BY Id ASC",
                    new { txId }, tx)).ToList();

                // The original pair is always the two rows with the lowest Ids.
                // Delete everything beyond the first 2.
                var toDelete = rows.Skip(2).ToList();
                foreach (var row in toDelete)
                {
                    await conn.ExecuteAsync("DELETE FROM Transactions WHERE Id = @id", new { id = (long)row.Id }, tx);
                    totalDeleted++;
                }

                Console.WriteLine($"  TransactionId={txId}: had {count} rows, deleted {toDelete.Count} excess.");
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        Console.WriteLine($"Cleanup complete. Deleted {totalDeleted} duplicate rows.");

        // Also clean up any "[R1]" or "R1_MEMO_" artifacts left by the failed test
        await using var conn2 = dbCtx.GetConnection();
        var dirtyDescriptions = (await conn2.QueryAsync<dynamic>(
            "SELECT Id, Description, Memo, FitId FROM Transactions WHERE Description LIKE '[R1]%' OR Description LIKE '[R2]%' OR Description LIKE '[R3]%'")).ToList();

        Console.WriteLine($"\nFound {dirtyDescriptions.Count} rows with modified descriptions from the test run.");
        foreach (var row in dirtyDescriptions)
        {
            Console.WriteLine($"  Id={row.Id}: '{row.Description}' | Memo='{row.Memo}'");
        }

        // Verify final count
        var finalCount = await conn2.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Transactions");
        Console.WriteLine($"\nFinal row count: {finalCount}");
    }
}
