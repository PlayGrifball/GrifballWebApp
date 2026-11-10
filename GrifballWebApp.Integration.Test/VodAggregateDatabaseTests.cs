using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The database's half of a VOD's state (#320): it refuses a linked VOD without a season and a match without one, and the
/// VodAggregate migration settles the rows from before, alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class VodAggregateDatabaseTests
{
    private static readonly string[] CompleteVideoIDs = ["cmplvideo01", "cmplvideo05"];

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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Refused(VodMatchStatus.Auto, null, null), Is.True, "Linked by the matcher, to nothing");
            Assert.That(await Refused(VodMatchStatus.Confirmed, null, null), Is.True, "Linked by a sysadmin, to nothing");
            Assert.That(await Refused(VodMatchStatus.NeedsReview, null, match), Is.True, "A match without its season");
            Assert.That(await Refused((VodMatchStatus)9, season, null), Is.True, "A status the model doesn't know");

            Assert.That(await Refused(VodMatchStatus.Auto, season, match), Is.False);
            Assert.That(await Refused(VodMatchStatus.Confirmed, season, null), Is.False);
            Assert.That(await Refused(VodMatchStatus.NeedsReview, null, null), Is.False);
            Assert.That(await Refused(VodMatchStatus.Dismissed, null, null), Is.False);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(complete, Is.EquivalentTo(CompleteVideoIDs));
            Assert.That(loaded.Where(v => v.IsComplete).Select(v => v.YouTubeVideoID), Is.EquivalentTo(complete), "The query and the loaded VOD agree");
            Assert.That(loaded.Select(v => Vod.Complete.Compile()(v)), Is.EqualTo(loaded.Select(v => v.IsComplete)));
        }
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
        Assert.That(await _context.Set<VodTeam>().Where(t => t.VodID == vod.VodID).Select(t => t.TeamID).ToListAsync(), Is.EqualTo(new[] { seeded.Away.TeamID }));
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
        using (Assert.EnterMultipleScope())
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
        }
    }
}
