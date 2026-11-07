using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

#pragma warning disable EF1001 // Npgsql's own generator, extended

namespace GrifballWebApp.Database;

/// <summary>
/// Postgres's SQL for a migration, a table moved back to the default schema moved there. EF writes a move to the default
/// schema as a RenameTable with no new schema (null: the default one), as a Down undoing a move out of it does
/// (MatchReschedules, from Event back to public); SQL Server's generator moves it to dbo, but Npgsql's moves a table only
/// when the new schema is named, and left it where it was, so the rest of the Down didn't find it. Here no new schema
/// means the model's default schema, public, as EF means it.
/// Applying migrations uses this, as the app and the bundle do (DatabaseProvider), and so does scripting them.
/// </summary>
public class PostgresMigrationsSqlGenerator(
    MigrationsSqlGeneratorDependencies dependencies,
    INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
{
    /// <summary>Postgres's default schema, where a table in no schema of its own is.</summary>
    public const string DefaultSchema = "public";

    protected override void Generate(RenameTableOperation operation, IModel? model, MigrationCommandListBuilder builder)
    {
        if (operation.NewSchema is null && operation.Schema is not null)
            operation.NewSchema = model?.GetDefaultSchema() ?? DefaultSchema;
        base.Generate(operation, model, builder);
    }
}
