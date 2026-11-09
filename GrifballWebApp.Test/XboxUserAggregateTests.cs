using GrifballWebApp.Database.Queries;
using GrifballWebApp.Database;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// An Xbox account and its past gamertags as one aggregate (#334): made by <see cref="XboxUser.FromXbox"/>, its past gamertags
/// recorded, widened and removed only through it (<see cref="XboxUser.Renamed"/>, <see cref="XboxUser.RecordPastGamertag"/>,
/// <see cref="XboxUser.RemovePastGamertag"/>), which refuse what no history holds.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class XboxUserAggregateTests
{
    private static DateTime Utc(int year, int month = 1) => new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void FromXbox_KeepsTheAccount_AndRefusesABlankGamertag()
    {
        var xbox = XboxUser.FromXbox(42, "Shadow S16");

        Assert.Multiple(() =>
        {
            Assert.That((xbox.XboxUserID, xbox.Gamertag), Is.EqualTo((42L, "Shadow S16")));
            Assert.That(xbox.PastGamertags, Is.Empty);
            Assert.That(xbox.LastRenamedAt, Is.Null);
            Assert.Throws<ArgumentException>(() => XboxUser.FromXbox(42, " "));
            Assert.Throws<ArgumentNullException>(() => XboxUser.FromXbox(42, null!));
        });
    }

    [Test]
    public void Renamed_RecordsTheOldGamertag_FromItsLastChangeUntilNow()
    {
        var xbox = XboxUser.FromXbox(1, "Old  Name");
        ((IAuditStamp)xbox).ModifiedAt = Utc(2020); // as the audit interceptor stamped its last save

        xbox.Renamed("New Name", Utc(2024));

        var past = xbox.PastGamertags.Single();
        Assert.Multiple(() =>
        {
            Assert.That(xbox.Gamertag, Is.EqualTo("New Name"));
            Assert.That((past.XboxUserID, past.Gamertag, past.NormalizedGamertag, past.FirstSeen, past.LastSeen, past.Source),
                Is.EqualTo((1L, "Old Name", "OLD NAME", (DateTime?)Utc(2020), (DateTime?)Utc(2024), GamertagHistorySource.RenameDetected)));
            Assert.That(past.XboxUser, Is.SameAs(xbox));
            Assert.That(xbox.LastRenamedAt, Is.EqualTo(Utc(2024)));
        });
    }

    /// <summary>The row's last change (the wall clock) after the rename's time (the caller's clock, e.g. another pod's or a
    /// fake one): the old gamertag is still recorded, dated from the rename's time.</summary>
    [Test]
    public void Renamed_ModifiedAfterTheRename_StillRecordsTheOldGamertag()
    {
        var xbox = XboxUser.FromXbox(1, "Old Name");
        ((IAuditStamp)xbox).ModifiedAt = Utc(2025);

        xbox.Renamed("New Name", Utc(2024));

        var past = xbox.PastGamertags.Single();
        Assert.Multiple(() =>
        {
            Assert.That(xbox.Gamertag, Is.EqualTo("New Name"));
            Assert.That((past.Gamertag, past.FirstSeen, past.LastSeen), Is.EqualTo(("Old Name", (DateTime?)Utc(2024), (DateTime?)Utc(2024))));
        });
    }

    [Test]
    public void Renamed_NeverStored_HasNoStart_AndASpellingChangeIsNoRename()
    {
        var unsaved = XboxUser.FromXbox(1, "Name");
        unsaved.Renamed("Other", Utc(2024));
        var respelled = XboxUser.FromXbox(2, "shadow s16");
        respelled.Renamed("Shadow  S16", Utc(2024));

        Assert.Multiple(() =>
        {
            Assert.That(unsaved.PastGamertags.Single().FirstSeen, Is.Null, "Never saved: when it was first used is unknown");
            Assert.That(respelled.Gamertag, Is.EqualTo("Shadow  S16"), "Xbox's spelling");
            Assert.That(respelled.PastGamertags, Is.Empty, "The same gamertag, spelled differently");
            Assert.Throws<ArgumentException>(() => respelled.Renamed("", Utc(2024)));
        });
    }

    [Test]
    public void RecordPastGamertag_AddsOnce_AndWidensTheDates_OfAGamertagItHas()
    {
        var xbox = XboxUser.FromXbox(1, "Now");

        var results = new[]
        {
            xbox.RecordPastGamertag(" Old  Tag ", Utc(2019, 6), Utc(2020), GamertagHistorySource.Manual, " WL 2019 sheet "),
            xbox.RecordPastGamertag("old tag", Utc(2018), Utc(2019), GamertagHistorySource.RosterMatch),
            xbox.RecordPastGamertag("OLD TAG", null, Utc(2021), GamertagHistorySource.RenameDetected, "  "),
        };

        var past = xbox.PastGamertags.Single();
        Assert.Multiple(() =>
        {
            Assert.That(results, Is.All.EqualTo(RecordPastGamertagResult.Recorded));
            Assert.That((past.Gamertag, past.NormalizedGamertag, past.FirstSeen, past.LastSeen, past.Source),
                Is.EqualTo(("Old Tag", "OLD TAG", (DateTime?)Utc(2018), (DateTime?)Utc(2021), GamertagHistorySource.Manual)), "Widened; the source it was first known by");
            Assert.That(past.Note, Is.EqualTo("WL 2019 sheet"), "A blank note leaves the note");
        });
    }

    [TestCase(null, null, 2019, 2020, 2019, 2020)]
    [TestCase(2019, 2020, null, null, 2019, 2020)]
    [TestCase(2018, 2021, 2019, 2020, 2018, 2021)]
    [TestCase(2019, 2020, 2018, 2021, 2018, 2021)]
    [TestCase(null, 2020, null, 2021, null, 2021)]
    [TestCase(2019, null, 2018, null, 2018, null)]
    public void RecordPastGamertag_Again_WidensToCoverBoth(int? firstSeen, int? lastSeen, int? newFirst, int? newLast, int? widenedFirst, int? widenedLast)
    {
        DateTime? At(int? year) => year is null ? null : Utc(year.Value);
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Tag", At(firstSeen), At(lastSeen), GamertagHistorySource.Manual);

        xbox.RecordPastGamertag("Tag", At(newFirst), At(newLast), GamertagHistorySource.Manual);

        var past = xbox.PastGamertags.Single();
        Assert.That((past.FirstSeen, past.LastSeen), Is.EqualTo((At(widenedFirst), At(widenedLast))));
    }

    [Test]
    public void LastRenamedAt_IsTheLatestEnd_OfThePastGamertagsWithOne()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Undated", null, null, GamertagHistorySource.Manual);
        Assert.That(xbox.LastRenamedAt, Is.Null, "No past gamertag has an end");

        xbox.RecordPastGamertag("Older", null, Utc(2019), GamertagHistorySource.Manual);
        xbox.RecordPastGamertag("Newer", Utc(2020), Utc(2022), GamertagHistorySource.Manual);

        Assert.That(xbox.LastRenamedAt, Is.EqualTo(Utc(2022)));
    }

    [Test]
    public void RecordPastGamertag_SeenAfterItsLastSighting_NeverStartsAfterItEnds()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Tag", null, Utc(2020), GamertagHistorySource.RenameDetected);

        xbox.RecordPastGamertag("Tag", Utc(2022), null, GamertagHistorySource.Manual);

        var past = xbox.PastGamertags.Single();
        Assert.That((past.FirstSeen, past.LastSeen), Is.EqualTo(((DateTime?)Utc(2020), (DateTime?)Utc(2020))),
            "The end, which matching reads, stays; the start is no later");
    }

    [Test]
    public void RecordPastGamertag_RefusesABlankGamertag_AndDatesOutOfOrder_AndCutsALongNote()
    {
        var xbox = XboxUser.FromXbox(1, "Now");

        Assert.Multiple(() =>
        {
            Assert.That(xbox.RecordPastGamertag(" ", null, null, GamertagHistorySource.Manual), Is.EqualTo(RecordPastGamertagResult.NoGamertag));
            Assert.That(xbox.RecordPastGamertag("Tag", Utc(2021), Utc(2020), GamertagHistorySource.Manual), Is.EqualTo(RecordPastGamertagResult.DatesOutOfOrder));
            Assert.That(xbox.PastGamertags, Is.Empty);
            Assert.That(xbox.RecordPastGamertag("Tag", null, null, GamertagHistorySource.Manual, new string('n', 250)), Is.EqualTo(RecordPastGamertagResult.Recorded));
            Assert.That(xbox.PastGamertags.Single().Note, Has.Length.EqualTo(XboxGamertagHistory.MaxNoteLength));
        });
    }

    [Test]
    public void RemovePastGamertag_TakesOutOnlyOneAddedByHand()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Manual", null, null, GamertagHistorySource.Manual);
        xbox.RecordPastGamertag("Seen", null, null, GamertagHistorySource.RenameDetected);
        var manual = xbox.PastGamertags.First();
        var seen = xbox.PastGamertags.Last();
        manual.XboxGamertagHistoryID = 10;
        seen.XboxGamertagHistoryID = 11;

        Assert.Multiple(() =>
        {
            Assert.That((manual.IsRemovable, seen.IsRemovable), Is.EqualTo((true, false)));
            Assert.That(xbox.RemovePastGamertag(99), Is.EqualTo(RemovePastGamertagResult.NotFound));
            Assert.That(xbox.RemovePastGamertag(11), Is.EqualTo(RemovePastGamertagResult.NotManual));
            Assert.That(xbox.RemovePastGamertag(10), Is.EqualTo(RemovePastGamertagResult.Removed));
            Assert.That(xbox.PastGamertags, Is.EqualTo(new[] { seen }));
        });
    }

    [TestCase(null, null, true)]
    [TestCase(2020, null, true)]
    [TestCase(null, 2019, true)]
    [TestCase(2020, 2020, true)]
    [TestCase(2020, 2019, true)]
    [TestCase(2020, 2021, false)]
    public void WasHeldAt_OnlyItsEndBoundsIt(int? lastSeen, int? from, bool held)
    {
        var past = new XboxGamertagHistory(1, "Tag", firstSeen: Utc(2030), lastSeen: lastSeen is null ? null : Utc(lastSeen.Value));

        Assert.That(past.WasHeldAt(from is null ? null : Utc(from.Value)), Is.EqualTo(held));
    }
}

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
    private static DateTime Utc(int year) => new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

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
        Assert.That(loaded.LastRenamedAt, Is.EqualTo(Utc(2021)));
        Assert.That(loaded.RemovePastGamertag(manualId), Is.EqualTo(RemovePastGamertagResult.Removed));
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

        Assert.Multiple(async () =>
        {
            Assert.That(await Refused(Utc(2021), Utc(2020)), Is.True, "First seen after it was last seen");
            Assert.That(await Refused(null, null, (GamertagHistorySource)0), Is.True, "No source");
            Assert.That(await Refused(null, null, (GamertagHistorySource)9), Is.True, "A source the model doesn't know");

            Assert.That(await Refused(Utc(2020), Utc(2020)), Is.False);
            Assert.That(await Refused(Utc(2021), null, GamertagHistorySource.HistoricalImport), Is.False);
            Assert.That(await Refused(null, Utc(2020), GamertagHistorySource.RosterMatch), Is.False);
        });
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
            Is.EquivalentTo(new[] { "Undated", "Until 2021" }));

        // Removable: only one added by hand
        var removable = await _context.XboxUsers.PastGamertags().Where(XboxGamertagHistory.Removable).Select(h => h.Gamertag).ToListAsync();
        var loadedRemovable = (await _context.XboxUsers.PastGamertags().ToListAsync()).Where(h => h.IsRemovable).Select(h => h.Gamertag);
        Assert.That(removable, Is.EquivalentTo(loadedRemovable));
        Assert.That(removable, Is.EquivalentTo(new[] { "Undated", "Until 2019" }));
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
        Assert.Multiple(() =>
        {
            Assert.That(after["In order"], Is.EqualTo(((DateTime?)Utc(2019), (DateTime?)Utc(2020), GamertagHistorySource.RenameDetected)), "Legal: kept");
            Assert.That(after["Undated"], Is.EqualTo(((DateTime?)null, (DateTime?)null, GamertagHistorySource.Manual)), "Legal: kept");
            Assert.That(after["Backwards"], Is.EqualTo(((DateTime?)Utc(2020), (DateTime?)Utc(2020), GamertagHistorySource.Manual)),
                "Starts as it ends: the end, which matching reads, stays");
            Assert.That(after["No source"], Is.EqualTo(((DateTime?)null, (DateTime?)Utc(2020), GamertagHistorySource.Manual)), "Known by hand");
            Assert.That(after["Unknown source"].Source, Is.EqualTo(GamertagHistorySource.Manual));
        });
        Assert.That(XboxGamertagHistoryConfiguration.KnownSource, Is.EqualTo("Source IN (1, 2, 3, 4)"));
    }
}
