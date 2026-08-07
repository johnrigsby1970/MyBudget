using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace StayOnTarget.Services;

public partial class BudgetService {
    public async Task<string?> GetSettingAsync(
        string key, 
        string? defaultValue = null, 
        SqliteConnection? cn = null, 
        IDbTransaction? tx = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return defaultValue;

        cn ??= tx?.Connection as SqliteConnection;
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try
        {
            if (isLocalConn && conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            var value = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT SettingValue FROM AppSettings WHERE SettingKey = @key", 
                new { key = key.Trim() }, 
                tx);

            return value ?? defaultValue;
        }
        finally
        {
            if (isLocalConn)
            {
                await conn.DisposeAsync();
            }
        }
    }

    public async Task SaveSettingAsync(
        string key, 
        string value, 
        SqliteConnection? cn = null, 
        IDbTransaction? tx = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return;

        cn ??= tx?.Connection as SqliteConnection;
        bool isLocalConn = cn == null;
        var conn = cn ?? _db.GetConnection();

        try
        {
            if (isLocalConn && conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            // SQLite UPSERT syntax
            await conn.ExecuteAsync(@"
                INSERT INTO AppSettings (SettingKey, SettingValue) 
                VALUES (@key, @value)
                ON CONFLICT(SettingKey) DO UPDATE SET SettingValue = excluded.SettingValue",
                new { key = key.Trim(), value = value ?? "" }, 
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