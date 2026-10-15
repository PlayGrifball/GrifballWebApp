using System.Data.Common;

namespace GrifballWebApp.Database.Conversion;

/// <summary>How each server quotes names and lists its tables' columns.</summary>
internal abstract class SqlDialect
{
    public static SqlDialect For(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.SqlServer => SqlServerDialect.Instance,
        DatabaseProvider.Postgres => PostgresDialect.Instance,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    /// <summary>The schema a table the model gives none is in.</summary>
    public abstract string DefaultSchema { get; }

    public abstract string Quote(string name);

    public string Quote(TableName table) => $"{Quote(table.Schema.Length == 0 ? DefaultSchema : table.Schema)}.{Quote(table.Name)}";

    /// <summary>A table this server lists, its default schema (dbo, public) as the default (TableName).</summary>
    public TableName Table(string schema, string name) => new(schema == DefaultSchema ? "" : schema, name);

    /// <summary>The columns' query: schema, table, column, type, generated, identity; in column order.</summary>
    protected abstract string ColumnsQuery { get; }

    /// <summary>Every table's columns, bar EF's migrations history, which each side keeps its own of.</summary>
    public async Task<Dictionary<TableName, List<ColumnInfo>>> ReadColumnsAsync(DbConnection connection, DbTransaction? transaction, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = ColumnsQuery;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new Dictionary<TableName, List<ColumnInfo>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var table = Table(reader.GetString(0), reader.GetString(1));
            if (table.Name == "__EFMigrationsHistory")
                continue;
            if (!tables.TryGetValue(table, out var columns))
                tables[table] = columns = [];
            columns.Add(new ColumnInfo(reader.GetString(2), reader.GetString(3), Convert.ToBoolean(reader.GetValue(4)), Convert.ToBoolean(reader.GetValue(5))));
        }
        return tables;
    }
}

internal sealed class SqlServerDialect : SqlDialect
{
    public static readonly SqlServerDialect Instance = new();

    public override string DefaultSchema => "dbo";

    public override string Quote(string name) => $"[{name.Replace("]", "]]")}]";

    // Computed columns only: a temporal table's period columns are generated too, but the SQL Server
    // writer makes them plain columns while it loads (SqlServerTargetWriter).
    protected override string ColumnsQuery => """
        SELECT s.name, t.name, c.name, ty.name, c.is_computed, c.is_identity
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        WHERE t.is_ms_shipped = 0
        ORDER BY s.name, t.name, c.column_id
        """;
}

internal sealed class PostgresDialect : SqlDialect
{
    public static readonly PostgresDialect Instance = new();

    public override string DefaultSchema => "public";

    public override string Quote(string name) => $"\"{name.Replace("\"", "\"\"")}\"";

    protected override string ColumnsQuery => """
        SELECT c.table_schema, c.table_name, c.column_name, c.udt_name, c.is_generated = 'ALWAYS', c.is_identity = 'YES'
        FROM information_schema.columns c
        JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name
        WHERE t.table_type = 'BASE TABLE' AND c.table_schema NOT IN ('pg_catalog', 'information_schema')
        ORDER BY c.table_schema, c.table_name, c.ordinal_position
        """;
}
