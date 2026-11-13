using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GrifballWebApp.Application.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// A season whose import fails (the database refuses a save) against the fake context: that season alone is rolled back
/// and reported as failed, and the run goes on to the next. The save refused is a late one, so there is something to
/// roll back: the stat row of "Lost Player" once its season (named "Broken ...") is written already. So is removing the
/// titles-only season "Winter League 2021". A test whose refusal never comes fails: nothing is reported then.
/// </summary>
[TestFixture]
internal sealed class HistoricalImportFailureUnitTests : ApplicationDbContextTests
{
    private const string Refusal = RefusingSaves.DefaultMessage;
    private const string LostPlayer = "Lost Player";
    private static readonly string[] OnlyWinter = ["Winter League 2019"];
    private static readonly string[] OnlySeason8 = ["Late League Season 8 (Halo 3)"];

    private readonly RefusingSaves _saves = new(context => LostPlayersStatAfterItsSeason(context)
        || context.ChangeTracker.Entries<Season>().Any(e => e.State == EntityState.Deleted && e.Entity.SeasonName == "Winter League 2021"));

    /// <summary>A save adding Lost Player's stat row, its "Broken ..." season (or matchmaking season) saved already.</summary>
    private static bool LostPlayersStatAfterItsSeason(DbContext context)
    {
        var seasonWritten = context.ChangeTracker.Entries<Season>().Any(e => e.State == EntityState.Unchanged && e.Entity.SeasonName.StartsWith("Broken", StringComparison.Ordinal))
            || context.ChangeTracker.Entries<MatchmakingSeason>().Any(e => e.State == EntityState.Unchanged && e.Entity.Name.StartsWith("Broken", StringComparison.Ordinal));
        return seasonWritten && context.ChangeTracker.Entries<PlayerStatTotals>().Any(e => e.State == EntityState.Added && e.Entity.Gamertag == LostPlayer);
    }

    private HistoricalImportService _service = null!;

    protected override IReadOnlyList<IInterceptor> Interceptors => [_saves];

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    [Test]
    public async Task Import_ASeasonThatFails_IsRolledBackAndReported_AndTheOthersAreImported()
    {
        var report = await _service.Import(Input(
            Entry("Broken League 2018", "halo3", "AGLA", false, Player(LostPlayer, kills: 3, team: "Lost Team")),
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10))));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Has.Count.EqualTo(1));
            Assert.That(report.Errors[0], Does.EndWith($": {Refusal}"));
            Assert.That(report.SeasonsCreated, Is.EqualTo(1));
            Assert.That(await Context.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(OnlyWinter));
            // Written in its transaction before the refused save, and rolled back with it
            Assert.That(await Context.Teams.AnyAsync(t => t.TeamName == "Lost Team"), Is.False, "its team");
            Assert.That(await Context.Users.AnyAsync(u => u.DisplayName == LostPlayer), Is.False, "its placeholder");
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1), "only the other season's stat row");
        }
    }

    [Test]
    public async Task Import_AMatchmakingSeasonThatFails_IsRolledBackAndReported_AndTheOthersAreImported()
    {
        var report = await _service.Import(Input(
            Entry("Late League Season 8 (Halo 3)", "halo3", "Late League", false, Player("Shadow S16", kills: 4)),
            Entry("Broken Late League Season 9", "halo3", "Late League", false, Player(LostPlayer, kills: 3))));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Has.Count.EqualTo(1));
            Assert.That(report.Errors[0], Does.EndWith($": {Refusal}"));
            Assert.That(report.MatchmakingSeasonsCreated, Is.EqualTo(1));
            Assert.That(await Context.MatchmakingSeasons.Select(s => s.Name).ToListAsync(), Is.EqualTo(OnlySeason8), "the failed one was written, then rolled back");
            Assert.That(await Context.Users.AnyAsync(u => u.DisplayName == LostPlayer), Is.False, "its placeholder");
            Assert.That(await Context.MatchmakingPlayerStats.CountAsync(), Is.EqualTo(1), "only the other season's stat row");
        }
    }

    [Test]
    public async Task Import_RetiringATitlesOnlySeasonThatFails_IsRolledBackAndReported_AndTheSeasonStays()
    {
        var champions = new ChampionsArchive
        {
            Champions =
            [
                new ArchiveChampionship { Season = "Winter League 2021", Game = "Halo 3", Tier = "Legacy", Team = "Wolves", Captain = "Alpha", Roster = ["Alpha"] },
                new ArchiveChampionship { Season = "Winter League 2021", Game = "Halo 3", Tier = "Rookie", Team = "Cubs", Captain = "Beta", Roster = ["Beta"] },
            ],
        };
        // The champions record alone: a titles-only season of both tiers' titles
        await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive(), Championships = champions });
        Context.ChangeTracker.Clear();
        var titlesOnly = await Context.Seasons.AsNoTracking().SingleAsync(s => s.SeasonName == "Winter League 2021");

        // The titles move to each tier's own season, so the titles-only season is retired: the database refuses that
        var report = await _service.Import(new HistoricalArchiveInput
        {
            Seasons = Input(
                Entry("Winter League 2021 (Legacy)", "halo3", "AGLA", false, Player("Alpha", team: "Wolves")),
                Entry("Winter League 2021 (Rookie)", "halo3", "AGLA", false, Player("Beta", team: "Cubs"))).Seasons,
            Championships = champions,
        });

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.EqualTo(new[] { $"{titlesOnly.HistoricalSourceID}: {Refusal}" }));
            Assert.That(report.SeasonsRetired, Is.Zero);
            Assert.That(await Context.Seasons.AnyAsync(s => s.SeasonID == titlesOnly.SeasonID), Is.True, "rolled back: it stays");
            Assert.That(await Context.Teams.CountAsync(t => t.SeasonID == titlesOnly.SeasonID), Is.EqualTo(2), "with its teams");
        }
    }
}
