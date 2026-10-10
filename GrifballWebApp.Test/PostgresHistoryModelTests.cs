using GrifballWebApp.Database;
using GrifballWebApp.Migrations.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Test;

/// <summary>
/// Row history's model and scaffolding (PostgresHistory, HistoryMigrationsModelDiffer), with no
/// database: these run on both. What the triggers do is PostgresHistoryTests.
/// </summary>
[TestFixture]
public class PostgresHistoryModelTests
{
    private static GrifballContext Context(DatabaseProvider provider)
    {
        var connectionString = provider == DatabaseProvider.Postgres ? "Host=unused;Database=unused" : "Server=unused;Database=unused";
        return new GrifballContext(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(provider, connectionString).Options);
    }

    private static IEnumerable<IEntityType> HistoryEntityTypes(IModel model)
    {
        return model.GetEntityTypes().Where(e => e.FindAnnotation(PostgresHistory.HistoryOfAnnotation) is not null);
    }

    [Test]
    public void Postgres_KeepsTheHistoryOfSqlServersTemporalTables_InTheSameTables()
    {
        using var sqlServer = Context(DatabaseProvider.SqlServer);
        using var postgres = Context(DatabaseProvider.Postgres);

        var temporal = sqlServer.GetService<IDesignTimeModel>().Model.GetEntityTypes().Where(e => e.IsTemporal())
            .Select(e => $"{e.GetSchema()}.{e.GetTableName()} -> {e.GetHistoryTableSchema()}.{e.GetHistoryTableName()}")
            .Distinct().Order().ToList();
        var history = HistoryEntityTypes(postgres.GetService<IDesignTimeModel>().Model)
            .Select(e => $"{e.GetSchema()}.{e.FindAnnotation(PostgresHistory.HistoryOfAnnotation)!.Value} -> {e.GetSchema()}.{e.GetTableName()}")
            .Order().ToList();

        Assert.That(history, Is.EqualTo(temporal));
        Assert.That(history, Has.Count.GreaterThan(30));
    }

    [Test]
    public void SqlServer_HasNoHistoryEntities()
    {
        using var sqlServer = Context(DatabaseProvider.SqlServer);

        Assert.That(HistoryEntityTypes(sqlServer.Model), Is.Empty);
    }

    [Test]
    public void Postgres_TablesWithHistory_HaveAStampedPeriodStart()
    {
        using var postgres = Context(DatabaseProvider.Postgres);

        var periodStart = postgres.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Database.Models.Season))!.FindProperty(PostgresHistory.PeriodStart)!;

        Assert.Multiple(() =>
        {
            Assert.That(periodStart.IsShadowProperty(), Is.True);
            Assert.That(periodStart.ValueGenerated, Is.EqualTo(ValueGenerated.OnAddOrUpdate));
            Assert.That(periodStart.GetColumnType(), Is.EqualTo("timestamp without time zone"));
            Assert.That(periodStart.GetDefaultValueSql(), Is.EqualTo("transaction_timestamp() AT TIME ZONE 'UTC'"));
            Assert.That(postgres.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Database.Models.MatchReschedule))!.FindProperty(PostgresHistory.PeriodStart), Is.Null,
                "MatchReschedules isn't temporal on SQL Server either");
        });
    }

    [Test]
    public void HistoryTable_CopiesEachColumn_NullableWithoutKeyOrDefault()
    {
        using var context = new WithHistory();
        var history = context.GetService<IDesignTimeModel>().Model.FindEntityType("Test.ThingsHistory")!;
        IProperty Column(string name) => history.FindProperty(name)!;

        Assert.Multiple(() =>
        {
            Assert.That(history.GetTableName(), Is.EqualTo("ThingsHistory"));
            Assert.That(history.GetSchema(), Is.EqualTo("Test"));
            Assert.That(history.FindPrimaryKey(), Is.Null);
            Assert.That(history.GetForeignKeys(), Is.Empty);
            Assert.That(history.GetIndexes().Single().Properties.Select(p => p.Name), Is.EqualTo(new[] { "A", "B", "PeriodEnd" }));
            Assert.That(history.FindAnnotation(PostgresHistory.HistoryOfAnnotation)!.Value, Is.EqualTo("Things"));

            Assert.That(Column("A").ClrType, Is.EqualTo(typeof(int?)));
            Assert.That(Column("A").IsNullable, Is.True);
            Assert.That(Column("A").ValueGenerated, Is.EqualTo(ValueGenerated.Never));
            Assert.That(Column("Count").ClrType, Is.EqualTo(typeof(int?)));
            Assert.That(Column("Price").GetColumnType(), Is.EqualTo("numeric(9,2)"));
            Assert.That(Column("Rate").GetColumnType(), Is.EqualTo("numeric(5)"));
            Assert.That(Column("Code").GetColumnType(), Is.EqualTo("character(4)"));
            Assert.That(Column("Code").GetCollation(), Is.EqualTo("C"));
            Assert.That(Column("Kind").GetColumnType(), Is.EqualTo("character varying(8)"));
            Assert.That(Column("Kind").GetProviderClrType(), Is.EqualTo(typeof(string)));
            Assert.That(Column("When").GetValueConverter(), Is.Not.Null);
            Assert.That(Column("Flag").GetDefaultValue(), Is.Null);
            Assert.That(Column("PeriodStart").IsNullable, Is.False);
            Assert.That(Column("PeriodEnd").IsNullable, Is.False);
            Assert.That(Column("PeriodEnd").GetColumnType(), Is.EqualTo("timestamp without time zone"));
        });
    }

    [Test]
    public void HistoryTable_OfAHierarchy_HasEveryTypesColumns()
    {
        using var context = new WithHistory();

        var columns = context.GetService<IDesignTimeModel>().Model.FindEntityType("Test.AnimalsHistory")!.GetProperties().Select(p => p.Name);

        Assert.That(columns, Is.EquivalentTo(new[] { "AnimalID", "Discriminator", "Name", "Bark", "PeriodStart", "PeriodEnd" }));
        Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Dog))!.FindProperty(PostgresHistory.PeriodStart)!.DeclaringType.ClrType, Is.EqualTo(typeof(Animal)));
    }

    [Test]
    public void HistoryTables_AreForTables_AnyEntityShape()
    {
        using var context = new WithHistory();

        Assert.Multiple(() =>
        {
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType("Tag")!.FindProperty(PostgresHistory.PeriodStart), Is.Not.Null);
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType("Test.TagsHistory"), Is.Not.Null);
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType("LogsHistory")!.GetIndexes().Single().Properties.Select(p => p.Name), Is.EqualTo(new[] { "PeriodEnd" }));
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Report))!.FindProperty(PostgresHistory.PeriodStart), Is.Null, "a view has no history");
        });
    }

    [Test]
    public void Differ_CreatingHistoryTables_TurnsVersioningOnAfterwards()
    {
        using var before = new WithoutHistory();
        using var after = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        var calls = operations.OfType<SqlOperation>().Select(o => o.Sql).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EquivalentTo(new[]
            {
                """CALL public.grif_enable_versioning('"Test"."Things"');""",
                """CALL public.grif_enable_versioning('"Test"."Animals"');""",
                """CALL public.grif_enable_versioning('"Test"."Tags"');""",
                """CALL public.grif_enable_versioning('"Logs"');""",
            }));
            Assert.That(operations.SkipWhile(o => o is not SqlOperation).All(o => o is SqlOperation), Is.True, "the calls come last");
            Assert.That(operations.OfType<CreateTableOperation>().Count(), Is.EqualTo(4));
            Assert.That(differ.HasDifferences(Relational(before), Relational(after)), Is.True);
            Assert.That(differ.HasDifferences(Relational(after), Relational(after)), Is.False);
        });
    }

    [Test]
    public void Differ_DroppingHistoryTables_TurnsVersioningOffFirst()
    {
        using var before = new WithHistory();
        using var after = new WithoutHistory();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        Assert.Multiple(() =>
        {
            Assert.That(operations.Take(4).Select(o => ((SqlOperation)o).Sql), Is.EquivalentTo(new[]
            {
                """CALL public.grif_disable_versioning('"Test"."Things"');""",
                """CALL public.grif_disable_versioning('"Test"."Animals"');""",
                """CALL public.grif_disable_versioning('"Test"."Tags"');""",
                """CALL public.grif_disable_versioning('"Logs"');""",
            }));
            Assert.That(operations.Skip(4).OfType<SqlOperation>(), Is.Empty);
            Assert.That(operations.OfType<DropTableOperation>().Count(), Is.EqualTo(4));
        });
    }

    [Test]
    public void Differ_DroppingTablesWithTheirHistory_LeavesTheTriggersToGoWithThem()
    {
        using var before = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(before.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), null);

        Assert.That(operations.OfType<SqlOperation>(), Is.Empty);
        Assert.That(operations.OfType<DropTableOperation>().Count(), Is.EqualTo(8));
    }

    [Test]
    public void Differ_FromNothing_CreatesEveryTable_ThenTurnsVersioningOn()
    {
        using var after = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(null, Relational(after));

        Assert.Multiple(() =>
        {
            Assert.That(operations.OfType<CreateTableOperation>().Count(), Is.EqualTo(8));
            Assert.That(operations.TakeLast(4).All(o => o is SqlOperation), Is.True);
            Assert.That(operations.OfType<SqlOperation>().Count(), Is.EqualTo(4));
        });
    }

    // dotnet ef builds its services the same way, from the startup assembly.
    [Test]
    public void DesignTimeServices_ScaffoldWithTheHistoryDiffer()
    {
        using var context = Context(DatabaseProvider.Postgres);
        var assembly = typeof(DesignTimeServices).Assembly;

#pragma warning disable EF1001 // Internal EF Core API usage: what dotnet ef itself calls.
        var services = new Microsoft.EntityFrameworkCore.Design.Internal.DesignTimeServicesBuilder(
            assembly, assembly, new Microsoft.EntityFrameworkCore.Design.Internal.OperationReporter(null), []).Build(context);
#pragma warning restore EF1001

        Assert.That(services.GetService(typeof(IMigrationsModelDiffer)), Is.InstanceOf<HistoryMigrationsModelDiffer>());
    }

    private static IRelationalModel Relational(DbContext context) => context.GetService<IDesignTimeModel>().Model.GetRelationalModel();

    public enum Kind { Small, Large }

    public class Thing
    {
        public int A { get; set; }
        public int B { get; set; }
        public int? Count { get; set; }
        public decimal Price { get; set; }
        public decimal Rate { get; set; }
        public string Code { get; set; } = "";
        public Kind Kind { get; set; }
        public DateTime When { get; set; }
        public bool Flag { get; set; }
    }

    public class Animal
    {
        public int AnimalID { get; set; }
        public string Name { get; set; } = "";
    }

    public class Dog : Animal
    {
        public bool Bark { get; set; }
    }

    public class Log
    {
        public string Line { get; set; } = "";
    }

    public class Report
    {
        public int Total { get; set; }
    }

    /// <summary>A model with each kind of entity: one context type per model, as EF caches a model per type.</summary>
    private abstract class HistoryTestContext(bool history) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseNpgsql("Host=unused;Database=unused");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Thing>(b =>
            {
                b.ToTable("Things", "Test");
                b.HasKey(e => new { e.A, e.B });
                b.Property(e => e.A).ValueGeneratedOnAdd();
                b.Property(e => e.Price).HasPrecision(9, 2);
                b.Property(e => e.Rate).HasPrecision(5);
                b.Property(e => e.Code).HasMaxLength(4).IsFixedLength().IsUnicode(false).UseCollation("C");
                b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(8);
                b.Property(e => e.When).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
                b.Property(e => e.Flag).HasDefaultValue(true);
            });
            modelBuilder.Entity<Animal>(b => b.ToTable("Animals", "Test"));
            modelBuilder.Entity<Dog>();
            modelBuilder.SharedTypeEntity<Dictionary<string, object>>("Tag", b =>
            {
                b.ToTable("Tags", "Test");
                b.IndexerProperty<int>("TagID");
                b.HasKey("TagID");
            });
            // No schema: the search path's.
            modelBuilder.Entity<Log>(b => b.ToTable("Logs").HasNoKey());
            modelBuilder.Entity<Report>(b => b.ToView("Reports", "Test").HasNoKey());

            if (history)
                modelBuilder.AddHistoryTables(modelBuilder.Model.GetEntityTypes().ToList());
        }
    }

    private sealed class WithHistory() : HistoryTestContext(true);

    private sealed class WithoutHistory() : HistoryTestContext(false);
}
