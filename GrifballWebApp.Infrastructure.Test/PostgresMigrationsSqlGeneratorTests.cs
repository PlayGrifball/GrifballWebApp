using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// A table moved back to the default schema is moved there (<see cref="PostgresMigrationsSqlGenerator"/>): EF writes the
/// move with no new schema, and Npgsql's own generator left the table where it was.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PostgresMigrationsSqlGeneratorTests
{
    private static string Sql(MigrationOperation operation)
    {
        using var context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Postgres, "Host=offline;Database=x").Options);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        Assert.That(generator, Is.TypeOf<PostgresMigrationsSqlGenerator>());
        return string.Join("\n", generator.Generate([operation], context.Model).Select(c => c.CommandText));
    }

    [Test]
    public void AMoveToNoSchema_IsAMoveToTheDefaultOne()
        => Assert.That(Sql(new RenameTableOperation { Name = "MatchReschedules", Schema = "Event", NewName = "MatchReschedules" }),
            Does.Match($"SET SCHEMA \"?{PostgresMigrationsSqlGenerator.DefaultSchema}\"?"));

    [Test]
    public void AMoveToANamedSchema_IsAsBefore()
        => Assert.That(Sql(new RenameTableOperation { Name = "MatchReschedules", NewName = "MatchReschedules", NewSchema = "Event" }),
            Does.Match("SET SCHEMA \"?Event\"?"));

    [Test]
    public void ARenameInTheDefaultSchema_MovesNothing()
        => Assert.That(Sql(new RenameTableOperation { Name = "Old", NewName = "New" }), Does.Not.Contain("SET SCHEMA"));
}
