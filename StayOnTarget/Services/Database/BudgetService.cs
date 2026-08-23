using StayOnTarget.Data;
using Serilog;

namespace StayOnTarget.Services;

public partial class BudgetService
{
    private readonly DatabaseContext _db;
    private readonly string _password;

    public BudgetService(string dbPath, string password)
    {
        try {
            _password = password;
            _db = new DatabaseContext(dbPath, password);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing BudgetService with path {DbPath}[cite: 20].", dbPath);
            throw;
        }
    }
    
    public BudgetService(DatabaseContext db, string password)
    {
        try {
            _password = password;
            _db = db;
        }
        catch (Exception ex) {
            Log.Error(ex, "Error initializing BudgetService with DatabaseContext[cite: 20].");
            throw;
        }
    }
    
    public string BackupDatabase()
    {
        try {
            return _db.BackupDatabase(_password);
        }
        catch (Exception ex) {
            Log.Error(ex, "Error backing up database[cite: 20].");
            throw;
        }
    }
}