using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations;

namespace GrifballWebApp.Database;

/// <summary>
/// Postgres migrations, with each history table's GiST index on (key, Valid) created as the constraint
/// UNIQUE (key, Valid WITHOUT OVERLAPS), which is that index and its rule. EF has no such constraint, so
/// the model has the index (<see cref="RowHistory.ConstraintName"/>), and its name says it's the
/// constraint. A history table added later, with <c>migrations add</c>, gets its constraint like any
/// index, with no SQL written by hand.
/// </summary>
#pragma warning disable EF1001 // Internal EF Core API usage: Npgsql's options, which its generator takes.
public class PostgresMigrationsSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, INpgsqlSingletonOptions npgsqlSingletonOptions)
    : NpgsqlMigrationsSqlGenerator(dependencies, npgsqlSingletonOptions)
#pragma warning restore EF1001
{
    protected override void Generate(CreateIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!IsConstraint(operation.Name, operation.Table))
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table, operation.Schema))
            .Append(" ADD CONSTRAINT ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name))
            .Append(" UNIQUE (")
            .Append(string.Join(", ", operation.Columns.Select(c => Dependencies.SqlGenerationHelper.DelimitIdentifier(c))))
            .Append(" WITHOUT OVERLAPS)");
        Terminate(builder, terminate);
    }

    protected override void Generate(DropIndexOperation operation, IModel? model, MigrationCommandListBuilder builder, bool terminate = true)
    {
        if (!IsConstraint(operation.Name, operation.Table))
        {
            base.Generate(operation, model, builder, terminate);
            return;
        }

        builder
            .Append("ALTER TABLE ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Table!, operation.Schema))
            .Append(" DROP CONSTRAINT ")
            .Append(Dependencies.SqlGenerationHelper.DelimitIdentifier(operation.Name));
        Terminate(builder, terminate);
    }

    private static bool IsConstraint(string name, string? table)
    {
        return table is not null && name == RowHistory.ConstraintName(table);
    }

    private void Terminate(MigrationCommandListBuilder builder, bool terminate)
    {
        if (terminate)
        {
            builder.AppendLine(Dependencies.SqlGenerationHelper.StatementTerminator);
            EndStatement(builder);
        }
    }
}
