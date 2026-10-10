using System.Text;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace GrifballWebApp.Database;

/// <summary>
/// PostgreSQL's migrations SQL, with row history (readme.md, Database). History is on when the database
/// has the periods extension: every table then has SYSTEM VERSIONING, a history table and a view and
/// functions over both. A plain ALTER TABLE on such a table goes wrong: adding a column leaves it out of
/// the history, changing a column's type or dropping a column or the table fails on the view, and
/// renaming a column succeeds and then every UPDATE and DELETE of the table fails. So each of those
/// operations runs in a DO block that, if the table has SYSTEM VERSIONING as the migration runs, suspends
/// it, makes the change to the table and the same to its history table, and resumes it (history kept;
/// periods' documented way); without it, just the change. A new table gets SYSTEM VERSIONING when the
/// database has the extension. Registered for both run time and design time (UseGrifballDatabase): the
/// bundle and MigrateAsync generate a migration's SQL as they apply it.
/// </summary>
#pragma warning disable EF1001 // Internal EF Core API usage.
// NpgsqlMigrationsSqlGenerator's constructor takes Npgsql's internal options service; there is no other
// way to extend it. Only the constructor touches it.
public class RowHistoryMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal.INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
#pragma warning restore EF1001
{
    public const string PeriodStart = "PeriodStart";
    public const string PeriodEnd = "PeriodEnd";

    protected override void Generate(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        switch (operation)
        {
            // EF's own history table (__EFMigrationsHistory) is created through here too, and never versioned.
            case CreateTableOperation create when create.Name != HistoryRepository.DefaultTableName:
                base.Generate(operation, model, builder);
                builder.Append(VersionNewTable(Table(create.Schema, create.Name))).EndCommand();
                return;
            case AddColumnOperation add:
                Suspended(operation, model, builder, add.Schema, add.Table,
                    History($"ADD COLUMN {Identifier(add.Name)} {HistoryColumnType(add, model)}{HistoryDefault(add, model)}"));
                return;
            case AlterColumnOperation alter:
                Suspended(operation, model, builder, alter.Schema, alter.Table, AlterHistoryColumn(alter, model));
                return;
            case RenameColumnOperation rename:
                Suspended(operation, model, builder, rename.Schema, rename.Table,
                    History($"RENAME COLUMN {Identifier(rename.Name)} TO {Identifier(rename.NewName)}"));
                return;
            case DropColumnOperation drop:
                Suspended(operation, model, builder, drop.Schema, drop.Table, History($"DROP COLUMN {Identifier(drop.Name)}"));
                return;
            case RenameTableOperation rename:
                Suspended(operation, model, builder, rename.Schema, rename.Name, RenameHistoryTable(rename));
                return;
            case DropTableOperation drop:
                DropVersionedTable(operation, model, builder, drop.Schema, drop.Name);
                return;
            default:
                base.Generate(operation, model, builder);
                return;
        }
    }

    /// <summary>The table as a regclass literal: '"Schema"."Table"'.</summary>
    private string Table(string? schema, string name) => Literal(Dependencies.SqlGenerationHelper.DelimitIdentifier(name, schema));

    private string Identifier(string name) => Dependencies.SqlGenerationHelper.DelimitIdentifier(name);

    private static string Literal(string value) => $"'{value.Replace("'", "''")}'";

    /// <summary>The operation's own SQL, as base generates it, for the DO block to EXECUTE.</summary>
    private string OwnSql(MigrationOperation operation, IModel? model)
    {
        var own = new MigrationCommandListBuilder(Dependencies);
        base.Generate(operation, model, own);
        return string.Concat(own.GetCommandList().Select(c => c.CommandText));
    }

    /// <summary>A statement on the history table (the DO block's history variable): ALTER TABLE history ...</summary>
    private static string History(string alteration) => $"EXECUTE 'ALTER TABLE ' || history::text || {Quote(" " + alteration)};";

    /// <summary>Dollar-quoted: generated SQL goes in as it is.</summary>
    private static string Quote(string sql) => $"$grif_sql${sql}$grif_sql$";

    /// <summary>
    /// The column's type and collation, without its default, identity or NOT NULL: the history table's.
    /// <paramref name="of"/>: the column it describes (an AlterColumnOperation's OldColumn names none).
    /// </summary>
    private string HistoryColumnType(ColumnOperation column, IModel? model, ColumnOperation? of = null)
    {
        of ??= column;
        var type = column.ColumnType ?? GetColumnType(of.Schema, of.Table, of.Name, column, model)!;
        return column.Collation is null ? type : $"{type} COLLATE {Identifier(column.Collation)}";
    }

    /// <summary>
    /// The new column's default, if it has one: the history's rows get it as the table's rows do, so an
    /// old version reads into the entity as a current one does (a NOT NULL column is never NULL there).
    /// </summary>
    private string HistoryDefault(AddColumnOperation add, IModel? model)
    {
        if (add.DefaultValue is null && add.DefaultValueSql is null)
            return "";
        var sql = new MigrationCommandListBuilder(Dependencies);
        DefaultValue(add.DefaultValue, add.DefaultValueSql, add.ColumnType ?? GetColumnType(add.Schema, add.Table, add.Name, add, model), sql);
        sql.EndCommand();
        return sql.GetCommandList().Single().CommandText.TrimEnd();
    }

    /// <summary>
    /// The history table's column keeps the table's type and collation (periods requires the same), and
    /// takes NULL once the table's does. It never gets NOT NULL: older versions may have none.
    /// </summary>
    private string AlterHistoryColumn(AlterColumnOperation alter, IModel? model)
    {
        var sql = new StringBuilder();
        var type = HistoryColumnType(alter, model);
        if (type != HistoryColumnType(alter.OldColumn, model, alter))
            sql.AppendLine(History($"ALTER COLUMN {Identifier(alter.Name)} TYPE {type}"));
        if (alter.IsNullable && !alter.OldColumn.IsNullable)
            sql.AppendLine(History($"ALTER COLUMN {Identifier(alter.Name)} DROP NOT NULL"));
        return sql.ToString();
    }

    /// <summary>
    /// The history table follows the table: the name periods gives a new table's (periods._choose_name,
    /// what add_system_versioning picks), in its schema.
    /// </summary>
    private string RenameHistoryTable(RenameTableOperation rename)
    {
        var sql = new StringBuilder();
        if (rename.NewName is not null && rename.NewName != rename.Name)
            sql.AppendLine($"EXECUTE format('ALTER TABLE %s RENAME TO %I', history, periods._choose_name(ARRAY[{Literal(rename.NewName)}]::name[], 'history'));");
        if (rename.NewSchema is not null && rename.NewSchema != rename.Schema)
            sql.AppendLine($"EXECUTE format('ALTER TABLE %s SET SCHEMA %I', history, {Literal(rename.NewSchema)});");
        return sql.ToString();
    }

    /// <summary>The DO block's start: history, the table's history table when it has SYSTEM VERSIONING.</summary>
    private static string FindHistory(string table)
    {
        // periods' catalog only by EXECUTE: PL/pgSQL plans a static query that names a missing table, so
        // without the extension it would fail. The statements that call periods' functions are planned
        // only when they run, which is only with it.
        return $"""
            DO $grif_history$
            DECLARE
                history regclass;
            BEGIN
                IF EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') THEN
                    EXECUTE 'SELECT history_table_name FROM periods.system_versioning WHERE table_name = $1::regclass'
                        INTO history USING {table};
                END IF;

            """;
    }

    /// <summary>The operation, with SYSTEM VERSIONING suspended around it and its history table changed alike.</summary>
    private void Suspended(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder,
        string? schema, string name, string historySql)
    {
        var table = Table(schema, name);
        var resumed = operation is RenameTableOperation rename
            ? Table(rename.NewSchema ?? schema, rename.NewName ?? name)
            : table;
        builder.Append(FindHistory(table))
            .Append($"""
                    IF history IS NOT NULL THEN
                        PERFORM periods.drop_system_versioning({table});
                    END IF;
                    EXECUTE {Quote(OwnSql(operation, model))};
                    IF history IS NOT NULL THEN
                {Indent(historySql)}
                        PERFORM periods.add_system_versioning({resumed});
                    END IF;
                END $grif_history$;
                """)
            .EndCommand();
    }

    /// <summary>
    /// A dropped table's history goes with it, as SQL Server drops a temporal table's: SYSTEM VERSIONING
    /// and the period are removed first (purged: the history table, view, functions, triggers and
    /// constraints), which periods requires before the table can go.
    /// </summary>
    private void DropVersionedTable(MigrationOperation operation, IModel? model, MigrationCommandListBuilder builder, string? schema, string name)
    {
        var table = Table(schema, name);
        builder.Append(FindHistory(table))
            .Append($"""
                    IF history IS NOT NULL THEN
                        PERFORM periods.drop_system_versioning({table}, purge => true);
                        PERFORM periods.drop_system_time_period({table}, purge => true);
                    END IF;
                    EXECUTE {Quote(OwnSql(operation, model))};
                END $grif_history$;
                """)
            .EndCommand();
    }

    /// <summary>With the extension in the database, a new table keeps its history from the start.</summary>
    private static string VersionNewTable(string table)
    {
        return $"""
            DO $grif_history$
            BEGIN
                IF EXISTS (SELECT FROM pg_extension WHERE extname = 'periods') THEN
                    PERFORM periods.add_system_time_period({table}, '{PeriodStart}', '{PeriodEnd}');
                    PERFORM periods.add_system_versioning({table});
                END IF;
            END $grif_history$;
            """;
    }

    private static string Indent(string sql) =>
        string.Join("\n", sql.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => "        " + l.TrimEnd('\r')));
}
