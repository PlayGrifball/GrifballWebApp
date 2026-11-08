using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>
/// A VOD and its team links as one aggregate (#320): made by <see cref="Vod.Found"/>, changed only by its methods, which
/// refuse the moves no VOD makes (the matcher changing one a sysadmin decided, a link without a season).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class VodAggregateTests
{
    private static readonly DateTime Published = new(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Found_WaitsToBeMatched_WithItsTextCutToWhatARowHolds()
    {
        var vod = Vod.Found("abcdefghijk", "  " + new string('t', 300) + "  ", new string('d', 6000), Published, true, 600, "UC");

        Assert.Multiple(() =>
        {
            Assert.That(vod.MatchStatus, Is.EqualTo(VodMatchStatus.NeedsReview));
            Assert.That(vod.Title, Is.EqualTo(new string('t', Vod.TitleMaxLength)));
            Assert.That(vod.Description, Has.Length.EqualTo(Vod.DescriptionMaxLength));
            Assert.That((vod.PublishedAt, vod.PublishedAtIsApproximate, vod.DurationSeconds, vod.ChannelID), Is.EqualTo((Published, true, (int?)600, "UC")));
            Assert.That((vod.SeasonID, vod.SeasonMatchID, vod.MatchNote), Is.EqualTo(((int?)null, (int?)null, (string?)null)));
            Assert.That(vod.Teams, Is.Empty);
            Assert.That(vod.IsVisible, Is.False);
            Assert.That(vod.IsUndecided, Is.True);
            Assert.That(Vod.Found("abcdefghijk", "Title", null, Published, false, null, "UC").Description, Is.Null);
            Assert.Throws<ArgumentException>(() => Vod.Found("abcdefghijk", " ", null, Published, false, null, "UC"), "A VOD has a title");
            Assert.Throws<ArgumentException>(() => Vod.Found("", "Title", null, Published, false, null, "UC"), "And a video");
        });
    }

    [Test]
    public void Refresh_GainsWhatTheVodLacked_AndLosesNothing()
    {
        var vod = Vod.Found("abcdefghijk", "Old title", null, Published, true, null, "UC");
        var exact = Published.AddHours(5);

        var changed = vod.Refresh("New title", "A description", exact, false, 900);
        var again = vod.Refresh("New title", "A description", exact.AddDays(1), false, 1200);
        var nothing = vod.Refresh(null, null, null, true, null);

        Assert.Multiple(() =>
        {
            Assert.That(changed, Is.True);
            Assert.That((vod.Title, vod.Description, vod.PublishedAt, vod.PublishedAtIsApproximate, vod.DurationSeconds),
                Is.EqualTo(("New title", "A description", exact, false, (int?)900)), "The exact date, once; the length it lacked, once");
            Assert.That(again, Is.False, "An exact date and a known length stay");
            Assert.That(nothing, Is.False);
        });
    }

    [Test]
    public void Rematch_LinksOrSuggests_AndSetsTheTeams()
    {
        var vod = Vod.Seed("abcdefghijk", "Alpha vs Bravo", Published, VodMatchStatus.NeedsReview, teamIDs: [1, 3]);

        vod.Rematch(VodMatchStatus.Auto, 7, 70, [1, 2, 2], new string('n', 600));

        Assert.Multiple(() =>
        {
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)7, (int?)70)));
            Assert.That(vod.MatchNote, Has.Length.EqualTo(Vod.MatchNoteMaxLength));
            Assert.That(vod.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { 1, 2 }), "A team gone is taken off, a new one added once");
            Assert.That(vod.IsVisible, Is.True);
        });

        vod.Rematch(VodMatchStatus.NeedsReview, null, null, [], "Nothing recognised");
        Assert.Multiple(() =>
        {
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.SeasonMatchID, vod.MatchNote), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null, (int?)null, "Nothing recognised")));
            Assert.That(vod.Teams, Is.Empty);
        });
    }

    [Test]
    public void Rematch_RefusesTheMovesTheMatcherNeverMakes()
    {
        Vod Undecided() => Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Auto, 7);

        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidOperationException>(() => Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Confirmed, 7).Rematch(VodMatchStatus.Auto, 7, null, [], "n"),
                "A sysadmin's link stays");
            Assert.Throws<InvalidOperationException>(() => Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Dismissed).Rematch(VodMatchStatus.NeedsReview, null, null, [], "n"),
                "A dismissed VOD is never suggested again");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.Confirmed, 7, null, [], "n"), "Only a sysadmin confirms");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.Dismissed, 7, null, [], "n"), "Only a sysadmin dismisses");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.Auto, null, null, [], "n"), "A linked VOD has a season");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.NeedsReview, null, 70, [], "n"), "A match is of a season");
        });
    }

    [Test]
    public void Confirm_LinksAnyVod_AndDismiss_HidesIt()
    {
        var vod = Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Dismissed, 7, teamIDs: [1]);

        vod.Confirm(8, 80, [2, 3]);

        Assert.Multiple(() =>
        {
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.SeasonMatchID), Is.EqualTo((VodMatchStatus.Confirmed, (int?)8, (int?)80)), "Even a dismissed VOD");
            Assert.That(vod.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { 2, 3 }));
            Assert.That((vod.IsVisible, vod.IsUndecided), Is.EqualTo((true, false)));
        });

        vod.Dismiss();
        Assert.Multiple(() =>
        {
            Assert.That(vod.MatchStatus, Is.EqualTo(VodMatchStatus.Dismissed));
            Assert.That((vod.IsVisible, vod.IsUndecided), Is.EqualTo((false, false)));
            Assert.That(vod.Teams.Select(t => t.TeamID), Is.EquivalentTo(new[] { 2, 3 }), "Its links stay as they were");
        });
    }
}

/// <summary>
/// The database's half of a VOD's state (#320): it refuses a linked VOD without a season and a match without one, and the
/// VodAggregate migration settles the rows from before, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodAggregateDatabaseTests
{
    private static readonly DateTime Published = new(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc);

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task The_database_refuses_a_link_without_a_season()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var (season, match) = (seeded.Season.SeasonID, seeded.SeasonMatch.SeasonMatchID);
        var n = 0;

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(VodMatchStatus status, int? seasonID, int? seasonMatchID)
        {
            await using var ctx = _context.NewContextLike();
            var entry = ctx.Vods.Add(Vod.Seed($"video{n++:D6}", "Title", Published, VodMatchStatus.NeedsReview));
            entry.Property(v => v.MatchStatus).CurrentValue = status;
            entry.Property(v => v.SeasonID).CurrentValue = seasonID;
            entry.Property(v => v.SeasonMatchID).CurrentValue = seasonMatchID;
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
            Assert.That(await Refused(VodMatchStatus.Auto, null, null), Is.True, "Linked by the matcher, to nothing");
            Assert.That(await Refused(VodMatchStatus.Confirmed, null, null), Is.True, "Linked by a sysadmin, to nothing");
            Assert.That(await Refused(VodMatchStatus.NeedsReview, null, match), Is.True, "A match without its season");
            Assert.That(await Refused((VodMatchStatus)9, season, null), Is.True, "A status the model doesn't know");

            Assert.That(await Refused(VodMatchStatus.Auto, season, match), Is.False);
            Assert.That(await Refused(VodMatchStatus.Confirmed, season, null), Is.False);
            Assert.That(await Refused(VodMatchStatus.NeedsReview, null, null), Is.False);
            Assert.That(await Refused(VodMatchStatus.Dismissed, null, null), Is.False);
        });
    }

    [Test]
    public async Task Complete_in_a_query_agrees_with_IsComplete_on_a_loaded_vod()
    {
        // Every shape of the two details Refresh can fill in, and one that Refresh completes
        var vods = new[]
        {
            Vod.Found("cmplvideo01", "Both", null, Published, false, 600, "UC"),
            Vod.Found("cmplvideo02", "No length", null, Published, false, null, "UC"),
            Vod.Found("cmplvideo03", "Approximate date", null, Published, true, 600, "UC"),
            Vod.Found("cmplvideo04", "Neither", null, Published, true, null, "UC"),
            Vod.Found("cmplvideo05", "Refreshed", null, Published, true, null, "UC"),
        };
        Assert.That(vods[4].Refresh(null, null, Published.AddHours(3), false, 900), Is.True);
        _context.Vods.AddRange(vods);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var complete = await _context.Vods.AsNoTracking().Where(v => v.YouTubeVideoID.StartsWith("cmplvideo")).Where(Vod.Complete).Select(v => v.YouTubeVideoID).ToListAsync();
        var loaded = await _context.Vods.AsNoTracking().Where(v => v.YouTubeVideoID.StartsWith("cmplvideo")).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(complete, Is.EquivalentTo(new[] { "cmplvideo01", "cmplvideo05" }));
            Assert.That(loaded.Where(v => v.IsComplete).Select(v => v.YouTubeVideoID), Is.EquivalentTo(complete), "The query and the loaded VOD agree");
            Assert.That(loaded.Select(v => Vod.Complete.Compile()(v)), Is.EqualTo(loaded.Select(v => v.IsComplete)));
        });
    }

    [Test]
    public async Task Relinking_removes_the_links_gone_through_the_root()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var vod = Vod.Seed("relinkvideo", "Title", Published, VodMatchStatus.Auto, seeded.Season.SeasonID, teamIDs: [seeded.Home.TeamID, seeded.Away.TeamID]);
        _context.Vods.Add(vod);
        await _context.SaveChangesAsync();

        vod.Rematch(VodMatchStatus.NeedsReview, seeded.Season.SeasonID, null, [seeded.Away.TeamID], "One team");
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        Assert.That(await _context.VodTeams.Where(t => t.VodID == vod.VodID).Select(t => t.TeamID).ToListAsync(), Is.EqualTo(new[] { seeded.Away.TeamID }));
    }

    [Test]
    public async Task The_migration_settles_the_rows_from_before_and_keeps_the_legal_ones()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_VodAggregate"))) - 1]);

        var seeded = await SeasonMatchTestData.SeedAsync(_context);
        var (season, match) = (seeded.Season.SeasonID, seeded.SeasonMatch.SeasonMatchID);

        // Rows as the old model's public setters could leave them
        var rows = new Dictionary<string, Vod>();
        void Row(string name, VodMatchStatus status, int? seasonID, int? seasonMatchID)
        {
            var vod = Vod.Seed($"video{rows.Count:D6}", name, Published, VodMatchStatus.NeedsReview);
            var entry = _context.Vods.Add(vod);
            entry.Property(v => v.MatchStatus).CurrentValue = status;
            entry.Property(v => v.SeasonID).CurrentValue = seasonID;
            entry.Property(v => v.SeasonMatchID).CurrentValue = seasonMatchID;
            rows[name] = vod;
        }
        Row("linked", VodMatchStatus.Auto, season, match);
        Row("confirmed", VodMatchStatus.Confirmed, season, null);
        Row("unsure", VodMatchStatus.NeedsReview, null, null);
        Row("dismissed", VodMatchStatus.Dismissed, null, null);
        Row("a match without its season", VodMatchStatus.Confirmed, null, match);
        Row("linked to nothing", VodMatchStatus.Auto, null, null);
        Row("confirmed as nothing", VodMatchStatus.Confirmed, null, null);
        Row("an unknown status", (VodMatchStatus)9, season, null);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.Vods.AsNoTracking().ToDictionaryAsync(v => v.VodID);
        (VodMatchStatus, int?, int?) Of(string name)
        {
            var v = after[rows[name].VodID];
            return (v.MatchStatus, v.SeasonID, v.SeasonMatchID);
        }
        Assert.Multiple(() =>
        {
            Assert.That(Of("linked"), Is.EqualTo((VodMatchStatus.Auto, (int?)season, (int?)match)), "Legal: kept");
            Assert.That(Of("confirmed"), Is.EqualTo((VodMatchStatus.Confirmed, (int?)season, (int?)null)));
            Assert.That(Of("unsure"), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null, (int?)null)));
            Assert.That(Of("dismissed"), Is.EqualTo((VodMatchStatus.Dismissed, (int?)null, (int?)null)));
            Assert.That(Of("a match without its season"), Is.EqualTo((VodMatchStatus.Confirmed, (int?)season, (int?)match)), "The match's season");
            Assert.That(Of("linked to nothing"), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null, (int?)null)), "Back to review");
            Assert.That(Of("confirmed as nothing"), Is.EqualTo((VodMatchStatus.Dismissed, (int?)null, (int?)null)), "The sysadmin's decision kept: hidden, never matched again");
            Assert.That(after[rows["confirmed as nothing"].VodID].MatchNote, Is.EqualTo("Dismissed by the VodAggregate migration: confirmed without a season (a Late League VOD)"));
            Assert.That(after[rows["linked to nothing"].VodID].MatchNote, Is.Null, "An Auto was no decision: back to review as it was");
            Assert.That(Of("an unknown status"), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)season, (int?)null)), "Back to review");
        });
    }
}
