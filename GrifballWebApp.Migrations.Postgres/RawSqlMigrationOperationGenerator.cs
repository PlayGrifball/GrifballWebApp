using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Migrations.Postgres;

/// <summary>
/// EF's C# for migration operations, bar SQL of more than one line (row history's functions,
/// HistoryMigrationsModelDiffer), which it writes as a raw string literal, one line of the migration
/// per line of SQL, rather than one line with each line break a \n.
/// </summary>
public class RawSqlMigrationOperationGenerator(CSharpMigrationOperationGeneratorDependencies dependencies)
    : CSharpMigrationOperationGenerator(dependencies)
{
    protected override void Generate(SqlOperation operation, IndentedStringBuilder builder)
    {
        if (!operation.Sql.Contains('\n') || operation.GetAnnotations().Any())
        {
            base.Generate(operation, builder);
            return;
        }

        // Longer than any run of quotes in the SQL, so none of them ends it.
        var longest = 0;
        for (int i = 0, run = 0; i < operation.Sql.Length; i++)
            longest = Math.Max(longest, run = operation.Sql[i] == '"' ? run + 1 : 0);
        var quotes = new string('"', Math.Max(3, longest + 1));

        builder.AppendLine($".Sql({quotes}");
        using (builder.Indent())
        {
            foreach (var line in operation.Sql.TrimEnd('\n').Split('\n'))
                builder.AppendLine(line.TrimEnd('\r'));
            builder.Append($"{quotes})");
        }
    }
}
