using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using StayOnTarget.Helpers;
using System;
using System.Data;
using System.IO;
using System.Windows;

namespace StayOnTarget.Data;

public class SqliteDecimalHandler : SqlMapper.TypeHandler<decimal> {
    public override void SetValue(System.Data.IDbDataParameter parameter, decimal value) {
        parameter.Value = value;
    }

    public override decimal Parse(object value) {
        return Convert.ToDecimal(value);
    }
}

public class SqliteGuidHandler : SqlMapper.TypeHandler<Guid> {
    public override void SetValue(System.Data.IDbDataParameter parameter, Guid value) {
        // Store as TEXT in SQLite
        parameter.Value = value.ToString();
    }

    public override Guid Parse(object value) {
        if (value is Guid g) return g;
        if (value is byte[] bytes && bytes.Length == 16) return new Guid(bytes);
        return Guid.Parse(value?.ToString() ?? string.Empty);
    }
}

public class SqliteNullableGuidHandler : SqlMapper.TypeHandler<Guid?> {
    public override void SetValue(System.Data.IDbDataParameter parameter, Guid? value) {
        parameter.Value = value?.ToString();
    }

    public override Guid? Parse(object value) {
        if (value == null || value is DBNull) return null;
        if (value is Guid g) return g;
        if (value is byte[] bytes && bytes.Length == 16) return new Guid(bytes);
        var s = value.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : Guid.Parse(s);
    }
}

public class DatabaseContext {
    private string _connectionString;
    private string _dbPath;

    //private const string ProgramFolderName = "StayOnTarget";
    //private const string ProgramFolderName = @"AppData\Local\StayOnTarget";
    // private const string DatabaseName = "budget.db";

    static DatabaseContext() {
        // // Call this once at application startup to register the encryption provider
        // SQLitePCL.Batteries_V2.Init();
        SqlMapper.AddTypeHandler(new SqliteDecimalHandler());
        SqlMapper.AddTypeHandler(new SqliteGuidHandler());
        SqlMapper.AddTypeHandler(new SqliteNullableGuidHandler());
    }

    public DatabaseContext(string dbPath, string userPassword) {
        // Ensure the directory exists for whatever path is passed in
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        _dbPath = dbPath;
        _connectionString = BuildConnectionString(_dbPath, userPassword);

        InitializeDatabase();
    }

    // Public helper to compute the default user profile path safely
    // public static string GetDefaultDbPath() {
    //     var userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    //     var dbFolder = Path.Combine(userProfileFolder, ProgramFolderName);
    //     return Path.Combine(dbFolder, DatabaseName);
    // }

    public string BuildConnectionString(string dbPath, string? password) {
        if (string.IsNullOrEmpty(password)) {
            return $"Data Source={dbPath};";
        }

        // Convert Windows backslashes to forward slashes so the SQLite URI parser reads it cleanly
        var normalizedPath = dbPath.Replace('\\', '/');

        var builder = new SqliteConnectionStringBuilder
        {
            // Use URI syntax safely in the Data Source field
            DataSource = $"file:{normalizedPath}?cipher=sqlcipher&legacy=4",
            Password = password,
            Pooling = false
        };
        
        // Semicolons only separate built-in keywords (Data Source, Password, Pooling)
        // The cipher settings live seamlessly inside the Data Source string itself!
        return builder.ConnectionString;
    }

    public string BackupDatabase(string? password) {
        var userProfileFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        //var dbFolder = Path.Combine(userProfileFolder, ProgramFolderName);
        var oldPath = _dbPath;//Path.Combine(dbFolder, DatabaseName);
        if (string.IsNullOrWhiteSpace(oldPath)) {
            MessageBox.Show("No file found to backup.");
            return string.Empty;
        }

        string directory = Path.GetDirectoryName(oldPath)!;
        string filenameWithoutExt = Path.GetFileNameWithoutExtension(oldPath);
        string extension = Path.GetExtension(oldPath);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string newFilename = $"{filenameWithoutExt}_{timestamp}{extension}";
        string newPath = Path.Combine(directory, newFilename);

        // Force the encryption engine to use standard SQLCipher 4 formatting
        // Note the "file:" prefix and the "?cipher=sqlcipher&legacy=4" parameters
        var oldConnectionString = BuildConnectionString(oldPath, password);
        var newConnectionString = BuildConnectionString(newPath, password);

        using (var source = new SqliteConnection(oldConnectionString))
        using (var destination = new SqliteConnection(newConnectionString)) {
            source.Open();
            destination.Open();

            // This performs a full, online backup safely
            source.BackupDatabase(destination);
            return newPath;
        }
    }

    public void ChangePassword(string dbPath, string oldPassword, string newPassword) {
        string connectionString = BuildConnectionString(dbPath, oldPassword);

        using (var connection = new SqliteConnection(connectionString)) {
            connection.Open();

            using (var command = connection.CreateCommand()) {
                // Correct SQLCipher/SQLite3MC syntax: PRAGMA rekey('password')
                // Note: Single quotes wrap the password string inside the command
                command.CommandText = $"PRAGMA rekey('{newPassword}');";
                command.ExecuteNonQuery();
            }
        }

        _connectionString = BuildConnectionString(dbPath, newPassword);
    }

    public SqliteConnection GetConnection() {
        try {
            return new SqliteConnection(_connectionString);
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to create SqliteConnection.");
            throw;
        }
    }

    private void InitializeDatabase() {
        Log.Information("Initializing database.");
        try {
            using var connection = GetConnection();
            connection.Open();
            Log.Debug("Database connection opened for initialization.");

            connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Accounts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                BankName TEXT,
                Balance DECIMAL NOT NULL,
                BalanceAsOf TEXT DEFAULT '2000-01-01', 
                AnnualGrowthRate DECIMAL DEFAULT 0,
                IncludeInTotal INTEGER DEFAULT 1,
                Type INTEGER NOT NULL,
                HexColor TEXT DEFAULT '#FF0000FF',
                IsPrimary INTEGER DEFAULT 0,
                IsArchived INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS MortgageDetails (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL,
                InterestRate DECIMAL NOT NULL,
                Escrow DECIMAL NOT NULL,
                MortgageInsurance DECIMAL NOT NULL,
                LoanPayment DECIMAL NOT NULL,
                PaymentDate TEXT,
                StatementDay INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id)
            );

            CREATE TABLE IF NOT EXISTS CreditCardDetails (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL,
                StatementDay INTEGER NOT NULL,
                DueDateOffset INTEGER NOT NULL DEFAULT 21,
                MinPayFloor DECIMAL NOT NULL DEFAULT 25,
                PayPreviousMonthBalanceInFull INTEGER NOT NULL,
                GraceActive INTEGER DEFAULT 0,
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id)
            );

            CREATE TABLE IF NOT EXISTS Bills (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ExpectedAmount DECIMAL NOT NULL,
                Frequency INTEGER NOT NULL,
                DueDay INTEGER NOT NULL,
                AccountId INTEGER,
                ToAccountId INTEGER,
                NextDueDate TEXT,
                Category TEXT,
                IsPrincipalOnly INTEGER DEFAULT 0,
                IsActive INTEGER DEFAULT 1,
                BucketId INTEGER REFERENCES Buckets(Id) ON DELETE SET NULL, 
                SubCategoryId INTEGER REFERENCES Subcategories(Id) ON DELETE SET NULL,
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id),
                FOREIGN KEY(ToAccountId) REFERENCES Accounts(Id)
            );

            CREATE TABLE IF NOT EXISTS PeriodBills (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BillId INTEGER NOT NULL,
                PeriodDate TEXT NOT NULL,
                DueDate TEXT NOT NULL,
                ActualAmount DECIMAL DEFAULT 0,
                IsPaid INTEGER DEFAULT 0,
                FitId TEXT NOT NULL,
                FOREIGN KEY(BillId) REFERENCES Bills(Id)                
                UNIQUE(BillId, PeriodDate) -- <--- Added composite unique constraint
            );

            CREATE TABLE IF NOT EXISTS Paychecks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ExpectedAmount DECIMAL NOT NULL,
                Frequency INTEGER NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT,
                AccountId INTEGER REFERENCES Accounts(Id),
                IsBalanced INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Description TEXT,
                NormalizedDescription TEXT,
                Memo TEXT,
                Amount DECIMAL NOT NULL,
                TransactionDate TEXT NOT NULL,
                AccountId INTEGER NOT NULL,
                BucketId INTEGER REFERENCES Buckets(Id),
                PeriodDate TEXT NOT NULL,
                IsPrincipalOnly INTEGER DEFAULT 0,
                IsInterestOnly INTEGER DEFAULT 0,
                TransactionId TEXT NOT NULL,
                FitId TEXT NOT NULL,
                PaycheckId INTEGER REFERENCES Paychecks(Id),
                PaycheckOccurrenceDate TEXT,
                BillId INTEGER REFERENCES Bills(Id),
                ReconciliationId INTEGER REFERENCES AccountReconciliations(Id),
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id),
                
                -- Optional budget/tracking links (preserve historical transactions if parent is deleted)
    FOREIGN KEY(BucketId) REFERENCES Buckets(Id) ON DELETE SET NULL,
    FOREIGN KEY(SubCategoryId) REFERENCES Subcategories(Id) ON DELETE SET NULL,
    FOREIGN KEY(BillId) REFERENCES Bills(Id) ON DELETE SET NULL,
    FOREIGN KEY(PaycheckId) REFERENCES Paychecks(Id) ON DELETE SET NULL,
    FOREIGN KEY(ReconciliationId) REFERENCES AccountReconciliations(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS Buckets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ExpectedAmount DECIMAL NOT NULL,
                CurrentBalance DECIMAL NOT NULL,
                InitialBalance DECIMAL NOT NULL,
                AccountId INTEGER,
                PaycheckId INTEGER,
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id),
                FOREIGN KEY(PaycheckId) REFERENCES PayChecks(Id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS PeriodBuckets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                BucketId INTEGER NOT NULL,
                PeriodDate TEXT NOT NULL,
                ActualAmount DECIMAL DEFAULT 0,
                IsPaid INTEGER DEFAULT 0,
                FitId TEXT NOT NULL,
                FOREIGN KEY(BucketId) REFERENCES Buckets(Id)
                UNIQUE(BucketId, PeriodDate) -- <--- Added composite unique constraint
            );

            CREATE TABLE IF NOT EXISTS AccountReconciliations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL,
                ReconciledAsOfDate TEXT NOT NULL, --StatementEndingBalance
                ReconciledBalance DECIMAL NOT NULL, --StatementEndingBalance
                ReconciledOnDate TEXT NOT NULL,
                IsInvalidated INTEGER DEFAULT 0,
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id)
            );

            CREATE TABLE IF NOT EXISTS AccountAprHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                AccountId INTEGER NOT NULL,
                AsOfDate TEXT NOT NULL,
                AnnualPercentageRate DECIMAL NOT NULL,
                CashAdvanceRate DECIMAL NOT NULL,
                BalanceTransferRate DECIMAL NOT NULL,
                FOREIGN KEY(AccountId) REFERENCES Accounts(Id)
            );

CREATE TABLE IF NOT EXISTS AppSettings (
    SettingKey TEXT PRIMARY KEY,
    SettingValue TEXT
);

CREATE TABLE IF NOT EXISTS AccountSnapshots (
    SnapshotDate TEXT NOT NULL,    -- ISO8601 YYYY-MM-DD
    AccountID INTEGER NOT NULL,
    Balance REAL NOT NULL,
    PRIMARY KEY (SnapshotDate, AccountID),
    FOREIGN KEY (AccountID) REFERENCES Accounts(ID)
);

CREATE TABLE IF NOT EXISTS Categories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    HexColor TEXT DEFAULT '#FF0000FF',
    SortOrder INTEGER DEFAULT 0,
    IsArchived INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS Subcategories (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CategoryId INTEGER NOT NULL,
    DefaultBucketId INTEGER,
    Name TEXT NOT NULL,
    SortOrder INTEGER DEFAULT 0,
    IsArchived INTEGER DEFAULT 0,
    FOREIGN KEY(CategoryId) REFERENCES Categories(Id) ON DELETE CASCADE,
    FOREIGN KEY(DefaultBucketId) REFERENCES Buckets(Id) ON DELETE SET NULL
);

-- 1. INSERT TRIGGER
CREATE TRIGGER IF NOT EXISTS trig_Transactions_AfterInsert
AFTER INSERT ON Transactions
BEGIN
    INSERT INTO AccountSnapshots (SnapshotDate, AccountID, Balance)
    VALUES (
        NEW.TransactionDate, 
        NEW.AccountID, 
        COALESCE((SELECT SUM(Amount) FROM Transactions WHERE AccountID = NEW.AccountID AND TransactionDate <= NEW.TransactionDate), 0.00)
    )
    ON CONFLICT(SnapshotDate, AccountID) DO UPDATE SET
        Balance = EXCLUDED.Balance;
END;

-- 2. UPDATE TRIGGER
CREATE TRIGGER IF NOT EXISTS trig_Transactions_AfterUpdate
AFTER UPDATE ON Transactions
BEGIN
    -- Fix the snapshot chain for the OLD account/date
    INSERT INTO AccountSnapshots (SnapshotDate, AccountID, Balance)
    VALUES (
        OLD.TransactionDate, 
        OLD.AccountID, 
        COALESCE((SELECT SUM(Amount) FROM Transactions WHERE AccountID = OLD.AccountID AND TransactionDate <= OLD.TransactionDate), 0.00)
    )
    ON CONFLICT(SnapshotDate, AccountID) DO UPDATE SET
        Balance = EXCLUDED.Balance;

    -- Fix the snapshot chain for the NEW account/date
    INSERT INTO AccountSnapshots (SnapshotDate, AccountID, Balance)
    VALUES (
        NEW.TransactionDate, 
        NEW.AccountID, 
        COALESCE((SELECT SUM(Amount) FROM Transactions WHERE AccountID = NEW.AccountID AND TransactionDate <= NEW.TransactionDate), 0.00)
    )
    ON CONFLICT(SnapshotDate, AccountID) DO UPDATE SET
        Balance = EXCLUDED.Balance;
END;

-- 3. DELETE TRIGGER
CREATE TRIGGER IF NOT EXISTS trig_Transactions_AfterDelete
AFTER DELETE ON Transactions
BEGIN
    INSERT INTO AccountSnapshots (SnapshotDate, AccountID, Balance)
    VALUES (
        OLD.TransactionDate, 
        OLD.AccountID, 
        COALESCE((SELECT SUM(Amount) FROM Transactions WHERE AccountID = OLD.AccountID AND TransactionDate <= OLD.TransactionDate), 0.00)
    )
    ON CONFLICT(SnapshotDate, AccountID) DO UPDATE SET
        Balance = EXCLUDED.Balance;
END;

        ");

            var subcategoryColumnExists = connection.ExecuteScalar<int>(@"
        SELECT COUNT(*) FROM pragma_table_info('Transactions') WHERE name='SubCategoryId'");      
            
            if (subcategoryColumnExists == 0)
            {
                var tableExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Transactions'");

                if (tableExists > 0)
                {
                    // Add SubCategoryId foreign key column
                    connection.Execute("ALTER TABLE Transactions ADD COLUMN SubCategoryId INTEGER REFERENCES Subcategories(Id)");

                    // 3. Seed initial Categories and Subcategories from existing Buckets
                    var existingBuckets = connection.Query<(long Id, string Name)>("SELECT Id, Name FROM Buckets");

                    foreach (var bucket in existingBuckets)
                    {
                        // Create a matching Category for each Bucket
                        var categoryId = connection.QuerySingle<long>(@"
                    INSERT INTO Categories (Name) 
                    VALUES (@Name);
                    SELECT last_insert_rowid();", 
                            new { Name = bucket.Name });

                        // Create a default SubCategory under that Category linked to the Bucket
                        var subCategoryId = connection.QuerySingle<long>(@"
                    INSERT INTO Subcategories (CategoryId, DefaultBucketId, Name) 
                    VALUES (@CategoryId, @DefaultBucketId, @Name);
                    SELECT last_insert_rowid();", 
                            new { 
                                CategoryId = categoryId, 
                                DefaultBucketId = bucket.Id, 
                                Name = $"General {bucket.Name}" 
                            });

                        // 4. Backfill existing transactions that currently have this BucketId
                        connection.Execute(@"
                    UPDATE Transactions 
                    SET SubCategoryId = @subCategoryId 
                    WHERE BucketId = @bucketId", 
                            new { subCategoryId, bucketId = bucket.Id });
                    }
                }
            }
            
            var columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Transactions') WHERE name='NormalizedDescription'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Transactions'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Transactions ADD COLUMN NormalizedDescription TEXT");

                    // Populate the new column
                    var transactions = connection.Query<(long Id, string Description)>("SELECT Id, Description FROM Transactions");
                    foreach (var tx in transactions) {
                        var normalized = TransactionMatcher.NormalizeName(tx.Description);
                        connection.Execute("UPDATE Transactions SET NormalizedDescription = @normalized WHERE Id = @id", new { normalized, id = tx.Id });
                    }
                }
            }
            
            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Transactions') WHERE name='IsCleared'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Transactions'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Transactions ADD COLUMN IsCleared INTEGER DEFAULT 0");
                    
                    var transactions = connection.Query<(long Id, int? ReconciliationId)>("SELECT Id, ReconciliationId FROM Transactions");
                    foreach (var tx in transactions) {
                        var isCleared = tx.ReconciliationId.HasValue;
                        connection.Execute("UPDATE Transactions SET IsCleared = @isCleared WHERE Id = @id", new { isCleared, id = tx.Id });
                    }
                }
            }
            
            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Transactions') WHERE name='IsInterestOnly'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Transactions'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Transactions ADD COLUMN IsInterestOnly INTEGER DEFAULT 0");
                }
            }

            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Bills') WHERE name='IsPrincipalOnly'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Bills'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Bills ADD COLUMN IsPrincipalOnly INTEGER DEFAULT 0");
                }
            }

            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Accounts') WHERE name='IsArchived'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Accounts'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Accounts ADD COLUMN IsArchived INTEGER DEFAULT 0");
                }
            }

            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Bills') WHERE name='IsArchived'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Bills'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Bills ADD COLUMN IsArchived INTEGER DEFAULT 0");
                }
            }

            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Buckets') WHERE name='IsArchived'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Buckets'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Buckets ADD COLUMN IsArchived INTEGER DEFAULT 0");
                }
            }

            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Buckets') WHERE name='IsActive'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Buckets'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Buckets ADD COLUMN IsActive INTEGER DEFAULT 1");
                }
            }

            columnExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Accounts') WHERE name='IsPrimary'");

            if (columnExists == 0) {
                // If the table exists but the column doesn't, add it. 
                // We check if table exists first.
                var tableExists = connection.ExecuteScalar<int>(@"
                SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Accounts'");

                if (tableExists > 0) {
                    connection.Execute("ALTER TABLE Accounts ADD COLUMN IsPrimary INTEGER DEFAULT 0");
                }
            }

            // Check if CreditCardDetails table exists
            var ccDetailsTableExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM sqlite_master WHERE TYPE='table' AND name='CreditCardDetails'");

            if (ccDetailsTableExists == 0) {
                connection.Execute(@"
                CREATE TABLE CreditCardDetails (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    AccountId INTEGER NOT NULL,
                    Apr DECIMAL NOT NULL,
                    StatementDay INTEGER NOT NULL,
                    DueDateOffset INTEGER NOT NULL DEFAULT 21,
                    PayPreviousMonthBalanceInFull INTEGER NOT NULL,
                    GraceActive INTEGER NOT NULL DEFAULT 0,
                    FOREIGN KEY(AccountId) REFERENCES Accounts(Id)
                )");
            }

            // Check if HexColor exists in Accounts table
            var hexColorExists = connection.ExecuteScalar<int>(@"
            SELECT COUNT(*) FROM pragma_table_info('Accounts') WHERE name='HexColor'");

            if (hexColorExists == 0) {
                connection.Execute("ALTER TABLE Accounts ADD COLUMN HexColor TEXT DEFAULT '#FF0000FF'");
            }
            
            if (!connection.Query<dynamic>("PRAGMA table_info(MortgageDetails)").Any(x => x.name == "StatementDay")) {
                connection.Execute("ALTER TABLE MortgageDetails ADD COLUMN StatementDay INTEGER NOT NULL DEFAULT 1;");
            }

            var billsBucketIdColumnExists = connection.ExecuteScalar<int>(@"
    SELECT COUNT(*) FROM pragma_table_info('Bills') WHERE name='BucketId'");

            if (billsBucketIdColumnExists == 0) 
            {
                connection.Execute(
                    "ALTER TABLE Bills ADD COLUMN BucketId INTEGER REFERENCES Buckets(Id) ON DELETE SET NULL;");
            }

            var billsSubCategoryIdColumnExists = connection.ExecuteScalar<int>(@"
    SELECT COUNT(*) FROM pragma_table_info('Bills') WHERE name='SubCategoryId'");

            if (billsSubCategoryIdColumnExists == 0) 
            {
                connection.Execute(
                    "ALTER TABLE Bills ADD COLUMN SubCategoryId INTEGER REFERENCES Subcategories(Id) ON DELETE SET NULL;");
            }

            MigrateBucketsTable(connection);
            
            // 2. Ensure Composite Unique Index Exists for Existing Databases
            // (This guarantees ON CONFLICT(BucketId, PeriodDate) works even if the table was created before UNIQUE was in DDL)
            string createIndexSql = @"
        CREATE UNIQUE INDEX IF NOT EXISTS UX_PeriodBills_BillId_PeriodDate 
        ON PeriodBills(BillId, PeriodDate);";
            connection.Execute(createIndexSql);
            
            // 2. Ensure Composite Unique Index Exists for Existing Databases
            // (This guarantees ON CONFLICT(BucketId, PeriodDate) works even if the table was created before UNIQUE was in DDL)
            createIndexSql = @"
        CREATE UNIQUE INDEX IF NOT EXISTS UX_PeriodBuckets_BucketId_PeriodDate 
        ON PeriodBuckets(BucketId, PeriodDate);";
            connection.Execute(createIndexSql);
            
            CategorySeeder.SeedDefaultCategories(connection);

            connection.Execute("PRAGMA foreign_keys = ON;");
            
            //TransactionTableMigration.FixTransactionForeignKeys(connection);
            
            Log.Information("Database initialization and schema updates completed successfully.");
        }
        catch (Exception ex) {
            Log.Fatal(ex, "Database initialization failed.");
            throw;
        }
    }
    
    // Usage when restoring/initializing SQLite database
    public static void MigrateBucketsTable(IDbConnection connection)
    {
        EnsureColumnExists(connection, "Buckets", "Type", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "Buckets", "TargetBalance", "NUMERIC NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "Buckets", "CurrentBalance", "NUMERIC NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "Buckets", "InitialBalance", "NUMERIC NOT NULL DEFAULT 0");
    }
    
    private static void EnsureColumnExists(IDbConnection connection, string tableName, string columnName, string columnDefinition)
    {
        var exists = connection.ExecuteScalar<int>($@"
        SELECT COUNT(*) 
        FROM pragma_table_info('{tableName}') 
        WHERE name = @columnName", new { columnName });

        if (exists == 0)
        {
            connection.Execute($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};");
        }
    }
}