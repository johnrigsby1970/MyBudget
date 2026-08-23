using Dapper;
using StayOnTarget.Data;
using StayOnTarget.Models;
using StayOnTarget.Services;
using System.IO;

namespace StayOnTarget.Tests;

[TestClass]
public class UpsertTransactionSafetyTests
{
    private const string DbPath = @"C:\Users\JohnRigsby\AppData\Local\StayOnTarget\test.db";
    private const string DbPassword = "StayOnTargetWantsToKeepYouSafe";

    private BudgetService _service = null!;
    private DatabaseContext _dbCtx = null!;

    private const string OriginalDbPath = @"C:\Users\JohnRigsby\AppData\Local\StayOnTarget\original.db";

    [TestInitialize]
    public void Setup()
    {
        // Reset test.db to a known-good baseline before every run.
        // Pooling=false means no SQLite connections are ever held open between tests,
        // so a direct binary copy is safe here.
        Assert.IsTrue(File.Exists(OriginalDbPath),
            $"Baseline database not found at {OriginalDbPath}. " +
            "Copy your clean database there before running these tests.");
        File.Copy(OriginalDbPath, DbPath, overwrite: true);

        _service = new BudgetService(DbPath, DbPassword);
        _dbCtx = new DatabaseContext(DbPath, DbPassword);
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private async Task<List<dynamic>> GetRawRowsAsync()
    {
        await using var conn = _dbCtx.GetConnection();
        return (await conn.QueryAsync<dynamic>("SELECT * FROM Transactions ORDER BY Id")).ToList();
    }

    // Snapshot the structural/financial fields used in the final raw comparison.
    //
    // Intentionally excluded:
    //   ReconciliationId — legitimately nulled when reconciliations are invalidated.
    //   Description/Memo/FitId — validated in-flight; also legitimately normalised
    //     when the two rows of a transfer have pre-existing inconsistent values
    //     (the upsert writes the outbound side's value to both rows).
    //   TransactionDate — the upsert always writes yyyy-MM-dd, stripping the time
    //     component that some rows carry in the original DB; also normalised for
    //     inconsistent transfer pairs.
    private static (long Id, string TxId, int AccountId, decimal Amount,
        int? BillId, int? BucketId, int? PaycheckId, int IsPrincipal, int IsInterest)
        Snapshot(dynamic r) =>
        (
            (long)r.Id,
            (string)r.TransactionId,
            (int)r.AccountId,
            (decimal)r.Amount,
            r.BillId is null ? (int?)null : (int)r.BillId,
            r.BucketId is null ? (int?)null : (int)r.BucketId,
            r.PaycheckId is null ? (int?)null : (int)r.PaycheckId,
            r.IsPrincipalOnly is null ? 0 : (int)r.IsPrincipalOnly,
            r.IsInterestOnly is null ? 0 : (int)r.IsInterestOnly
        );

    private static Transaction Clone(Transaction src) => new()
    {
        TransactionId = src.TransactionId,
        FromRecordId = src.FromRecordId,
        ToRecordId = src.ToRecordId,
        Description = src.Description,
        NormalizedDescription = src.NormalizedDescription,
        Memo = src.Memo,
        Amount = src.Amount,
        TransactionDate = src.TransactionDate,
        PeriodDate = src.PeriodDate,
        AccountId = src.AccountId,
        ToAccountId = src.ToAccountId,
        BillId = src.BillId,
        BucketId = src.BucketId,
        PaycheckId = src.PaycheckId,
        PaycheckOccurrenceDate = src.PaycheckOccurrenceDate,
        ToFitId = src.ToFitId,
        FromFitId = src.FromFitId,
        IsPrincipalOnly = src.IsPrincipalOnly,
        IsInterestOnly = src.IsInterestOnly,
        FromAccountReconciliationId = src.FromAccountReconciliationId,
        ToAccountReconciliationId = src.ToAccountReconciliationId,
        FromAccountIsCleared = src.FromAccountIsCleared,
        ToAccountIsCleared = src.ToAccountIsCleared,
        AccountName = src.AccountName,
        ToAccountName = src.ToAccountName,
        BillName = src.BillName,
        BucketName = src.BucketName,
    };

    // ---------------------------------------------------------------------------
    // Test
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Exercises UpsertTransactionAsync against the live test database with a
    /// full change/verify/change/verify/restore/compare cycle across all
    /// non-interest-only transactions (up to 30).
    ///
    /// Assertion invariants:
    ///   1. Raw row count never changes during any phase.
    ///   2. Every transaction is still retrievable by TransactionId after each upsert.
    ///   3. Field changes are visible on the next read.
    ///   4. After a full restore, every raw row's key fields match the original snapshot.
    /// </summary>
    [TestMethod]
    public async Task UpsertTransaction_MultiRoundChanges_PreservesCountAndData()
    {
        // ── 0. Capture baseline ──────────────────────────────────────────────────
        var baselineRaw = await GetRawRowsAsync();
        var baselineSnaps = baselineRaw.Select(Snapshot).ToDictionary(s => s.Id);
        int expectedCount = baselineRaw.Count;

        var allTx = (await _service.GetAllTransactionsAsync()).ToList();

        // Target: up to 30 non-interest-only transactions; try to get a mix of
        // standalone expenses, standalone deposits, and transfers (both accounts set).
        var standaloneExpenses = allTx.Where(t => !t.IsInterestOnly && t.AccountId.HasValue && !t.ToAccountId.HasValue).Take(10).ToList();
        var standaloneDeposits = allTx.Where(t => !t.IsInterestOnly && !t.AccountId.HasValue && t.ToAccountId.HasValue).Take(10).ToList();
        var transfers = allTx.Where(t => !t.IsInterestOnly && t.AccountId.HasValue && t.ToAccountId.HasValue).Take(10).ToList();

        var targets = standaloneExpenses.Concat(standaloneDeposits).Concat(transfers).ToList();

        Assert.IsTrue(targets.Count > 0,
            "No non-interest-only transactions found in test.db — cannot run the test.");

        Console.WriteLine(
            $"Testing {targets.Count} transactions: " +
            $"{standaloneExpenses.Count} expense, {standaloneDeposits.Count} deposit, {transfers.Count} transfer.");

        // Preserve originals for final restore
        var originals = targets.Select(Clone).ToList();

        try
        {
            // ── ROUND 1: Change description, memo, and FitId (no reconciliation impact) ──

            foreach (var t in targets)
            {
                t.Description = "[R1] " + t.Description;
                t.Memo = "R1_MEMO_" + t.TransactionId.ToString("N")[..6];
                t.ToFitId = "R1_TOFIT_" + t.TransactionId.ToString("N")[..8];
                t.FromFitId = "R1_FROMFIT_" + t.TransactionId.ToString("N")[..8];
            }

            foreach (var t in targets)
            {
                bool ok = await _service.UpsertTransactionAsync(t, showConfirmationOfImpactToExistingReconciliations: false);
                Assert.IsTrue(ok, $"Round-1 upsert failed for TransactionId={t.TransactionId}");
            }

            var countAfterR1 = (await GetRawRowsAsync()).Count;
            Assert.AreEqual(expectedCount, countAfterR1,
                $"Row count changed after Round-1 (description/memo/fitid). " +
                $"Expected={expectedCount}, Got={countAfterR1}. " +
                "This typically means an UPDATE used an INSERT path (e.g. missing ToRecordId on transfer).");

            var txAfterR1 = (await _service.GetAllTransactionsAsync()).ToList();

            foreach (var orig in originals)
            {
                var found = txAfterR1.FirstOrDefault(t => t.TransactionId == orig.TransactionId);
                Assert.IsNotNull(found,
                    $"Transaction {orig.TransactionId} not found after Round-1. " +
                    "Record may have been buried by duplicate inserts causing group-count > 2.");
                Assert.IsTrue(found.Description.StartsWith("[R1] "),
                    $"Round-1 description change not visible for {orig.TransactionId}. Got: '{found.Description}'");
            }

            // ── ROUND 2: Change Amount (+1.00) — the reported bug trigger ──────────

            // Work from the freshly-read set so record IDs are current.
            var r2Targets = txAfterR1
                .Where(t => originals.Any(o => o.TransactionId == t.TransactionId))
                .Select(Clone) // clone so we can freely mutate
                .ToList();

            foreach (var t in r2Targets)
            {
                // Use a distinctive delta that's easy to verify.
                t.Amount = t.Amount + 1.00m;
                t.Description = "[R2] " + t.Description;
            }

            foreach (var t in r2Targets)
            {
                bool ok = await _service.UpsertTransactionAsync(t, showConfirmationOfImpactToExistingReconciliations: false);
                Assert.IsTrue(ok, $"Round-2 (amount) upsert failed for TransactionId={t.TransactionId}");
            }

            var countAfterR2 = (await GetRawRowsAsync()).Count;
            Assert.AreEqual(expectedCount, countAfterR2,
                $"Row count changed after Round-2 (amount change). " +
                $"Expected={expectedCount}, Got={countAfterR2}.");

            var txAfterR2 = (await _service.GetAllTransactionsAsync()).ToList();

            foreach (var orig in originals)
            {
                var found = txAfterR2.FirstOrDefault(t => t.TransactionId == orig.TransactionId);
                Assert.IsNotNull(found,
                    $"Transaction {orig.TransactionId} NOT FOUND after amount change — THIS IS THE REPORTED BUG. " +
                    "Modifying Amount caused the transaction to appear deleted.");
                Assert.AreEqual(orig.Amount + 1.00m, found.Amount, 0.005m,
                    $"Amount not updated correctly for {orig.TransactionId}. " +
                    $"Expected {orig.Amount + 1.00m:F2}, Got {found.Amount:F2}");
                Assert.IsTrue(found.Description.StartsWith("[R2] "),
                    $"Round-2 description change not visible for {orig.TransactionId}");
            }

            // ── ROUND 3: Change Amount again (–2.50) and TransactionDate (+1 day) ──

            var r3Targets = txAfterR2
                .Where(t => originals.Any(o => o.TransactionId == t.TransactionId))
                .Select(Clone)
                .ToList();

            foreach (var t in r3Targets)
            {
                t.Amount = Math.Max(0.01m, t.Amount - 2.50m); // keep positive
                t.TransactionDate = t.TransactionDate.AddDays(1);
                t.Description = "[R3] " + t.Description;
                t.Memo = "R3_MEMO";
            }

            foreach (var t in r3Targets)
            {
                bool ok = await _service.UpsertTransactionAsync(t, showConfirmationOfImpactToExistingReconciliations: false);
                Assert.IsTrue(ok, $"Round-3 (amount+date) upsert failed for TransactionId={t.TransactionId}");
            }

            var countAfterR3 = (await GetRawRowsAsync()).Count;
            Assert.AreEqual(expectedCount, countAfterR3,
                $"Row count changed after Round-3 (amount+date change). " +
                $"Expected={expectedCount}, Got={countAfterR3}.");

            var txAfterR3 = (await _service.GetAllTransactionsAsync()).ToList();

            foreach (var orig in originals)
            {
                var found = txAfterR3.FirstOrDefault(t => t.TransactionId == orig.TransactionId);
                Assert.IsNotNull(found,
                    $"Transaction {orig.TransactionId} not found after Round-3 (amount+date change).");
                Assert.IsTrue(found.Description.StartsWith("[R3] "),
                    $"Round-3 description not visible for {orig.TransactionId}");
            }

            // ── RESTORE: Back to original values ─────────────────────────────────

            // Refresh record IDs from the latest read so we hit UPDATE not INSERT.
            foreach (var orig in originals)
            {
                var current = txAfterR3.FirstOrDefault(t => t.TransactionId == orig.TransactionId);
                if (current != null)
                {
                    orig.FromRecordId = current.FromRecordId;
                    orig.ToRecordId = current.ToRecordId;
                }
            }

            foreach (var orig in originals)
            {
                bool ok = await _service.UpsertTransactionAsync(orig, showConfirmationOfImpactToExistingReconciliations: false);
                Assert.IsTrue(ok, $"Restore upsert failed for TransactionId={orig.TransactionId}");
            }

            var countAfterRestore = (await GetRawRowsAsync()).Count;
            Assert.AreEqual(expectedCount, countAfterRestore,
                $"Row count changed after restore. Expected={expectedCount}, Got={countAfterRestore}.");

            var txAfterRestore = (await _service.GetAllTransactionsAsync()).ToList();

            foreach (var orig in originals)
            {
                var found = txAfterRestore.FirstOrDefault(t => t.TransactionId == orig.TransactionId);
                Assert.IsNotNull(found,
                    $"Transaction {orig.TransactionId} not found after restore.");
                Assert.AreEqual(orig.Amount, found.Amount, 0.005m,
                    $"Amount not restored for {orig.TransactionId}. Expected {orig.Amount}, Got {found.Amount}");
                Assert.AreEqual(orig.TransactionDate.Date, found.TransactionDate.Date,
                    $"TransactionDate not restored for {orig.TransactionId}.");
                Assert.AreEqual(orig.AccountId, found.AccountId,
                    $"AccountId changed after restore for {orig.TransactionId}.");
                Assert.AreEqual(orig.ToAccountId, found.ToAccountId,
                    $"ToAccountId changed after restore for {orig.TransactionId}.");
            }

            // ── FINAL RAW COMPARISON ─────────────────────────────────────────────
            // Every row that existed at baseline must still exist.
            // We compare structural/financial fields only — see Snapshot() for
            // the rationale behind each exclusion.

            var finalRaw = await GetRawRowsAsync();
            Assert.AreEqual(expectedCount, finalRaw.Count,
                $"Final raw count mismatch. Expected={expectedCount}, Final={finalRaw.Count}.");

            var finalById = finalRaw.Select(Snapshot).ToDictionary(s => s.Id);

            var mismatchMessages = new List<string>();

            foreach (var (id, orig) in baselineSnaps)
            {
                if (!finalById.TryGetValue(id, out var final))
                {
                    mismatchMessages.Add($"Row Id={id} missing from final snapshot.");
                    continue;
                }

                if (orig.TxId != final.TxId)
                    mismatchMessages.Add($"Id={id}: TransactionId changed '{orig.TxId}' → '{final.TxId}'");
                if (orig.AccountId != final.AccountId)
                    mismatchMessages.Add($"Id={id}: AccountId changed {orig.AccountId} → {final.AccountId}");
                if (Math.Abs(orig.Amount - final.Amount) > 0.005m)
                    mismatchMessages.Add($"Id={id}: Amount {orig.Amount:F4} → {final.Amount:F4}");
                if (orig.BillId != final.BillId)
                    mismatchMessages.Add($"Id={id}: BillId {orig.BillId} → {final.BillId}");
                if (orig.BucketId != final.BucketId)
                    mismatchMessages.Add($"Id={id}: BucketId {orig.BucketId} → {final.BucketId}");
                if (orig.PaycheckId != final.PaycheckId)
                    mismatchMessages.Add($"Id={id}: PaycheckId {orig.PaycheckId} → {final.PaycheckId}");
            }

            if (mismatchMessages.Any())
            {
                Assert.Fail(
                    $"Final raw snapshot differs from baseline in {mismatchMessages.Count} field(s):\n" +
                    string.Join("\n", mismatchMessages));
            }
        }
        catch
        {
            // Best-effort emergency restore so the test DB isn't left dirty.
            Console.WriteLine("Test failed — attempting emergency restore of modified transactions.");
            foreach (var orig in originals)
            {
                try { await _service.UpsertTransactionAsync(orig, false); }
                catch (Exception ex) { Console.WriteLine($"  Emergency restore failed for {orig.TransactionId}: {ex.Message}"); }
            }
            throw;
        }
    }
}
