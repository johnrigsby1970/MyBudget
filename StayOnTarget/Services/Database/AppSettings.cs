using Dapper;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<string?> GetSettingAsync(string key, string? defaultValue = null)
    {
        await using var conn = _db.GetConnection();
        var value = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT SettingValue FROM AppSettings WHERE SettingKey = @key", 
            new { key });

        return value ?? defaultValue;
    }

    public async Task SaveSettingAsync(string key, string value)
    {
        await using var conn = _db.GetConnection();
    
        // SQLite UPSERT syntax
        await conn.ExecuteAsync(@"
        INSERT INTO AppSettings (SettingKey, SettingValue) 
        VALUES (@key, @value)
        ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue",
            new { key, value });
    }

}