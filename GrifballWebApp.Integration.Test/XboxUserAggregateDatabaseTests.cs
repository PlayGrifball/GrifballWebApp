using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The account's past gamertags in the database: saved and removed through the account, refused by the constraints when
/// out of order or of a source the model doesn't know, the rule in a query agreeing with the loaded rows, and the migration
/// settling the rows from before. Alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxUserAggregateDatabaseTests
{
    private static readonly string[] HeldIn2020 = ["Undated", "Until 2021"];
    private static readonly string[] RemovableGamertags = ["Undated", "Until 2019"];

    private static DateTime Utc(int year) => new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task PastGamertags_AreSavedAndRemoved_ThroughTheAccount()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Manual", Utc(2019), Utc(2020), GamertagHistorySource.Manual, "by hand");
        xbox.RecordPastGamertag("Seen", null, Utc(2021), GamertagHistorySource.RenameDetected);
        _context.XboxUsers.Add(xbox);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.XboxUsers.WithPastGamertags().SingleAsync();
        var manualId = loaded.PastGamertags.Single(h => h.Source == GamertagHistorySource.Manual).XboxGamertagHistoryID;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loaded.LastRenamedAt, Is.EqualTo(Utc(2021)));
            Assert.That(loaded.RemovePastGamertag(manualId), Is.EqualTo(RemovePastGamertagResult.Removed));
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        Assert.That((await _context.XboxUsers.PastGamertags().ToListAsync()).Select(h => (h.XboxUserID, h.Gamertag, h.Note)),
            Is.EqualTo(new[] { (1L, "Seen", (string?)null) }), "Taken out of the account: deleted");
    }

    [Test]
    public async Task The_database_refuses_dates_out_of_order_and_a_source_the_model_doesnt_know()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(1, "Now"));
        await _context.SaveChangesAsync();

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(DateTime? firstSeen, DateTime? lastSeen, GamertagHistorySource source = GamertagHistorySource.Manual)
        {
            await using var ctx = _context.NewContextLike();
            var past = new XboxGamertagHistory(1, $"Tag {Guid.NewGuid():N}"[..20]);
            var entry = ctx.Set<XboxGamertagHistory>().Add(past);
            entry.Property(h => h.FirstSeen).CurrentValue = firstSeen;
            entry.Property(h => h.LastSeen).CurrentValue = lastSeen;
            entry.Property(h => h.Source).CurrentValue = source;
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(Utc(2021), Utc(2020)), Is.True, "First seen after it was last seen");
            Assert.That(await Refused(null, null, (GamertagHistorySource)0), Is.True, "No source");
            Assert.That(await Refused(null, null, (GamertagHistorySource)9), Is.True, "A source the model doesn't know");

            Assert.That(await Refused(Utc(2020), Utc(2020)), Is.False);
            Assert.That(await Refused(Utc(2021), null, GamertagHistorySource.HistoricalImport), Is.False);
            Assert.That(await Refused(null, Utc(2020), GamertagHistorySource.RosterMatch), Is.False);
        }
    }

    [Test]
    public async Task The_rule_in_a_query_agrees_with_the_loaded_rows()
    {
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "One"), XboxUser.FromXbox(2, "Two"));
        _context.Set<XboxGamertagHistory>().AddRange(
            new XboxGamertagHistory(1, "Undated"),
            new XboxGamertagHistory(1, "Until 2019", lastSeen: Utc(2019)),
            new XboxGamertagHistory(2, "Until 2021", firstSeen: Utc(2018), lastSeen: Utc(2021), source: GamertagHistorySource.RosterMatch));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        foreach (var from in new DateTime?[] { null, Utc(2020), Utc(2022) })
        {
            var queried = await _context.XboxUsers.PastGamertags().Where(XboxGamertagHistory.HeldAt(from)).Select(h => h.Gamertag).ToListAsync();
            var loaded = (await _context.XboxUsers.PastGamertags().ToListAsync()).Where(h => h.WasHeldAt(from)).Select(h => h.Gamertag);
            Assert.That(queried, Is.EquivalentTo(loaded), $"From {from}");
        }
        Assert.That(await _context.XboxUsers.PastGamertags().Where(XboxGamertagHistory.HeldAt(Utc(2020))).Select(h => h.Gamertag).ToListAsync(),
            Is.EquivalentTo(HeldIn2020));

        // Removable: only one added by hand
        var removable = await _context.XboxUsers.PastGamertags().Where(XboxGamertagHistory.Removable).Select(h => h.Gamertag).ToListAsync();
        var loadedRemovable = (await _context.XboxUsers.PastGamertags().ToListAsync()).Where(h => h.IsRemovable).Select(h => h.Gamertag);
        Assert.That(removable, Is.EquivalentTo(loadedRemovable));
        Assert.That(removable, Is.EquivalentTo(RemovableGamertags));
    }

    [Test]
    public async Task The_migration_settles_the_rows_from_before_and_keeps_the_legal_ones()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_XboxUserAggregate"))) - 1]);

        // Rows as the old model's public setters could leave them
        _context.XboxUsers.Add(XboxUser.FromXbox(1, "Now"));
        var rows = new Dictionary<string, XboxGamertagHistory>();
        void Row(string name, DateTime? firstSeen, DateTime? lastSeen, GamertagHistorySource source = GamertagHistorySource.Manual)
        {
            var past = new XboxGamertagHistory(1, name);
            var entry = _context.Set<XboxGamertagHistory>().Add(past);
            entry.Property(h => h.FirstSeen).CurrentValue = firstSeen;
            entry.Property(h => h.LastSeen).CurrentValue = lastSeen;
            entry.Property(h => h.Source).CurrentValue = source;
            rows[name] = past;
        }
        Row("In order", Utc(2019), Utc(2020), GamertagHistorySource.RenameDetected);
        Row("Undated", null, null);
        Row("Backwards", Utc(2021), Utc(2020));
        Row("No source", null, Utc(2020), (GamertagHistorySource)0);
        Row("Unknown source", null, null, (GamertagHistorySource)9);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.XboxUsers.PastGamertags().ToDictionaryAsync(h => h.Gamertag, h => (h.FirstSeen, h.LastSeen, h.Source));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(after["In order"], Is.EqualTo(((DateTime?)Utc(2019), (DateTime?)Utc(2020), GamertagHistorySource.RenameDetected)), "Legal: kept");
            Assert.That(after["Undated"], Is.EqualTo(((DateTime?)null, (DateTime?)null, GamertagHistorySource.Manual)), "Legal: kept");
            Assert.That(after["Backwards"], Is.EqualTo(((DateTime?)Utc(2020), (DateTime?)Utc(2020), GamertagHistorySource.Manual)),
                "Starts as it ends: the end, which matching reads, stays");
            Assert.That(after["No source"], Is.EqualTo(((DateTime?)null, (DateTime?)Utc(2020), GamertagHistorySource.Manual)), "Known by hand");
            Assert.That(after["Unknown source"].Source, Is.EqualTo(GamertagHistorySource.Manual));
        }
        Assert.That(XboxGamertagHistoryConfiguration.KnownSource, Is.EqualTo("Source IN (1, 2, 3, 4)"));
    }
}
