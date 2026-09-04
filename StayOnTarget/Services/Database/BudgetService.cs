using System.IO;
using StayOnTarget.Data;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService {
    private readonly DatabaseContext _db;
    private readonly string _password;

    // Configurable retention limits (can be linked to your Settings model later)
    public int MaxStartupBackups { get; set; } = 5;
    public int MaxRollingBackups { get; set; } = 10;

    public BudgetService(string dbPath, string password) {
        try {
            _password = password;
            _db = new DatabaseContext(dbPath, password);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing BudgetService with path {DbPath}[cite: 20].", dbPath);
            throw;
        }
    }

    public BudgetService(DatabaseContext db, string password) {
        try {
            _password = password;
            _db = db;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing BudgetService with DatabaseContext[cite: 20].");
            throw;
        }
    }

    public string BackupDatabase() {
        try {
            return _db.BackupDatabase(_password);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error backing up database[cite: 20].");
            throw;
        }
    }

    /// <summary>
    /// Creates a timestamped snapshot of the database and enforces separate retention pools 
    /// for startup backups and pre-action operational backups.
    /// </summary>
    public string CreateRollingBackup(string reasonTag = "auto") {
        try {
            var dbPath = _db.DbPath;

            if (!Path.IsPathRooted(dbPath)) {
                dbPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath));
            }

            if (!File.Exists(dbPath) || dbPath == ":memory:") return string.Empty;

            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StayOnTarget",
                "Backups"
            );

            Directory.CreateDirectory(backupDir);

            bool isStartup = reasonTag.Equals("startup", StringComparison.OrdinalIgnoreCase);

            // Generate timestamped filename
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"StayOnTarget_{reasonTag.ToLower()}_{timestamp}.db";
            string destinationPath = Path.Combine(backupDir, backupFileName);

            // Perform encrypted SQLite online backup to explicit path
            _db.BackupDatabaseToPath(destinationPath, _password);

            // Prune after saving new file
            if (isStartup) {
                PruneBackupPool(backupDir, "StayOnTarget_startup_*.db", MaxStartupBackups);
            }
            else {
                PruneBackupPool(backupDir, "StayOnTarget_*.db", MaxRollingBackups,
                    excludePattern: "StayOnTarget_startup_");
            }

            Log.Information("Database backup ({Reason}) created successfully at {Path}", reasonTag, destinationPath);

            return destinationPath;
        }
        catch (Exception ex) {
            Log.Error(ex, "Failed to create database backup for reason: {ReasonTag}", reasonTag);
            return string.Empty;
        }
    }

    private void PruneBackupPool(string backupDir, string searchPattern, int maxRetentionLimit,
        string? excludePattern = null) {
        var dirInfo = new DirectoryInfo(backupDir);
        var files = dirInfo.GetFiles(searchPattern)
            .Where(f => excludePattern == null ||
                        !f.Name.StartsWith(excludePattern, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.CreationTimeUtc)
            .ToList();

        // Prune when file count exceeds max limit
        while (files.Count > maxRetentionLimit) {
            var oldest = files.Last();
            try {
                oldest.Delete();
                Log.Information("Pruned old backup file: {FileName}", oldest.Name);
            }
            catch (Exception ex) {
                Log.Warning(ex, "Could not delete old backup file: {FileName}", oldest.Name);
            }

            files.Remove(oldest);
        }
    }

// public static string GetDbPathFromConnectionString(string connectionString)
// {
//     if (string.IsNullOrWhiteSpace(connectionString))
//         return string.Empty;
//
//     var builder = new SqliteConnectionStringBuilder(connectionString);
//
//     // The DataSource property extracts the file path/location
//     string dbPath = builder.DataSource;
//
//     // Handle relative paths by converting them to full paths if needed
//     if (!string.IsNullOrEmpty(dbPath) && !Path.IsPathRooted(dbPath))
//     {
//         dbPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbPath));
//     }
//
//     return dbPath;
// }
}