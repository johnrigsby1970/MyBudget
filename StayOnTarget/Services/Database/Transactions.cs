using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;
using StayOnTarget.Helpers;
using StayOnTarget.Models;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<IEnumerable<Transaction>> GetTransactionsAsync(DateTime periodStart, DateTime periodEnd) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn.QueryAsync<dynamic>(@"
            SELECT t.*, a1.Name as AccountName, 
                   Bills.Name as BillName, Buckets.Name as BucketName 
            , SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id
            WHERE t.TransactionDate >= @periodStart AND t.TransactionDate <= @periodEnd",
            new {
                periodStart = periodStart.ToString("yyyy-MM-dd"),
                periodEnd = periodEnd.ToString("yyyy-MM-dd")
            })).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Transaction>> GetAllTransactionsAsync(DateTime? periodStart = null,
        DateTime? periodEnd = null) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        const string sql = @"
        SELECT t.*, 
               a1.Name as AccountName, 
               Bills.Name as BillName, 
               Buckets.Name as BucketName, 
               SubCategories.Name as SubCategoryName 
        FROM Transactions t
        LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
        LEFT JOIN Bills ON t.BillId = Bills.Id
        LEFT JOIN Buckets ON t.BucketId = Buckets.Id
        LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id
        WHERE (@periodStart IS NULL OR t.TransactionDate >= @periodStart)
          AND (@periodEnd IS NULL OR t.TransactionDate <= @periodEnd)
        ORDER BY t.TransactionDate DESC";

        var parameters = new {
            periodStart = periodStart?.ToString("yyyy-MM-dd"),
            periodEnd = periodEnd?.ToString("yyyy-MM-dd")
        };

        var dbRows = (await conn.QueryAsync<dynamic>(sql, parameters)).ToList();
        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Transaction>> GetRawTransactionsAsync() {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn.QueryAsync<dynamic>(@"
            SELECT t.*, a.Name as AccountName, b.Name as BucketName
            , SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a ON t.AccountId = a.Id
            LEFT JOIN Buckets b ON t.BucketId = b.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id")).ToList();

        return dbRows.Select(row => (Transaction)MapDynamicToTransaction(row, isTransferSide: false)).ToList();
    }

    public async Task<IEnumerable<Transaction>> GetAccountTransactionsAsync(int accountId) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn.QueryAsync<dynamic>(@"
            SELECT t.*, a1.Name as AccountName, 
                   Bills.Name as BillName, Buckets.Name as BucketName 
            , SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id  WHERE t.AccountId=@accountId",
            new { accountId })).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Ledger>> GetAccountTransactionsAsDynamicAsync(int accountId) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn
            .QueryAsync<Ledger>("SELECT * FROM Transactions WHERE AccountId=@accountId",
                new { accountId })).ToList();
        return dbRows;
    }

    public async Task<IEnumerable<Ledger>> GetUnreconciledAccountLedgerAsync(int accountId) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn
            .QueryAsync<Ledger>("SELECT * FROM Transactions WHERE AccountId=@accountId AND ReconciliationId IS NULL",
                new { accountId })).ToList();
        return dbRows;
    }

    public async Task<IEnumerable<Transaction>> GetAllPaycheckTransactionsAsync() {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn.QueryAsync<dynamic>(@"
            SELECT t.*, a1.Name as AccountName, 
                   Bills.Name as BillName, Buckets.Name as BucketName 
            , SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id WHERE t.PaycheckId IS NOT NULL")).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Transaction>> GetBillTransactionsAsync() {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn.QueryAsync<dynamic>(@"
            SELECT t.*, a1.Name as AccountName, 
                   Bills.Name as BillName, Buckets.Name as BucketName 
            , SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id WHERE t.BillId IS NOT NULL")).ToList();
        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Transaction>> GetBucketTransactionsAsync() {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn.QueryAsync<dynamic>(@"
            SELECT t.*, a1.Name as AccountName, 
                   Bills.Name as BillName, Buckets.Name as BucketName 
            , SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id WHERE t.BucketId IS NOT NULL")).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<List<string>> GetAlreadyImportedBankIdsAsync(int accountId) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        var dbRows = (await conn
                .QueryAsync<string>(
                    "SELECT FitId FROM Transactions WHERE AccountId=@accountId", new { accountId })
            ).ToList();
        return dbRows;
    }

    public async Task<List<string>> GetAlreadyImportedBankIdsAsync(int accountId, List<string> bankIds) {
        if (bankIds == null || !bankIds.Any()) {
            return new List<string>();
        }

        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        const string sql = @"
        SELECT DISTINCT FitId 
        FROM Transactions 
        WHERE AccountId = @accountId 
          AND IsCleared = 1 
          AND FitId IN @bankIds";

        var results = await conn.QueryAsync<string>(sql, new { accountId, bankIds });
        return results.ToList();
    }

    public async Task<IEnumerable<Transaction>> GetAllUnreconciledTransactionsAsync(int? accountId = null) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        string sql;
        object param;

        if (accountId.HasValue) {
            sql = @"
            SELECT t.*, 
                   a1.Name as AccountName, 
                   Bills.Name as BillName, 
                   Buckets.Name as BucketName, 
                   SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id 
            WHERE t.TransactionId IN (
                SELECT DISTINCT TransactionId 
                FROM Transactions 
                WHERE ReconciliationId IS NULL 
                  AND AccountId = @accountId
            )";
            param = new { accountId };
        }
        else {
            sql = @"
            SELECT t.*, 
                   a1.Name as AccountName, 
                   Bills.Name as BillName, 
                   Buckets.Name as BucketName, 
                   SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id 
            WHERE t.TransactionId IN (
                SELECT DISTINCT TransactionId 
                FROM Transactions 
                WHERE ReconciliationId IS NULL
            )";
            param = new { };
        }

        var dbRows = (await conn.QueryAsync<dynamic>(sql, param)).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Transaction>> GetAllUnclearedTransactionsAsync(int? accountId = null) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        string sql;
        object param;

        if (accountId.HasValue) {
            sql = @"
            SELECT t.*, 
                   a1.Name as AccountName, 
                   Bills.Name as BillName, 
                   Buckets.Name as BucketName, 
                   SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id 
            WHERE t.TransactionId IN (
                SELECT DISTINCT TransactionId 
                FROM Transactions 
                WHERE IsCleared != 1 
                  AND AccountId = @accountId
            )";
            param = new { accountId };
        }
        else {
            sql = @"
            SELECT t.*, 
                   a1.Name as AccountName, 
                   Bills.Name as BillName, 
                   Buckets.Name as BucketName, 
                   SubCategories.Name as SubCategoryName 
            FROM Transactions t
            LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
            LEFT JOIN Bills ON t.BillId = Bills.Id
            LEFT JOIN Buckets ON t.BucketId = Buckets.Id
            LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id 
            WHERE t.TransactionId IN (
                SELECT DISTINCT TransactionId 
                FROM Transactions 
                WHERE IsCleared != 1
            )";
            param = new { };
        }

        var dbRows = (await conn.QueryAsync<dynamic>(sql, param)).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<IEnumerable<Transaction>> GetAllUnreconciledTransactionsSinceLastReconciliationAsync(
        int accountId) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();

        const string sql = @"
        WITH AccountMinDate AS (
            SELECT a.Id AS AccountId, IFNULL(ar.MaxDate, a.BalanceAsOf) AS MinDate 
            FROM Accounts a
            LEFT JOIN (
                SELECT AccountId, MAX(ReconciledAsOfDate) AS MaxDate
                FROM AccountReconciliations
                WHERE IsInvalidated IS NULL OR IsInvalidated = 0
                GROUP BY AccountId
            ) AS ar ON ar.AccountId = a.Id 
            WHERE a.Id = @accountId
        ),
        TargetTransactionIds AS (
            SELECT DISTINCT t.TransactionId
            FROM Transactions t
            INNER JOIN AccountMinDate amd ON t.AccountId = amd.AccountId
            WHERE t.ReconciliationId IS NULL
              AND date(t.TransactionDate) >= date(amd.MinDate)
        )
        SELECT t.*, 
               a1.Name as AccountName, 
               Bills.Name as BillName, 
               Buckets.Name as BucketName, 
               SubCategories.Name as SubCategoryName
        FROM Transactions t
        INNER JOIN TargetTransactionIds target ON t.TransactionId = target.TransactionId
        LEFT JOIN Accounts a1 ON t.AccountId = a1.Id
        LEFT JOIN Bills ON t.BillId = Bills.Id
        LEFT JOIN Buckets ON t.BucketId = Buckets.Id
        LEFT JOIN SubCategories ON t.SubCategoryId = SubCategories.Id;";

        var dbRows = (await conn.QueryAsync<dynamic>(sql, new { accountId })).ToList();

        return MergeDbRowsToUiTransactions(dbRows);
    }

    public async Task<bool> UpdateTransactionForReconciliationAsync(Transaction transaction) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        try {
            if (transaction.AccountId.HasValue) {
                await conn.ExecuteAsync(
                    @"UPDATE Transactions SET ReconciliationId=@ReconciliationId, IsCleared=@IsCleared WHERE AccountId=@AccountId AND TRANSACTIONID=@TransactionId",
                    new {
                        AccountId = transaction.AccountId, 
                        ReconciliationId = transaction.FromAccountReconciledId,
                        TransactionId = transaction.TransactionId.ToString(),
                        IsCleared = transaction.FromAccountIsCleared
                    }, tx);
            }

            if (transaction.ToAccountId.HasValue) {
                await conn.ExecuteAsync(
                    @"UPDATE Transactions SET ReconciliationId=@ReconciliationId, IsCleared=@IsCleared WHERE AccountId=@AccountId AND TRANSACTIONID=@TransactionId",
                    new {
                        AccountId = transaction.ToAccountId, 
                        ReconciliationId = transaction.ToAccountReconciledId,
                        TransactionId = transaction.TransactionId.ToString(),
                        IsCleared = transaction.ToAccountIsCleared
                    }, tx);
            }

            await tx.CommitAsync();
            return true;
        }
        catch {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateTransactionForBankFitIdAsync(int accountId, string transactionId,
        string bankFitId, bool isCleared, int id) {
        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();

        try {
            await conn.ExecuteAsync(@"
            UPDATE Transactions 
            SET FitId = @bankFitId, IsCleared = @isCleared 
            WHERE AccountId = @accountId AND TRANSACTIONID = @transactionId AND ID = @id",
                new {
                    bankFitId,
                    isCleared = isCleared ? 1 : 0,
                    accountId,
                    transactionId,
                    id
                }, tx);

            await tx.CommitAsync();
            return true;
        }
        catch {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<decimal> GetAccountBalanceAsOfAsync(int accountId, DateTime asOfDate,
        SqliteConnection? conn = null, IDbTransaction? tx = null) {
        var account =
            (await GetAllAccountsAsOfAsync(asOfDate: asOfDate, cn: conn, tx: tx))
            .FirstOrDefault(a => a.Id == accountId);
        return account?.Balance ?? 0;
    }

    private async Task<decimal> CalculateAccruedInterestAsync(DateTime paymentDate, decimal apr, int statementDay,
        int accountId, SqliteConnection? cn = null, IDbTransaction? tx = null) {
        int year = paymentDate.Year;
        int month = paymentDate.Month;

        if (paymentDate.Day < statementDay) {
            var prevMonthDate = paymentDate.AddMonths(-1);
            year = prevMonthDate.Year;
            month = prevMonthDate.Month;
        }

        int safeDay = Math.Min(statementDay, DateTime.DaysInMonth(year, month));
        DateTime targetStatementDate = new DateTime(year, month, safeDay).Date.AddDays(1).AddTicks(-1);

        decimal priorBalance = await GetAccountBalanceAsOfAsync(accountId, targetStatementDate, cn, tx);

        decimal monthlyRate = (apr / 100m) / 12m;
        decimal interestAmount = Math.Round(priorBalance * monthlyRate, 2, MidpointRounding.AwayFromZero);

        return interestAmount;
    }

    public async Task<bool> UpsertTransactionAsync(Transaction t,
        bool showConfirmationOfImpactToExistingReconciliations = true) {
        t.Amount = Math.Abs(t.Amount);

        await using var conn = _db.GetConnection();
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        try {
            var bucketsToRecalculate = new HashSet<int>();

            // Step 1: Capture original Bucket IDs before modification
            if (t.TransactionId != Guid.Empty) {
                var oldRows = (await conn.QueryAsync<dynamic>(
                    "SELECT BucketId FROM Transactions WHERE TransactionId = @TransactionId",
                    new { TransactionId = t.TransactionId.ToString() }, tx)).ToList();

                foreach (var row in oldRows) {
                    if (row.BucketId != null) {
                        bucketsToRecalculate.Add((int)row.BucketId);
                    }
                }
            }

            // Guard: Null out stale Reconciliation IDs if referenced AccountReconciliation no longer exists
            if (t.FromAccountReconciledId.HasValue) {
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM AccountReconciliations WHERE Id = @id",
                    new { id = t.FromAccountReconciledId.Value }, tx);
                if (exists == 0) t.FromAccountReconciledId = null;
            }

            if (t.ToAccountReconciledId.HasValue) {
                var exists = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM AccountReconciliations WHERE Id = @id",
                    new { id = t.ToAccountReconciledId.Value }, tx);
                if (exists == 0) t.ToAccountReconciledId = null;
            }

            // Step 2: In-place Upsert Logic
            if (t.TransactionId == Guid.Empty) {
                t.TransactionId = Guid.NewGuid();
            }

            if (t.BucketId.HasValue) {
                bucketsToRecalculate.Add(t.BucketId.Value);
            }
            
            string insertWithIdSql = GetInsertSql() + "; SELECT last_insert_rowid();";

            // --- 1. OUTBOUND / FROM SIDE ---
            if (t.AccountId.HasValue) {
                decimal amount = -Math.Abs(t.Amount);
                if (t.FromRecordId.HasValue && t.FromRecordId > 0) {
                    var p = GetUpdateParameters(t, t.AccountId, amount, t.FromAccountReconciledId, t.FromRecordId,
                        t.FromAccountIsCleared ?? false);
                    await conn.ExecuteAsync(GetUpdateSql(), p, tx);
                }
                else {
                    var p = GetInsertParameters(t, t.AccountId, amount, t.FromAccountReconciledId,
                        t.FromAccountIsCleared ?? false);
                    t.FromRecordId = await conn.ExecuteScalarAsync<int>(insertWithIdSql, p, tx);
                }
            }
            else if (t.FromRecordId.HasValue && t.FromRecordId > 0) {
                await conn.ExecuteAsync("DELETE FROM Transactions WHERE Id = @Id", new { Id = t.FromRecordId }, tx);
                t.FromRecordId = null;
            }

            // --- 2. INBOUND / TO SIDE ---
            if (t.ToAccountId.HasValue) {
                decimal amount = t.AccountId.HasValue ? Math.Abs(t.Amount) : t.Amount;

                if (t.ToRecordId.HasValue && t.ToRecordId > 0) {
                    var p = GetUpdateParameters(t, t.ToAccountId, amount, t.ToAccountReconciledId, t.ToRecordId,
                        t.ToAccountIsCleared ?? false);
                    await conn.ExecuteAsync(GetUpdateSql(), p, tx);
                }
                else {
                    var p = GetInsertParameters(t, t.ToAccountId, amount, t.ToAccountReconciledId,
                        t.ToAccountIsCleared ?? false);
                    t.ToRecordId = await conn.ExecuteScalarAsync<int>(insertWithIdSql, p, tx);
                }
            }
            else if (t.ToRecordId.HasValue && t.ToRecordId > 0) {
                await conn.ExecuteAsync("DELETE FROM Transactions WHERE Id = @Id", new { Id = t.ToRecordId }, tx);
                t.ToRecordId = null;
            }

            // --- 3. MORTGAGE INTEREST COMPUTATION ---
            if (t.ToAccountId.HasValue && !t.IsPrincipalOnly) {
                var toAccount = (await GetAllAccountsAsOfAsync(asOfDate: t.TransactionDate, cn: conn, tx: tx))
                    .FirstOrDefault(a => a.Id == t.ToAccountId.Value);

                if (toAccount != null && (toAccount.IsLoanAccount) && toAccount.MortgageDetails != null) {
                    int statementDay = toAccount.MortgageDetails.StatementDay;

                    int year = t.TransactionDate.Year;
                    int month = t.TransactionDate.Month;
                    if (t.TransactionDate.Day < statementDay) {
                        var prevMonthDate = t.TransactionDate.AddMonths(-1);
                        year = prevMonthDate.Year;
                        month = prevMonthDate.Month;
                    }

                    int safeDay = Math.Min(statementDay, DateTime.DaysInMonth(year, month));
                    DateTime targetStatementDate = new DateTime(year, month, safeDay);

                    var nextMonthDate = targetStatementDate.AddMonths(1);
                    int nextSafeDay = Math.Min(statementDay,
                        DateTime.DaysInMonth(nextMonthDate.Year, nextMonthDate.Month));
                    DateTime nextStatementDate = new DateTime(nextMonthDate.Year, nextMonthDate.Month, nextSafeDay);

                    var memoToSearch = $"as of statement date {targetStatementDate:M/d/yyyy}";

                    var existingInterestOnStatement = await conn.QueryFirstOrDefaultAsync<dynamic>(
                        @"SELECT TransactionId, ReconciliationId FROM Transactions 
                  WHERE AccountId = @accountId 
                  AND IsInterestOnly = 1 
                  AND TransactionDate > @start 
                  AND TransactionDate <= @end",
                        new {
                            accountId = t.ToAccountId.Value,
                            start = targetStatementDate.ToString("yyyy-MM-dd"),
                            end = nextStatementDate.ToString("yyyy-MM-dd")
                        }, tx);

                    if (existingInterestOnStatement == null || existingInterestOnStatement?.TransactionId.ToString() ==
                        t.TransactionId.ToString()) {
                        var interestAmount = await CalculateAccruedInterestAsync(t.TransactionDate,
                            toAccount.MortgageDetails.InterestRate, statementDay, t.ToAccountId.Value, conn, tx);

                        var existingInterest = await conn.QueryFirstOrDefaultAsync<dynamic>(
                            "SELECT Id, ReconciliationId, TransactionDate FROM Transactions WHERE TransactionId = @TransactionId AND IsInterestOnly = 1",
                            new { TransactionId = t.TransactionId.ToString() }, tx);

                        int? interestReconciledId = null;
                        int? existingInterestId = null;
                        bool interestIsCleared = false;

                        if (existingInterest != null) {
                            existingInterestId = (int?)existingInterest.Id;
                            interestReconciledId = (int?)existingInterest.ReconciliationId;
                            interestIsCleared = (bool)(existingInterest.IsCleared == 1);
                        }

                        var interestTx = new Transaction {
                            TransactionId = t.TransactionId,
                            Description = "Interest",
                            Memo = memoToSearch,
                            Amount = Math.Abs(interestAmount),
                            TransactionDate = t.TransactionDate,
                            PeriodDate = t.PeriodDate,
                            IsInterestOnly = true,
                            FromAccountIsCleared = false,
                            ToAccountIsCleared = false
                        };

                        if (existingInterestId.HasValue) {
                            var intParam = GetUpdateParameters(interestTx, t.ToAccountId.Value,
                                -Math.Abs(interestAmount), interestReconciledId, existingInterestId.Value,
                                interestIsCleared);
                            await conn.ExecuteAsync(GetUpdateSql(), intParam, tx);
                        }
                        else {
                            var intParam = GetInsertParameters(interestTx, t.ToAccountId.Value,
                                -Math.Abs(interestAmount), interestReconciledId, interestIsCleared);
                            await conn.ExecuteAsync(GetInsertSql(), intParam, tx);
                        }
                    }
                }
            }

            // --- 4. RE-SYNC BUCKET BALANCES FOR ALL AFFECTED ENVELOPES ---
            foreach (var bucketId in bucketsToRecalculate) {
                await RecalculateBucketBalanceAsync(bucketId, tx);
            }

            await tx.CommitAsync();
            return true;
        }
        catch {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task RecalculateBucketBalanceAsync(int bucketId, IDbTransaction? tx = null) {
        var conn = tx?.Connection ?? _db.GetConnection();
        bool isLocalConn = tx == null;

        try {
            if (isLocalConn && conn is DbConnection dbConn && dbConn.State != ConnectionState.Open) {
                await dbConn.OpenAsync();
            }

            var bucket = await conn.QuerySingleOrDefaultAsync<BudgetBucket>(
                "SELECT * FROM Buckets WHERE Id = @bucketId", new { bucketId }, tx);

            if (bucket == null || bucket.Type != BucketType.AccumulatingDrawdown) return;

            var totalSpent = await conn.ExecuteScalarAsync<decimal?>(@"
            SELECT ABS(SUM(Amount)) 
            FROM Transactions 
            WHERE BucketId = @bucketId AND Amount < 0", new { bucketId }, tx) ?? 0m;

            var totalContributed = await conn.ExecuteScalarAsync<decimal?>(@"
            SELECT SUM(ActualAmount) 
            FROM PeriodBuckets 
            WHERE BucketId = @bucketId AND IsPaid = 1", new { bucketId }, tx) ?? 0m;

            decimal newCurrentBalance = Math.Max(0, bucket.InitialBalance + totalContributed - totalSpent);

            await conn.ExecuteAsync(@"
            UPDATE Buckets 
            SET CurrentBalance = @newCurrentBalance 
            WHERE Id = @bucketId", new { bucketId, newCurrentBalance }, tx);
        }
        finally {
            if (isLocalConn && conn is IAsyncDisposable asyncDisposable) {
                await asyncDisposable.DisposeAsync();
            }
        }
    }

    public async Task DeleteTransactionAsync(Guid transactionId) {
        await using var conn = _db.GetConnection();
        await conn.ExecuteAsync("DELETE FROM Transactions WHERE TransactionId = @transactionId",
            new { transactionId = transactionId.ToString() });
    }

    #region Private Service Helpers (Mapping Engine)

    private IEnumerable<Transaction> MergeDbRowsToUiTransactions(IEnumerable<dynamic> dbRows) {
        var resultList = new List<Transaction>();
        var transactionGroups = dbRows.GroupBy(r => r.TransactionId?.ToString());

        foreach (var group in transactionGroups) {
            if (string.IsNullOrEmpty(group.Key)) continue;

            var list = group.ToList();
            bool hasInterestOnly = list.Any(r => r.IsInterestOnly != null && Convert.ToInt32(r.IsInterestOnly) == 1);

            if (list.Count() >= 2) {
                if (hasInterestOnly) {
                    var normalRows = list.Where(r => r.IsInterestOnly != 1).ToList();
                    var interestRows = list.Where(r => r.IsInterestOnly == 1).ToList();

                    if (normalRows.Count == 2) {
                        resultList.Add(MergeRows(normalRows));
                    }
                    else if (normalRows.Count == 1) {
                        resultList.Add(MapDynamicToTransaction(normalRows[0], false));
                    }

                    foreach (var ir in interestRows) {
                        if ((double)ir.Amount < 0) {
                            resultList.Add(MapDynamicToTransaction(ir, false));
                        }
                    }

                    continue;
                }

                resultList.Add(MergeRows(list));
            }
            else {
                var standaloneRow = list.First();

                if (standaloneRow.IsInterestOnly == 1) {
                    if ((double)standaloneRow.Amount < 0) {
                        resultList.Add(MapDynamicToTransaction(standaloneRow, isTransferSide: false));
                    }
                }
                else {
                    resultList.Add(MapDynamicToTransaction(standaloneRow, isTransferSide: false));
                }
            }
        }

        return resultList;
    }

    private Transaction MergeRows(IEnumerable<dynamic> group) {
        var groupList = group.ToList();

        var outboundSide = groupList.FirstOrDefault(r => r != null && (double)r!.Amount < 0);
        var inboundSide = groupList.FirstOrDefault(r => r != null && (double)r!.Amount >= 0);

        var primaryRow = outboundSide ?? inboundSide;
        if (primaryRow == null) {
            throw new InvalidOperationException("Cannot merge an empty group of transaction rows.");
        }

        var uiTx = MapDynamicToTransaction(primaryRow, isTransferSide: true);

        if (outboundSide != null && inboundSide != null) {
            uiTx.FromRecordId = Convert.ToInt64(outboundSide!.Id);
            uiTx.AccountId = (int)outboundSide.AccountId;
            uiTx.FromAccountReconciledId = outboundSide.ReconciliationId != null
                ? (int?)outboundSide.ReconciliationId
                : null;
            uiTx.AccountName = outboundSide.AccountName;
            uiTx.FromAccountIsCleared = outboundSide.IsCleared == 1;
            uiTx.ToRecordId = Convert.ToInt64(inboundSide!.Id);
            uiTx.ToAccountId = (int)inboundSide.AccountId;
            uiTx.ToAccountReconciledId =
                inboundSide.ReconciliationId != null ? (int?)inboundSide.ReconciliationId : null;
            uiTx.ToAccountName = inboundSide.AccountName;
            uiTx.ToAccountIsCleared = inboundSide.IsCleared == 1;
            uiTx.Amount = Math.Abs((decimal)uiTx.Amount);
        }

        return uiTx;
    }

    private Transaction MapDynamicToTransaction(dynamic row, bool isTransferSide) {
        int? dbAccountId = row.AccountId != null ? (int?)row.AccountId : null;
        int? dbReconciledId = row.ReconciliationId != null ? (int?)row.ReconciliationId : null;
        decimal amount = (decimal)row.Amount;

        int? uiAccountId = null;
        int? uiToAccountId = null;
        bool? uiIsCleared = null;
        bool? uiToIsCleared = null;
        long? uiRecordId = null;
        long? uiToRecordId = null;
        int? uiFromAccountReconciledId = null;
        int? uiToAccountReconciledId = null;

        if (isTransferSide) {
            uiAccountId = dbAccountId;
            uiFromAccountReconciledId = dbReconciledId;
            uiRecordId = Convert.ToInt64(row.Id);
            uiIsCleared = row.IsCleared == 1;
            uiToIsCleared = null;
            uiToRecordId = null;
            uiToAccountId = null;
            uiToAccountReconciledId = null;
        }
        else {
            if (amount >= 0) {
                uiAccountId = null;
                uiFromAccountReconciledId = null;
                uiRecordId = null;
                uiToRecordId = Convert.ToInt64(row.Id);
                uiIsCleared = null;
                uiToIsCleared = row.IsCleared == 1;
                uiToAccountId = dbAccountId;
                uiToAccountReconciledId = dbReconciledId;
            }
            else {
                uiAccountId = dbAccountId;
                uiIsCleared = row.IsCleared == 1;
                uiToIsCleared = null;
                uiToAccountId = null;
                uiToAccountReconciledId = null;
                uiRecordId = Convert.ToInt64(row.Id);
                uiToRecordId = null;
            }
        }

        return new Transaction {
            Description = row.Description,
            NormalizedDescription = row.NormalizedDescription,
            Memo = row.Memo,
            Amount = Math.Abs(amount),
            TransactionDate = DateTime.Parse(row.TransactionDate),
            TransactionId = Guid.Parse(row.TransactionId.ToString()),
            FromRecordId = uiRecordId,
            ToRecordId = uiToRecordId,
            AccountId = uiAccountId,
            AccountName = uiAccountId == null ? "" : row.AccountName,
            ToAccountId = uiToAccountId,
            ToAccountName = uiToAccountId == null ? "" :
                string.IsNullOrEmpty(row.ToAccountName) ? row.AccountName : row.ToAccountName,
            BillId = row.BillId != null ? (int)row.BillId : null,
            BillName = row.BillName,
            BucketId = row.BucketId != null ? (int)row.BucketId : null,
            SubCategoryId = row.SubCategoryId != null ? (int)row.SubCategoryId : null,
            BucketName = row.BucketName,
            IsPrincipalOnly = row.IsPrincipalOnly == 1,
            IsInterestOnly = row.IsInterestOnly == 1,
            FitId = row.FitId != null ? Convert.ToString(row.FitId) : "",
            PaycheckId = row.PaycheckId != null ? (int)row.PaycheckId : null,
            PaycheckOccurrenceDate =
                row.PaycheckOccurrenceDate != null ? DateTime.Parse(row.PaycheckOccurrenceDate) : null,
            FromAccountReconciledId = uiFromAccountReconciledId,
            ToAccountReconciledId = uiToAccountReconciledId,
            FromAccountIsCleared = uiIsCleared,
            ToAccountIsCleared = uiToIsCleared,
        };
    }

    private string GetInsertSql() {
        return
            @"INSERT INTO Transactions (TransactionId, Description, Memo, Amount, TransactionDate, AccountId, BillId, BucketId, PeriodDate, IsPrincipalOnly, IsInterestOnly, FitId, PaycheckId, PaycheckOccurrenceDate, ReconciliationId, NormalizedDescription, IsCleared, SubCategoryId)
                 VALUES (@TransactionId, @Description, @Memo, @Amount, @TransactionDate, @AccountId, @BillId, @BucketId, @PeriodDate, @IsPrincipalOnly, @IsInterestOnly, @FitId, @PaycheckId, @PaycheckOccurrenceDate, @ReconciliationId, @NormalizedDescription, @IsCleared, @SubCategoryId)";
    }

    private string GetUpdateSql() {
        return
            @"UPDATE Transactions SET TransactionId=@TransactionId, Description=@Description, Memo=@Memo, Amount=@Amount, TransactionDate=@TransactionDate, AccountId=@AccountId, BillId=@BillId, BucketId=@BucketId, PeriodDate=@PeriodDate, IsPrincipalOnly= @IsPrincipalOnly, IsInterestOnly=@IsInterestOnly, FitId=@FitId, PaycheckId=@PaycheckId, PaycheckOccurrenceDate=@PaycheckOccurrenceDate, ReconciliationId=@ReconciliationId, NormalizedDescription=@NormalizedDescription, IsCleared=@IsCleared, SubCategoryId=@SubCategoryId
                 WHERE Id=@Id";
    }

    private DynamicParameters GetInsertParameters(Transaction t, int? targetAccountId, decimal targetedAmount,
        int? targetReconciliationId, bool targetIsCleared) {
        var p = new DynamicParameters();
        p.Add("TransactionId", t.TransactionId.ToString());
        p.Add("Description", t.Description);
        p.Add("Memo", t.Memo);
        p.Add("Amount", Math.Round(targetedAmount, 2, MidpointRounding.AwayFromZero));
        p.Add("TransactionDate", t.TransactionDate.ToString("yyyy-MM-dd"));
        p.Add("AccountId", targetAccountId);
        p.Add("BillId", t.BillId);
        p.Add("BucketId", t.BucketId);
        p.Add("PeriodDate", "1900-01-01");
        p.Add("IsPrincipalOnly", t.IsPrincipalOnly ? 1 : 0);
        p.Add("IsInterestOnly", t.IsInterestOnly ? 1 : 0);
        p.Add("FitId", t.FitId.ToString());
        p.Add("PaycheckId", t.PaycheckId);
        p.Add("PaycheckOccurrenceDate", t.PaycheckOccurrenceDate?.ToString("yyyy-MM-dd"));
        p.Add("ReconciliationId", targetReconciliationId);
        p.Add("NormalizedDescription", TransactionMatcher.NormalizeName(t.Description));
        p.Add("IsCleared", targetIsCleared);
        p.Add("SubCategoryId", t.SubCategoryId);
        return p;
    }

    private DynamicParameters GetUpdateParameters(Transaction t, int? targetAccountId, decimal targetedAmount,
        int? targetReconciliationId, long? id, bool targetIsCleared) {
        var p = new DynamicParameters();
        p.Add("TransactionId", t.TransactionId.ToString());
        p.Add("Description", t.Description);
        p.Add("Memo", t.Memo);
        p.Add("Amount", Math.Round(targetedAmount, 2, MidpointRounding.AwayFromZero));
        p.Add("TransactionDate", t.TransactionDate.ToString("yyyy-MM-dd"));
        p.Add("AccountId", targetAccountId);
        p.Add("BillId", t.BillId);
        p.Add("BucketId", t.BucketId);
        p.Add("PeriodDate", "1900-01-01");
        p.Add("IsPrincipalOnly", t.IsPrincipalOnly ? 1 : 0);
        p.Add("IsInterestOnly", t.IsInterestOnly ? 1 : 0);
        p.Add("FitId", t.FitId.ToString());
        p.Add("PaycheckId", t.PaycheckId);
        p.Add("PaycheckOccurrenceDate", t.PaycheckOccurrenceDate?.ToString("yyyy-MM-dd"));
        p.Add("ReconciliationId", targetReconciliationId);
        p.Add("Id", id);
        p.Add("NormalizedDescription", TransactionMatcher.NormalizeName(t.Description));
        p.Add("IsCleared", targetIsCleared);
        p.Add("SubCategoryId", t.SubCategoryId);
        return p;
    }

    #endregion
}