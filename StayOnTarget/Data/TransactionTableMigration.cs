namespace StayOnTarget.Data;

using Dapper;
using System.Data;

public static class TransactionTableMigration
{
    public static void FixTransactionForeignKeys(IDbConnection connection)
    {
        // 1. Disable foreign keys temporarily for schema restructuring
        connection.Execute("PRAGMA foreign_keys = OFF;");

        // Begin transaction to ensure atomic execution
        using var transaction = connection.BeginTransaction();

        try
        {
            // 2. Create new table with updated foreign key actions (ON DELETE SET NULL)
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Transactions_temp (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Description TEXT,
                    NormalizedDescription TEXT,
                    Memo TEXT,
                    Amount DECIMAL NOT NULL,
                    TransactionDate TEXT NOT NULL,
                    AccountId INTEGER NOT NULL,
                    BucketId INTEGER,
                    SubCategoryId INTEGER,
                    BillId INTEGER,
                    PaycheckId INTEGER,
                    ReconciliationId INTEGER,
                    PeriodDate TEXT NOT NULL,
                    IsPrincipalOnly INTEGER DEFAULT 0,
                    IsInterestOnly INTEGER DEFAULT 0,
                    TransactionId TEXT NOT NULL,
                    FitId TEXT NOT NULL,
                    PaycheckOccurrenceDate TEXT,
                    
                    FOREIGN KEY(AccountId) REFERENCES Accounts(Id),
                    FOREIGN KEY(BucketId) REFERENCES Buckets(Id) ON DELETE SET NULL,
                    FOREIGN KEY(SubCategoryId) REFERENCES Subcategories(Id) ON DELETE SET NULL,
                    FOREIGN KEY(BillId) REFERENCES Bills(Id) ON DELETE SET NULL,
                    FOREIGN KEY(PaycheckId) REFERENCES Paychecks(Id) ON DELETE SET NULL,
                    FOREIGN KEY(ReconciliationId) REFERENCES AccountReconciliations(Id) ON DELETE SET NULL
                );
            ", transaction: transaction);

            // 3. Inspect existing table columns so we only copy columns that currently exist
            var existingColumns = connection.Query<string>(
                "SELECT name FROM pragma_table_info('Transactions')", 
                transaction: transaction).ToList();

            // Core columns present in original schema
            var selectColumns = new List<string>
            {
                "Id", "Description", "Memo", "Amount", "TransactionDate", 
                "AccountId", "BucketId", "PeriodDate", "IsPrincipalOnly", 
                "IsInterestOnly", "TransactionId", "FitId", "PaycheckId", 
                "PaycheckOccurrenceDate", "BillId", "ReconciliationId"
            };

            // Include optional columns if they already exist in the target DB
            if (existingColumns.Contains("NormalizedDescription", StringComparer.OrdinalIgnoreCase))
                selectColumns.Add("NormalizedDescription");

            if (existingColumns.Contains("SubCategoryId", StringComparer.OrdinalIgnoreCase))
                selectColumns.Add("SubCategoryId");

            string columnList = string.Join(", ", selectColumns);

            // 4. Copy existing data over to the temporary table
            connection.Execute($@"
                INSERT INTO Transactions_temp ({columnList})
                SELECT {columnList} FROM Transactions;
            ", transaction: transaction);

            // 5. Drop old table and rename new table to replace it
            connection.Execute("DROP TABLE Transactions;", transaction: transaction);
            connection.Execute("ALTER TABLE Transactions_temp RENAME TO Transactions;", transaction: transaction);

            // 6. Re-create indexes if needed (e.g., NormalizedDescription index)
            if (selectColumns.Contains("NormalizedDescription"))
            {
                connection.Execute(@"
                    CREATE INDEX IF NOT EXISTS IX_Transactions_NormalizedDescription 
                    ON Transactions(NormalizedDescription);
                ", transaction: transaction);
            }

            // Commit migration changes
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            // 7. Re-enable foreign key enforcement
            connection.Execute("PRAGMA foreign_keys = ON;");
        }
    }
}