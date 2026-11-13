using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// GrifballWebApp.Application.Test's FakeApplicationDbContext hand-copies GrifballContext's model setup (Application.Test may
/// not reference Infrastructure). This compares the two SQLite schemas, table by table: each table's columns (store type,
/// nullability, collation), keys, indexes (with their filters), foreign keys and check constraints. A configuration or
/// filtered index added to GrifballContext and not to the fake (or the other way round) fails here. Left out on purpose:
/// the SQLite history tables, their triggers and period columns (SqliteHistory), and Data Protection's keys, which the
/// Application layer never touches.
/// </summary>
[TestFixture]
internal sealed class FakeApplicationDbContextModelTests
{
    private static readonly string[] PeriodColumns = ["PeriodStart", "PeriodEnd"];
    private const string DataProtectionKeys = "DataProtectionKeys";

    [Test]
    public void FakeContext_HasGrifballContextsSqliteSchema()
    {
        using var real = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, "DataSource=:memory:").Options);
        using var fake = new FakeApplicationDbContext();

        var expected = Describe(real.GetService<IDesignTimeModel>().Model.GetRelationalModel());
        var actual = Describe(fake.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(actual.Keys, Is.EqualTo(expected.Keys), "The same tables");
            foreach (var (table, description) in expected)
                if (actual.TryGetValue(table, out var fakeDescription))
                    Assert.That(fakeDescription, Is.EqualTo(description), table);
        }
    }

    private static SortedDictionary<string, string> Describe(IRelationalModel model)
    {
        var tables = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var table in model.Tables)
        {
            if (SqliteHistory.HistoryOf(table) is not null || table.Name == DataProtectionKeys)
                continue;
            var lines = new List<string>();
            lines.AddRange(table.Columns.Where(c => !PeriodColumns.Contains(c.Name))
                .Select(c => $"column {c.Name} {c.StoreType} {(c.IsNullable ? "null" : "not null")} collation={c.Collation}"));
            lines.AddRange(table.UniqueConstraints.Select(u => $"key {Columns(u.Columns)}"));
            lines.AddRange(table.Indexes.Select(i => $"index {Columns(i.Columns)} unique={i.IsUnique} filter={i.Filter}"));
            lines.AddRange(table.ForeignKeyConstraints.Select(f =>
                $"foreign key {Columns(f.Columns)} -> {f.PrincipalTable.Name}({Columns(f.PrincipalColumns)}) on delete {f.OnDeleteAction}"));
            lines.AddRange(table.CheckConstraints.Select(c => $"check {c.Name}: {c.Sql}"));
            lines.Sort(StringComparer.Ordinal);
            tables[$"{table.Schema}.{table.Name}"] = string.Join(Environment.NewLine, lines);
        }
        return tables;
    }

    private static string Columns(IEnumerable<IColumn> columns) => string.Join(", ", columns.Select(c => c.Name));
}
