using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace GrifballWebApp.Test;

/// <summary>
/// A table with rows that becomes temporal, on both databases, by the SQL a scaffolded migration runs:
/// each provider's differ and its migrations SQL generator (on Postgres, RowHistoryMigrationsSqlGenerator,
/// as UseGrifballDatabase registers it for dotnet ef and the app: it versions a table once an AddColumn
/// gives it both period columns). The rows it had are current from the beginning of time,
/// DateTime.MinValue: SQL Server's 0001-01-01, Postgres's -infinity. A row written after has the
/// transaction's time.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TemporalConversionTests
{
    private GrifballContext _context;
    private string _connectionString;

    // Postgres: a database with the periods extension, which keeps the history, as the Helm chart's
    // database.history gives it (SQL Server: any database; its tables can be temporal).
    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContextWithHistory();
        _connectionString = _context.Database.GetConnectionString()!;
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private const string PeriodStart = "PeriodStart";
    private const string PeriodEnd = "PeriodEnd";

    [Test]
    public async Task RowsATableHasWhenItBecomesTemporal_AreCurrentFromTheBeginningOfTime()
    {
        await using (var plain = new PlainNotes(Options<PlainNotes>()))
        {
            await Migrate(plain, null);
            plain.Notes.AddRange(new Note { Text = "a" }, new Note { Text = "b" });
            await plain.SaveChangesAsync();
        }
        var beforeConversion = await DatabaseNow();
        await Task.Delay(20);

        await using var temporal = new TemporalNotes(Options<TemporalNotes>());
        using (var plain = new PlainNotes(Options<PlainNotes>()))
            await Migrate(temporal, plain);

        var asOfBefore = await temporal.Notes.TemporalAsOf(beforeConversion).OrderBy(n => n.Text).Select(n => n.Text).ToListAsync();
        var starts = await temporal.Notes.Select(n => EF.Property<DateTime>(n, PeriodStart)).Distinct().ToListAsync();

        var beforeSave = await DatabaseNow();
        var added = new Note { Text = "c" };
        temporal.Notes.Add(added);
        var a = await temporal.Notes.SingleAsync(n => n.Text == "a");
        a.Text = "a2";
        await temporal.SaveChangesAsync();
        var afterSave = await DatabaseNow();
        var addedStart = (DateTime)temporal.Entry(added).Property(PeriodStart).CurrentValue!;

        var versionsOfA = await temporal.Notes.TemporalAll().Where(n => n.NoteID == a.NoteID)
            .OrderBy(n => EF.Property<DateTime>(n, PeriodStart))
            .Select(n => new { n.Text, Start = EF.Property<DateTime>(n, PeriodStart), End = EF.Property<DateTime>(n, PeriodEnd) })
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(asOfBefore, Is.EqualTo(new[] { "a", "b" }), "TemporalAsOf a time before the table was temporal");
            Assert.That(starts, Is.EqualTo(new[] { DateTime.MinValue }));
            Assert.That(addedStart, Is.InRange(beforeSave, afterSave), "a row written after has the transaction's time");
            Assert.That(versionsOfA.Select(v => v.Text), Is.EqualTo(new[] { "a", "a2" }));
            Assert.That(versionsOfA[0].Start, Is.EqualTo(DateTime.MinValue));
            Assert.That(versionsOfA[0].End, Is.EqualTo(versionsOfA[1].Start));
            Assert.That(versionsOfA[1].Start, Is.InRange(beforeSave, afterSave));
            Assert.That(versionsOfA[1].End, Is.EqualTo(DateTime.MaxValue));
        });
    }

    [Test]
    public async Task ATableCreatedTemporal_HasItsRowsTimes()
    {
        await using var temporal = new TemporalNotes(Options<TemporalNotes>());
        await Migrate(temporal, null);

        var beforeSave = await DatabaseNow();
        var note = new Note { Text = "a" };
        temporal.Notes.Add(note);
        await temporal.SaveChangesAsync();
        var afterSave = await DatabaseNow();

        var start = await temporal.Notes.Select(n => EF.Property<DateTime>(n, PeriodStart)).SingleAsync();
        var asOfBefore = await temporal.Notes.TemporalAsOf(beforeSave).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(start, Is.InRange(beforeSave, afterSave));
            Assert.That(asOfBefore, Is.Empty);
        });
    }

    private DbContextOptions<T> Options<T>() where T : DbContext
    {
        return new DbContextOptionsBuilder<T>().UseGrifballDatabase(TestDatabase.Provider, _connectionString).Options;
    }

    /// <summary>The database's clock, which stamps the periods.</summary>
    private async Task<DateTime> DatabaseNow()
    {
        var sql = TestDatabase.Provider == DatabaseProvider.Postgres
            ? """SELECT clock_timestamp() AS "Value" """
            : "SELECT SYSUTCDATETIME() AS [Value]";
        var now = (await _context.Database.SqlQueryRaw<DateTime>(sql).ToListAsync()).Single();
        return DateTime.SpecifyKind(now, DateTimeKind.Utc);
    }

    /// <summary>
    /// Takes the database from <paramref name="from"/>'s model to <paramref name="to"/>'s (from nothing
    /// when null) with the operations and SQL a scaffolded migration would have.
    /// </summary>
    private static async Task Migrate(DbContext to, DbContext? from)
    {
        var differ = to.GetService<IMigrationsModelDiffer>();
        var target = to.GetService<IDesignTimeModel>().Model;
        var operations = differ.GetDifferences(from?.GetService<IDesignTimeModel>().Model.GetRelationalModel(), target.GetRelationalModel());
        var commands = to.GetService<IMigrationsSqlGenerator>().Generate(operations, target);
        await to.GetService<IMigrationCommandExecutor>().ExecuteNonQueryAsync(commands, to.GetService<IRelationalConnection>());
    }

    public class Note
    {
        public int NoteID { get; set; }
        public string Text { get; set; } = "";
    }

    private abstract class NotesContext(DbContextOptions options, bool temporal) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // As GrifballContext keeps history: SQL Server's temporal tables; Postgres's period columns,
            // which the migrations SQL generator versions with the periods extension.
            if (temporal && Database.IsSqlServer())
                modelBuilder.Entity<Note>().ToTable("Notes", "Test", tb => tb.IsTemporal());
            else
                modelBuilder.Entity<Note>().ToTable("Notes", "Test");
            if (temporal && Database.IsNpgsql())
                modelBuilder.AddSystemTimePeriods(modelBuilder.Model.GetEntityTypes().ToList());
        }
    }

    // One context type per model: EF caches a model per type.
    private sealed class PlainNotes(DbContextOptions<PlainNotes> options) : NotesContext(options, false);

    private sealed class TemporalNotes(DbContextOptions<TemporalNotes> options) : NotesContext(options, true);
}
