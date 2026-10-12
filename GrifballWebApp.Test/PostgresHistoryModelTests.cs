using GrifballWebApp.Database;
using GrifballWebApp.Migrations.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
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
    public void Postgres_TablesWithHistory_HaveSqlServersPeriodColumns()
    {
        using var postgres = Context(DatabaseProvider.Postgres);
        using var sqlServer = Context(DatabaseProvider.SqlServer);

        var season = postgres.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Database.Models.Season))!;
        var periodStart = season.FindProperty(PostgresHistory.PeriodStart)!;
        var periodEnd = season.FindProperty(PostgresHistory.PeriodEnd)!;
        var sqlServerSeason = sqlServer.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Database.Models.Season))!;

        Assert.Multiple(() =>
        {
            Assert.That(periodStart.IsShadowProperty(), Is.True);
            Assert.That(periodStart.ValueGenerated, Is.EqualTo(ValueGenerated.OnAddOrUpdate));
            Assert.That(periodStart.GetColumnType(), Is.EqualTo("timestamp with time zone"));
            Assert.That(periodStart.GetDefaultValueSql(), Is.EqualTo("transaction_timestamp()"));
            Assert.That(periodEnd.IsShadowProperty(), Is.True);
            Assert.That(periodEnd.ValueGenerated, Is.EqualTo(ValueGenerated.OnAddOrUpdate));
            Assert.That(periodEnd.GetColumnType(), Is.EqualTo("timestamp with time zone"));
            Assert.That(periodEnd.GetComputedColumnSql(), Is.EqualTo(PostgresHistory.EndOfTime));
            Assert.That(periodEnd.GetIsStored(), Is.True);
            // The names SQL Server's temporal tables give them.
            Assert.That(new[] { sqlServerSeason.GetPeriodStartPropertyName(), sqlServerSeason.GetPeriodEndPropertyName() },
                Is.EqualTo(new[] { PostgresHistory.PeriodStart, PostgresHistory.PeriodEnd }));
            Assert.That(postgres.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Database.Models.MatchReschedule))!.FindProperty(PostgresHistory.PeriodStart), Is.Null,
                "MatchReschedules isn't temporal on SQL Server either");
        });
    }

    [Test]
    public void FindHistoryEntityType_IsTheHistoryTableOfTheTable()
    {
        using var postgres = Context(DatabaseProvider.Postgres);
        using var withHistory = new WithHistory();
        var model = postgres.Model;

        Assert.Multiple(() =>
        {
            Assert.That(model.FindEntityType(typeof(Database.Models.Season))!.FindHistoryEntityType()?.Name, Is.EqualTo("GrifballWebApp.Database.Models.Season.History"));
            Assert.That(model.FindEntityType(typeof(Database.Models.MatchReschedule))!.FindHistoryEntityType(), Is.Null);
            Assert.That(withHistory.Model.FindEntityType(typeof(Dog))!.FindHistoryEntityType()?.GetTableName(), Is.EqualTo("AnimalsHistory"));
            Assert.That(withHistory.Model.FindEntityType(typeof(Report))!.FindHistoryEntityType(), Is.Null, "a view");
        });
    }

    [Test]
    public void HistoryTable_CopiesEachColumn_NullableWithoutKeyOrDefault()
    {
        using var context = new WithHistory();
        var history = (IEntityType)context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Thing))!.FindHistoryEntityType()!;
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
            Assert.That(Column("PeriodEnd").GetColumnType(), Is.EqualTo("timestamp with time zone"));
        });
    }

    [Test]
    public void HistoryTable_OfAHierarchy_HasEveryTypesColumns()
    {
        using var context = new WithHistory();

        var columns = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Animal))!.FindHistoryEntityType()!.GetProperties().Select(p => p.Name);

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
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType("Tag.History"), Is.Not.Null);
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Log))!.FindHistoryEntityType()!.GetIndexes().Single().Properties.Select(p => p.Name), Is.EqualTo(new[] { "PeriodEnd" }));
            Assert.That(context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Report))!.FindProperty(PostgresHistory.PeriodStart), Is.Null, "a view has no history");
        });
    }

    [Test]
    public void Differ_CreatingHistoryTables_CreatesTheFunctionsFirst_AndTurnsVersioningOnAfterwards()
    {
        using var before = new WithoutHistory();
        using var after = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        var calls = operations.Skip(1).OfType<SqlOperation>().Select(o => o.Sql).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(((SqlOperation)operations[0]).Sql, Is.EqualTo(PostgresHistoryFunctions.Versions[^1].Create), "the functions first");
            Assert.That(calls, Is.EquivalentTo(new[]
            {
                """CALL public.grif_enable_versioning('"Test"."Things"');""",
                """CALL public.grif_enable_versioning('"Test"."Animals"');""",
                """CALL public.grif_enable_versioning('"Test"."Tags"');""",
                """CALL public.grif_enable_versioning('"Logs"');""",
            }));
            Assert.That(operations.Skip(1).SkipWhile(o => o is not SqlOperation).All(o => o is SqlOperation), Is.True, "the calls come last");
            Assert.That(operations.OfType<CreateTableOperation>().Count(), Is.EqualTo(4));
            Assert.That(operations.OfType<AlterDatabaseOperation>(), Is.Empty, "the functions' version alone is no AlterDatabase");
            Assert.That(differ.HasDifferences(Relational(before), Relational(after)), Is.True);
            Assert.That(differ.HasDifferences(Relational(after), Relational(after)), Is.False);
        });
    }

    [Test]
    public void Differ_KeepsTheRestOfAnAlterDatabase()
    {
        using var before = new WithoutHistory();
        using var after = new WithHistoryAndCollation();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var alter = differ.GetDifferences(Relational(before), Relational(after)).OfType<AlterDatabaseOperation>().Single();

        Assert.Multiple(() =>
        {
            Assert.That(alter.GetAnnotations().Select(a => a.Name), Is.EqualTo(new[] { "Npgsql:CollationDefinition:ci" }));
            Assert.That(alter.OldDatabase.GetAnnotations(), Is.Empty);
        });
    }

    [Test]
    public void Differ_TablesGettingHistory_GiveTheirRowsTheBeginningOfTime_ThenTheModelsDefault()
    {
        using var before = new WithoutHistory();
        using var after = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after)).ToList();

        var adds = operations.OfType<AddColumnOperation>().Where(o => o.Name == PostgresHistory.PeriodStart).ToList();
        Assert.That(adds.Select(a => a.Table), Is.EquivalentTo(new[] { "Things", "Animals", "Tags", "Logs" }));
        Assert.Multiple(() =>
        {
            foreach (var add in adds)
            {
                Assert.That(add.DefaultValueSql, Is.EqualTo(PostgresHistory.BeginningOfTime), add.Table);
                var alter = operations[operations.IndexOf(add) + 1] as AlterColumnOperation;
                Assert.That(alter, Is.Not.Null, $"{add.Table}: altered right after");
                Assert.That((alter!.Schema, alter.Table, alter.Name), Is.EqualTo((add.Schema, add.Table, add.Name)));
                Assert.That(alter.DefaultValueSql, Is.EqualTo(PostgresHistory.PeriodStartDefault));
                Assert.That((alter.ClrType, alter.ColumnType, alter.IsNullable), Is.EqualTo((typeof(DateTime), "timestamp with time zone", false)));
                Assert.That(alter.OldColumn.DefaultValueSql, Is.EqualTo(PostgresHistory.BeginningOfTime));
                Assert.That((alter.OldColumn.ClrType, alter.OldColumn.ColumnType, alter.OldColumn.IsNullable), Is.EqualTo((typeof(DateTime), "timestamp with time zone", false)));
            }
            Assert.That(operations.OfType<AlterColumnOperation>().Count(), Is.EqualTo(4));
        });
    }

    [Test]
    public void Differ_LeavesOtherAddedColumnsAlone_EvenOnesNamedPeriodStart()
    {
        using var before = new WithoutHistory();
        using var after = new WithPeriodStartColumn();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        Assert.Multiple(() =>
        {
            Assert.That(operations.OfType<AddColumnOperation>().Single().DefaultValueSql, Is.EqualTo("now()"), "a table without history");
            Assert.That(operations.OfType<AlterColumnOperation>(), Is.Empty);
            Assert.That(operations.OfType<SqlOperation>(), Is.Empty, "no history, no functions");
        });
    }

    [Test]
    public void Differ_DroppingHistoryTables_TurnsVersioningOffFirst_AndDropsTheFunctionsLast()
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
            Assert.That(operations.Skip(4).OfType<SqlOperation>().Single(), Is.SameAs(operations[^1]));
            Assert.That(((SqlOperation)operations[^1]).Sql, Is.EqualTo(PostgresHistoryFunctions.Versions[^1].Drop));
            Assert.That(operations.OfType<DropTableOperation>().Count(), Is.EqualTo(4));
        });
    }

    // The triggers go with the tables, but the versioning functions are their own: turned off first.
    [Test]
    public void Differ_DroppingTablesWithTheirHistory_TurnsVersioningOffFirst_TakingTheTablesFunctions()
    {
        using var before = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(before.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), null);

        Assert.Multiple(() =>
        {
            Assert.That(operations.Take(4).Select(o => ((SqlOperation)o).Sql), Is.EquivalentTo(new[]
            {
                """CALL public.grif_disable_versioning('"Test"."Things"');""",
                """CALL public.grif_disable_versioning('"Test"."Animals"');""",
                """CALL public.grif_disable_versioning('"Test"."Tags"');""",
                """CALL public.grif_disable_versioning('"Logs"');""",
            }));
            Assert.That(operations.Skip(4).OfType<SqlOperation>().Select(o => o.Sql), Is.EqualTo(new[] { PostgresHistoryFunctions.Versions[^1].Drop }), "then only the functions, last");
            Assert.That(operations[^1], Is.InstanceOf<SqlOperation>());
            Assert.That(operations.OfType<DropTableOperation>().Count(), Is.EqualTo(8));
        });
    }

    [Test]
    public void Differ_FromNothing_CreatesTheFunctionsAndEveryTable_ThenTurnsVersioningOn()
    {
        using var after = new WithHistory();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(null, Relational(after));

        Assert.Multiple(() =>
        {
            Assert.That(((SqlOperation)operations[0]).Sql, Is.EqualTo(PostgresHistoryFunctions.Versions[^1].Create));
            Assert.That(operations.OfType<CreateTableOperation>().Count(), Is.EqualTo(8));
            Assert.That(operations.TakeLast(4).All(o => o is SqlOperation), Is.True);
            Assert.That(operations.OfType<SqlOperation>().Count(), Is.EqualTo(5));
            // New tables have no rows: the model's default, and nothing to alter.
            Assert.That(operations.OfType<CreateTableOperation>().Single(o => o.Name == "Things").Columns.Single(c => c.Name == PostgresHistory.PeriodStart).DefaultValueSql,
                Is.EqualTo(PostgresHistory.PeriodStartDefault));
            Assert.That(operations.OfType<AddColumnOperation>(), Is.Empty);
            Assert.That(operations.OfType<AlterColumnOperation>(), Is.Empty);
        });
    }

    private static RenameTableOperation Renamed(IEnumerable<MigrationOperation> operations, string name) =>
        operations.OfType<RenameTableOperation>().Single(o => o.Name == name);

    // A table renamed: its history table too, with its index, keeping its rows and triggers. Its versioning
    // function, which names both tables, is written again after both renames.
    [Test]
    public void Differ_RenamingATable_RenamesItsHistoryTable_BothWays()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryRenamed();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var up = differ.GetDifferences(Relational(before), Relational(after)).ToList();
        var down = differ.GetDifferences(Relational(after), Relational(before)).ToList();

        Assert.Multiple(() =>
        {
            foreach (var (operations, from, to) in new[] { (up, "Things", "Stuff"), (down, "Stuff", "Things") })
            {
                Assert.That((Renamed(operations, from).Schema, Renamed(operations, from).NewName, Renamed(operations, from).NewSchema), Is.EqualTo(("Test", to, "Test")));
                var history = Renamed(operations, from + "History");
                Assert.That((history.Schema, history.NewName, history.NewSchema), Is.EqualTo(("Test", to + "History", "Test")));
                var index = operations.OfType<RenameIndexOperation>().Single(o => o.Table == to + "History");
                Assert.That((index.Name, index.NewName), Is.EqualTo(($"IX_{from}History_A_B_PeriodEnd", $"IX_{to}History_A_B_PeriodEnd")));
                Assert.That(operations.OfType<CreateTableOperation>(), Is.Empty);
                Assert.That(operations.OfType<DropTableOperation>(), Is.Empty);
                Assert.That(operations.OfType<CreateIndexOperation>(), Is.Empty);
                Assert.That(operations.OfType<SqlOperation>().Select(o => o.Sql), Is.EqualTo(new[] { $"""CALL public.grif_refresh_versioning('"Test"."{to}"');""" }));
                Assert.That(operations.IndexOf(operations.OfType<SqlOperation>().Single()),
                    Is.EqualTo(Math.Max(operations.IndexOf(Renamed(operations, from)), operations.IndexOf(history)) + 1), "right after the second rename");
            }
        });
    }

    [Test]
    public void Differ_MovingATableToAnotherSchema_MovesItsHistoryTable()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryMoved();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        Assert.Multiple(() =>
        {
            Assert.That(operations.OfType<EnsureSchemaOperation>().Single().Name, Is.EqualTo("Moved"));
            Assert.That((Renamed(operations, "Things").NewName, Renamed(operations, "Things").NewSchema), Is.EqualTo(("Things", "Moved")));
            Assert.That((Renamed(operations, "ThingsHistory").NewName, Renamed(operations, "ThingsHistory").NewSchema), Is.EqualTo(("ThingsHistory", "Moved")));
            Assert.That(operations.OfType<RenameIndexOperation>(), Is.Empty, "the index's name has no schema");
            Assert.That(operations.OfType<CreateTableOperation>(), Is.Empty);
            Assert.That(operations.OfType<DropTableOperation>(), Is.Empty);
            Assert.That(operations.OfType<SqlOperation>().Select(o => o.Sql), Is.EqualTo(new[] { """CALL public.grif_refresh_versioning('"Moved"."Things"');""" }));
        });
    }

    [Test]
    public void Differ_RenamingATable_AndAddingAColumn_DoesBothToItsHistoryTable()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryRenamedWithNote();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(Renamed(operations, "ThingsHistory").NewName, Is.EqualTo("StuffHistory"));
            Assert.That(Renamed(operations, "Things").NewName, Is.EqualTo("Stuff"));
            var adds = operations.OfType<AddColumnOperation>().ToList();
            Assert.That(adds.Select(a => (a.Table, a.Name, a.IsNullable)), Is.EquivalentTo(new[] { ("Stuff", "Note", false), ("StuffHistory", "Note", true) }));
            Assert.That(adds.Min(operations.IndexOf), Is.GreaterThan(operations.IndexOf(Renamed(operations, "ThingsHistory"))), "added to the renamed table");
            Assert.That(operations.OfType<CreateTableOperation>(), Is.Empty);
            Assert.That(operations.OfType<DropTableOperation>(), Is.Empty);
            var refresh = operations.OfType<SqlOperation>().Single();
            Assert.That(refresh.Sql, Is.EqualTo("""CALL public.grif_refresh_versioning('"Test"."Stuff"');"""));
            Assert.That(operations.IndexOf(refresh), Is.EqualTo(adds.Max(operations.IndexOf) + 1), "once, after the last change");
        });
    }

    // What still loses history, on SQL Server too: EF can't tell a table whose entity and name both changed
    // is the same table, so drops it and creates it anew, its history table with it.
    [Test]
    public void Differ_RenamingAnEntityAndItsTable_DropsAndCreatesBoth()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryRelabelled();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        Assert.Multiple(() =>
        {
            Assert.That(operations.OfType<DropTableOperation>().Select(o => o.Name), Is.EquivalentTo(new[] { "Tags", "TagsHistory" }));
            Assert.That(operations.OfType<CreateTableOperation>().Select(o => o.Name), Is.EquivalentTo(new[] { "Labels", "LabelsHistory" }));
            Assert.That(operations.OfType<RenameTableOperation>(), Is.Empty);
            Assert.That(operations.OfType<SqlOperation>().Select(o => o.Sql), Is.EqualTo(new[]
            {
                """CALL public.grif_disable_versioning('"Test"."Tags"');""",
                """CALL public.grif_enable_versioning('"Test"."Labels"');""",
            }));
            Assert.That(operations[0], Is.InstanceOf<SqlOperation>(), "off first");
        });
    }

    private static string Refresh(string table) => $"""CALL public.grif_refresh_versioning('{table}');""";

    // A table's versioning function names the columns its table and history table share: each migration
    // that adds, drops or renames one writes it again, once, right after the last change to either table.
    [Test]
    public void Differ_AddingOrDroppingAColumn_WritesTheVersioningFunctionAgain()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryAndNote();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var up = differ.GetDifferences(Relational(before), Relational(after)).ToList();
        var down = differ.GetDifferences(Relational(after), Relational(before)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(up.OfType<AddColumnOperation>().Select(o => o.Table), Is.EquivalentTo(new[] { "Things", "ThingsHistory" }));
            Assert.That(up.Select(o => (o as SqlOperation)?.Sql).Where(s => s is not null), Is.EqualTo(new[] { Refresh("\"Test\".\"Things\"") }));
            Assert.That(up[^1], Is.InstanceOf<SqlOperation>(), "after both columns");
            Assert.That(down.OfType<DropColumnOperation>().Select(o => o.Table), Is.EquivalentTo(new[] { "Things", "ThingsHistory" }));
            Assert.That(down.Select(o => (o as SqlOperation)?.Sql).Where(s => s is not null), Is.EqualTo(new[] { Refresh("\"Test\".\"Things\"") }));
            Assert.That(down[^1], Is.InstanceOf<SqlOperation>());
        });
    }

    [Test]
    public void Differ_RenamingAColumn_WritesTheVersioningFunctionAgain()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryRecoded();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after)).ToList();

        var renames = operations.OfType<RenameColumnOperation>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(renames.Select(o => (o.Table, o.Name, o.NewName)), Is.EquivalentTo(new[] { ("Things", "Code", "Kode"), ("ThingsHistory", "Code", "Kode") }));
            Assert.That(operations.OfType<SqlOperation>().Select(o => o.Sql), Is.EqualTo(new[] { Refresh("\"Test\".\"Things\"") }));
            Assert.That(operations.IndexOf(operations.OfType<SqlOperation>().Single()), Is.EqualTo(renames.Max(operations.IndexOf) + 1));
        });
    }

    // A column's type: the function's INSERT names it alone, and Postgres plans it again for the new type.
    [Test]
    public void Differ_AlteringAColumn_LeavesTheVersioningFunction()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryLongerCode();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        Assert.That(operations.OfType<AlterColumnOperation>().Select(o => o.Table), Is.EquivalentTo(new[] { "Things", "ThingsHistory" }));
        Assert.That(operations.OfType<SqlOperation>(), Is.Empty);
    }

    [Test]
    public void Differ_ChangingTheColumnsOfATableWithoutHistory_WritesNothing()
    {
        using var before = new WithoutHistory();
        using var after = new WithoutHistoryWithNote();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var operations = differ.GetDifferences(Relational(before), Relational(after));

        Assert.That(operations.OfType<AddColumnOperation>().Single().Table, Is.EqualTo("Things"));
        Assert.That(operations.OfType<SqlOperation>(), Is.Empty);
    }

    // A foreign key's index on the history table too, with PeriodEnd: an AsOf join from the principal
    // finds the dependents' versions by it.
    [Test]
    public void HistoryTable_IndexesEachForeignKey_WithPeriodEnd()
    {
        using var context = new WithHistoryAndOwner();
        var model = context.GetService<IDesignTimeModel>().Model;

        var indexes = model.FindEntityType(typeof(Thing))!.FindHistoryEntityType()!.GetIndexes().Select(i => i.Properties.Select(p => p.Name).ToArray());

        Assert.That(indexes, Is.EquivalentTo(new[] { new[] { "A", "B", "PeriodEnd" }, new[] { "OwnerID", "PeriodEnd" } }));
    }

    [Test]
    public void Differ_AddingAForeignKey_IndexesItInTheHistoryTableToo()
    {
        using var before = new WithHistory();
        using var after = new WithHistoryAndOwner();
        var differ = new HistoryMigrationsModelDiffer(after.GetService<IMigrationsModelDiffer>());

        var up = differ.GetDifferences(Relational(before), Relational(after)).ToList();
        var down = differ.GetDifferences(Relational(after), Relational(before)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(up.OfType<CreateIndexOperation>().Select(o => (o.Table, o.Name, string.Join(",", o.Columns))), Is.EquivalentTo(new[]
            {
                ("Things", "IX_Things_OwnerID", "OwnerID"),
                ("ThingsHistory", "IX_ThingsHistory_OwnerID_PeriodEnd", "OwnerID,PeriodEnd"),
            }));
            Assert.That(up.OfType<SqlOperation>().Select(o => o.Sql), Is.EqualTo(new[] { Refresh("\"Test\".\"Things\"") }));
            Assert.That(down.OfType<DropIndexOperation>().Select(o => (o.Table, o.Name)), Is.EquivalentTo(new[]
            {
                ("Things", "IX_Things_OwnerID"),
                ("ThingsHistory", "IX_ThingsHistory_OwnerID_PeriodEnd"),
            }));
        });
    }

    // Every table's foreign keys, in the app's model: each has its history table's index, bar those another
    // index already starts with (MatchTeams' MatchID, the first column of its key).
    [Test]
    public void Postgres_HistoryTables_IndexTheirTablesForeignKeys()
    {
        using var postgres = Context(DatabaseProvider.Postgres);
        var model = postgres.GetService<IDesignTimeModel>().Model;
        string[] Columns(IEnumerable<IReadOnlyProperty> properties) => [.. properties.Select(p => p.Name)];
        string[][] HistoryIndexes(Type type) => [.. model.FindEntityType(type)!.FindHistoryEntityType()!.GetIndexes().Select(i => Columns(i.Properties))];

        Assert.Multiple(() =>
        {
            foreach (var entityType in model.GetEntityTypes().Where(e => e.BaseType is null && e.FindHistoryEntityType() is not null))
            {
                var store = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());
                var indexes = entityType.FindHistoryEntityType()!.GetIndexes().Select(i => Columns(i.Properties)).ToList();
                foreach (var foreignKey in entityType.GetDerivedTypesInclusive().SelectMany(e => e.GetDeclaredForeignKeys()))
                {
                    var columns = foreignKey.Properties.Select(p => p.GetColumnName(store)).ToArray();
                    Assert.That(indexes.Any(i => i.Take(columns.Length).SequenceEqual(columns!)), Is.True, $"{entityType.DisplayName()}: {string.Join(", ", columns)}");
                }
                Assert.That(indexes.All(i => i[^1] == PostgresHistory.PeriodEnd), Is.True, entityType.DisplayName());
            }
            Assert.That(HistoryIndexes(typeof(Database.Models.Team)), Is.EquivalentTo(new[]
            {
                new[] { "TeamID", "PeriodEnd" }, new[] { "CaptainID", "PeriodEnd" }, new[] { "SeasonID", "PeriodEnd" },
            }));
            Assert.That(HistoryIndexes(typeof(Database.Models.MatchTeam)), Is.EquivalentTo(new[] { new[] { "MatchID", "TeamID", "PeriodEnd" } }));
        });
    }

    private static readonly HistoryFunctionsVersion[] TwoVersions =
    [
        PostgresHistoryFunctions.Versions[0],
        new(2, Create: "CREATE OR REPLACE FUNCTION v2();", Drop: "DROP FUNCTION v2();"),
    ];

    // A function fixed: a new version, which the next migration creates over the old one, and its Down
    // puts back. A change to the model that has-pending-model-changes sees, in EF's own differ too.
    [Test]
    public void Differ_ANewVersionOfTheFunctions_ReplacesThem_AndDownPutsTheOldOnesBack()
    {
        using var v1 = new WithHistory();
        using var v2 = new WithHistoryV2();
        var differ = new HistoryMigrationsModelDiffer(v2.GetService<IMigrationsModelDiffer>(), TwoVersions);

        var up = differ.GetDifferences(Relational(v1), Relational(v2));
        var down = differ.GetDifferences(Relational(v2), Relational(v1));

        Assert.Multiple(() =>
        {
            Assert.That(up.Select(o => (o as SqlOperation)?.Sql), Is.EqualTo(new[] { TwoVersions[1].Create }), "nothing else");
            Assert.That(down.Select(o => (o as SqlOperation)?.Sql), Is.EqualTo(new[] { TwoVersions[0].Create }));
            Assert.That(differ.HasDifferences(Relational(v1), Relational(v2)), Is.True);
            Assert.That(v2.GetService<IMigrationsModelDiffer>().HasDifferences(Relational(v1), Relational(v2)), Is.True, "EF's own differ, which has-pending-model-changes asks");
            Assert.That(differ.HasDifferences(Relational(v2), Relational(v2)), Is.False);
        });
    }

    [Test]
    public void Differ_AnUnknownVersionOfTheFunctions_Throws()
    {
        using var v1 = new WithHistory();
        using var v2 = new WithHistoryV2();
        var differ = new HistoryMigrationsModelDiffer(v2.GetService<IMigrationsModelDiffer>());

        var ex = Assert.Throws<InvalidOperationException>(() => differ.GetDifferences(Relational(v1), Relational(v2)));

        Assert.That(ex!.Message, Does.Contain("no version 2"));
    }

    [Test]
    public void Model_WithHistory_HasTheFunctionsVersion_AtDesignTimeOnly()
    {
        using var withHistory = new WithHistory();
        using var withoutHistory = new WithoutHistory();
        using var sqlServer = Context(DatabaseProvider.SqlServer);
        using var postgres = Context(DatabaseProvider.Postgres);

        Assert.Multiple(() =>
        {
            Assert.That(Relational(withHistory).FindAnnotation(PostgresHistoryFunctions.Annotation)?.Value, Is.EqualTo(PostgresHistoryFunctions.Versions[^1].Number));
            Assert.That(Relational(postgres).FindAnnotation(PostgresHistoryFunctions.Annotation)?.Value, Is.EqualTo(PostgresHistoryFunctions.Versions[^1].Number));
            Assert.That(withHistory.Model.GetRelationalModel().FindAnnotation(PostgresHistoryFunctions.Annotation), Is.Null, "the app's runtime model");
            Assert.That(Relational(withoutHistory).FindAnnotation(PostgresHistoryFunctions.Annotation), Is.Null);
            Assert.That(sqlServer.GetService<IDesignTimeModel>().Model.FindAnnotation(PostgresHistoryFunctions.Annotation), Is.Null);
        });
    }

    [Test]
    public void HistoryFunctionsVersion_HasUnixLineEndings()
    {
        var version = new HistoryFunctionsVersion(9, "a\r\nb", "c\r\nd");

        Assert.That((version.Create, version.Drop), Is.EqualTo(("a\nb", "c\nd")));
    }

    private static IServiceProvider DesignTimeServices(DbContext context)
    {
        var assembly = typeof(DesignTimeServices).Assembly;
#pragma warning disable EF1001 // Internal EF Core API usage: what dotnet ef itself calls.
        return new Microsoft.EntityFrameworkCore.Design.Internal.DesignTimeServicesBuilder(
            assembly, assembly, new Microsoft.EntityFrameworkCore.Design.Internal.OperationReporter(null), []).Build(context);
#pragma warning restore EF1001
    }

    // dotnet ef builds its services the same way, from the startup assembly.
    [Test]
    public void DesignTimeServices_ScaffoldWithTheHistoryDiffer_AndRawSql()
    {
        using var context = Context(DatabaseProvider.Postgres);
        var services = DesignTimeServices(context);

        Assert.That(services.GetService(typeof(IMigrationsModelDiffer)), Is.InstanceOf<HistoryMigrationsModelDiffer>());
        Assert.That(services.GetService(typeof(ICSharpMigrationOperationGenerator)), Is.InstanceOf<RawSqlMigrationOperationGenerator>());
    }

    [Test]
    public void RawSql_WritesSqlOfManyLines_AsARawStringLiteral()
    {
        using var context = Context(DatabaseProvider.Postgres);
        var generator = (ICSharpMigrationOperationGenerator)DesignTimeServices(context).GetService(typeof(ICSharpMigrationOperationGenerator))!;
        var builder = new IndentedStringBuilder();
        var annotated = new SqlOperation { Sql = "SELECT\n3;" };
        annotated.AddAnnotation("Test:Note", 1);

        generator.Generate("migrationBuilder",
        [
            new SqlOperation { Sql = "SELECT 1;" },
            new SqlOperation { Sql = "SELECT\r\n  '\"\"\"\"';\n\nSELECT 2;\n" },
            annotated,
        ], builder);

        Assert.That(builder.ToString().ReplaceLineEndings("\n"), Is.EqualTo(""""""
            migrationBuilder.Sql("SELECT 1;");

            migrationBuilder.Sql("""""
                SELECT
                  '""""';

                SELECT 2;
                """"");

            migrationBuilder.Sql("SELECT\n3;")
                .Annotation("Test:Note", 1);
            """"""));
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

    /// <summary>
    /// A model with each kind of entity, and Things as <paramref name="things"/> in <paramref name="schema"/>,
    /// with a column Note if <paramref name="note"/>: one context type per model, as EF caches a model per
    /// type.
    /// </summary>
    private abstract class HistoryTestContext(bool history, bool periodStartColumn = false, string things = "Things", string schema = "Test",
        bool note = false, int? functions = null, bool collation = false, string tag = "Tag", string tags = "Tags",
        string code = "Code", int codeLength = 4, bool owner = false) : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseNpgsql("Host=unused;Database=unused")
            .ReplaceService<IRelationalAnnotationProvider, PostgresHistoryAnnotationProvider>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Thing>(b =>
            {
                b.ToTable(things, schema);
                b.HasKey(e => new { e.A, e.B });
                b.Property(e => e.A).ValueGeneratedOnAdd();
                b.Property(e => e.Price).HasPrecision(9, 2);
                b.Property(e => e.Rate).HasPrecision(5);
                b.Property(e => e.Code).HasColumnName(code).HasMaxLength(codeLength).IsFixedLength().IsUnicode(false).UseCollation("C");
                b.Property(e => e.Kind).HasConversion<string>().HasMaxLength(8);
                b.Property(e => e.When).HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
                b.Property(e => e.Flag).HasDefaultValue(true);
                if (note)
                    b.Property<string>("Note").IsRequired();
                if (owner)
                    b.HasOne<Animal>().WithMany().HasForeignKey("OwnerID");
            });
            modelBuilder.Entity<Animal>(b => b.ToTable("Animals", "Test"));
            modelBuilder.Entity<Dog>();
            modelBuilder.SharedTypeEntity<Dictionary<string, object>>(tag, b =>
            {
                b.ToTable(tags, "Test");
                b.IndexerProperty<int>("TagID");
                b.HasKey("TagID");
            });
            // No schema: the search path's.
            modelBuilder.Entity<Log>(b => b.ToTable("Logs").HasNoKey());
            modelBuilder.Entity<Report>(b => b.ToView("Reports", "Test").HasNoKey());
            if (collation)
                modelBuilder.HasCollation("ci", locale: "und-u-ks-level2", provider: "icu", deterministic: false);

            if (history)
            {
                modelBuilder.AddHistoryTables(modelBuilder.Model.GetEntityTypes().ToList());
                if (functions is { } version)
                    modelBuilder.HasAnnotation(PostgresHistoryFunctions.Annotation, version);
            }
            else if (periodStartColumn)
                modelBuilder.Entity<Log>().Property<DateTime>(PostgresHistory.PeriodStart).HasDefaultValueSql("now()");
        }
    }

    private sealed class WithHistory() : HistoryTestContext(true);

    private sealed class WithoutHistory() : HistoryTestContext(false);

    /// <summary>No history, but a column of its own called PeriodStart.</summary>
    private sealed class WithPeriodStartColumn() : HistoryTestContext(false, periodStartColumn: true);

    private sealed class WithHistoryRenamed() : HistoryTestContext(true, things: "Stuff");

    private sealed class WithHistoryMoved() : HistoryTestContext(true, schema: "Moved");

    private sealed class WithHistoryRenamedWithNote() : HistoryTestContext(true, things: "Stuff", note: true);

    private sealed class WithHistoryV2() : HistoryTestContext(true, functions: 2);

    private sealed class WithHistoryAndCollation() : HistoryTestContext(true, collation: true);

    private sealed class WithHistoryRelabelled() : HistoryTestContext(true, tag: "Label", tags: "Labels");

    private sealed class WithHistoryAndNote() : HistoryTestContext(true, note: true);

    private sealed class WithoutHistoryWithNote() : HistoryTestContext(false, note: true);

    /// <summary>Things' Code column called Kode.</summary>
    private sealed class WithHistoryRecoded() : HistoryTestContext(true, code: "Kode");

    private sealed class WithHistoryLongerCode() : HistoryTestContext(true, codeLength: 6);

    /// <summary>Things with an owner, an Animal: a foreign key, OwnerID.</summary>
    private sealed class WithHistoryAndOwner() : HistoryTestContext(true, owner: true);
}
