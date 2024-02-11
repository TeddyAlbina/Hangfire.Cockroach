using System;
using System.Data;
using System.Text.Json;

using Dapper;

using Hangfire.Annotations;

using Npgsql;

using NpgsqlTypes;

namespace Hangfire.Cockroach;

internal sealed class JsonParameter : SqlMapper.ICustomQueryParameter
{
    [CanBeNull] 
    private readonly object value;
    
    private readonly ValueType type;

    public JsonParameter([CanBeNull] object value) : this(value, ValueType.Object)
    {
    }

    public JsonParameter([CanBeNull] object value, ValueType type)
    {
        this.value = value;
        this.type = type;
    }

    public void AddParameter(IDbCommand command, string name)
    {
        var value = this.value switch
        {
            string { Length: > 0 } stringValue => stringValue,
            string { Length: 0 } or null => this.GetDefaultValue(),
            var _ => JsonSerializer.Serialize(this.value),
        };

        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = value });
    }

    private string GetDefaultValue() => this.type switch
    {
        ValueType.Object => "{}",
        ValueType.Array => "[]",
        var _ => throw new ArgumentOutOfRangeException(),
    };

    public enum ValueType
    {
        Object,
        Array,
    }
}
