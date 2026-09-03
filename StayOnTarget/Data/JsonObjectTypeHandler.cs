using System.Data;
using System.Text.Json;
using Dapper;

namespace StayOnTarget.Data;

public class JsonObjectTypeHandler<T> : SqlMapper.TypeHandler<T> where T : class, new()
{
    public override void SetValue(IDbDataParameter parameter, T? value)
    {
        parameter.Value = value is null ? DBNull.Value : JsonSerializer.Serialize(value);
        parameter.DbType = DbType.String;
    }

    public override T Parse(object value)
    {
        if (value is null or DBNull) return new T();
        var json = value.ToString();
        return string.IsNullOrWhiteSpace(json) 
            ? new T() 
            : JsonSerializer.Deserialize<T>(json) ?? new T();
    }
}