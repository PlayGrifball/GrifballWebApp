using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Historical.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using static GrifballWebApp.Historical.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// A schedule's matches of a season whose results are lost (<see cref="ArchiveScheduledMatchInput"/>): dated season matches with
/// no result, reconciled run after run. Against the fake SQLite context, as HistoricalImportForfeitsUnitTests.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalImportScheduledUnitTests : ApplicationDbContextTests
{
    private const string SeasonKey = "winter-league-2020/Stats";
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System, Substitute.For<IUserMergeService>());
    }

    private static ArchiveSeason Season() => Entry("Winter League 2020", "reach", "AGLA", false,
        Player("Church", team: "Red Team"), Player("Tex", team: "Blue Team"), Player("Sarge", team: "Gold Team"));

    private static ArchiveScheduledMatchInput Scheduled(int day, string home, string away) => new()
    {
        SeasonSourceId = SeasonKey,
        SourceKey = $"wl20-reach/Full Schedule@2020-01-{day:00}",
        Home = home,
        Away = away,
        PlayedAt = new DateTime(2020, 1, day, 1, 0, 0, DateTimeKind.Utc),
    };

    private static HistoricalArchiveInput Input(params ArchiveScheduledMatchInput[] matches)
        => new() { Seasons = new UnifiedArchive { Seasons = [Season()] }, ScheduledMatches = [.. matches] };

    private async Task<List<(string? Home, string? Away, DateTime? At, SeasonMatchResult? HomeResult, SeasonMatchResult? AwayResult)>> Matches()
        => (await Context.SeasonMatches.AsNoTracking().OrderBy(m => m.SeasonMatchID)
            .Select(m => new { Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName, m.ScheduledTime, m.HomeTeamResult, m.AwayTeamResult })
            .ToListAsync())
            .Select(m => ((string?)m.Home, (string?)m.Away, m.ScheduledTime, m.HomeTeamResult, m.AwayTeamResult)).ToList();

    [Test]
    public async Task Import_AScheduledMatch_IsADatedMatchWithNoResult()
    {
        var report = await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team"), Scheduled(6, "Gold Team", "Red Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await Matches(), Is.EqualTo(new[]
            {
                ((string?)"Red Team", (string?)"Blue Team", (DateTime?)new DateTime(2020, 1, 5, 1, 0, 0, DateTimeKind.Utc), (SeasonMatchResult?)null, (SeasonMatchResult?)null),
                ("Gold Team", "Red Team", new DateTime(2020, 1, 6, 1, 0, 0, DateTimeKind.Utc), null, null),
            }));
            Assert.That(report.SeasonMatchesCreated, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_AgainWithTheSameMatches_ChangesNothing_AndOneNoLongerListedGoes()
    {
        await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team"), Scheduled(6, "Gold Team", "Red Team")));
        var first = await Context.SeasonMatches.Select(m => m.SeasonMatchID).OrderBy(id => id).FirstAsync();
        Context.ChangeTracker.Clear();

        var again = await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.SeasonMatchesCreated, again.SeasonMatchesRemoved), Is.EqualTo((0, 1)));
            Assert.That(await Context.SeasonMatches.Select(m => m.SeasonMatchID).ToListAsync(), Is.EqualTo(new[] { first }), "The same match kept, not made again");
        }
    }

    [Test]
    public async Task Import_AScheduledMatchOfATeamTheSeasonHasnt_OrOfNoSeason_IsLeftOut_AndReported()
    {
        var other = new ArchiveScheduledMatchInput { SeasonSourceId = "nothing/Stats", SourceKey = "x@1", Home = "Red Team", Away = "Blue Team", PlayedAt = DateTime.UnixEpoch };

        var report = await _service.Import(Input(Scheduled(5, "Red Team", "Nobody"), Scheduled(6, "Nobody", "Red Team"), Scheduled(7, "Red Team", "Red Team"), other));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
            Assert.That(report.Warnings, Does.Contain($"{SeasonKey}: 3 scheduled match(es) naming a team that is none of the season's, left out: " +
                "wl20-reach/Full Schedule@2020-01-05, wl20-reach/Full Schedule@2020-01-06, wl20-reach/Full Schedule@2020-01-07"), "A team playing itself too");
            Assert.That(report.Skipped, Does.Contain("Scheduled match x@1: no season with the key nothing/Stats in the archive"));
        }
    }

    [Test]
    public async Task Import_AMatchMovedToAnotherDay_OrHomeAndAwaySwapped_IsMadeAgain_TheOldOneGone()
    {
        await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team")));
        Context.ChangeTracker.Clear();

        var again = await _service.Import(Input(Scheduled(6, "Red Team", "Blue Team"), Scheduled(5, "Blue Team", "Red Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.SeasonMatchesCreated, again.SeasonMatchesRemoved), Is.EqualTo((2, 1)));
            Assert.That((await Matches()).Select(m => (m.Home, m.Away, m.At)), Is.EquivalentTo(new[]
            {
                ((string?)"Red Team", (string?)"Blue Team", (DateTime?)new DateTime(2020, 1, 6, 1, 0, 0, DateTimeKind.Utc)),
                ("Blue Team", "Red Team", new DateTime(2020, 1, 5, 1, 0, 0, DateTimeKind.Utc)),
            }));
        }
    }

    /// <summary>A per-game tab's game of Church's Red Team (side 1, the winner) and Tex's Blue Team.</summary>
    private static RecordedGameInput Game(int row, DateTime? playedAt) => new()
    {
        SeasonSourceId = SeasonKey,
        SourceKey = $"winter-league-2020/HR_MatchH#{row}",
        GameVersion = "reach",
        PlayedAt = playedAt,
        Teams = [new RecordedTeamInput { Side = 1, Outcome = Outcomes.Won }, new RecordedTeamInput { Side = 2, Outcome = Outcomes.Lost }],
        Players = [new RecordedPlayerInput { Gamertag = "Church", Side = 1, Kills = 5 }, new RecordedPlayerInput { Gamertag = "Tex", Side = 2, Kills = 3 }],
    };

    private static HistoricalArchiveInput WithGames(params RecordedGameInput[] games)
        => new() { Seasons = new UnifiedArchive { Seasons = [Season()] }, RecordedGames = [.. games] };

    [Test]
    public async Task Import_AgainWithItsGamesDated_LinksThemAgain_AsOneDatedSeries()
    {
        // #603: a schedule now dates games an earlier import linked undated, each its own series
        await _service.Import(WithGames(Game(1, null), Game(2, null)));
        Context.ChangeTracker.Clear();
        Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(2), "Undated: a series each");
        var day = new DateTime(2020, 1, 6, 1, 0, 0, DateTimeKind.Utc);

        await _service.Import(WithGames(Game(1, day), Game(2, day.AddMinutes(20))));

        var matches = await Context.SeasonMatches.AsNoTracking().Include(m => m.SeriesGames).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches.Select(m => (m.ScheduledTime, m.SeriesGames.Count)), Is.EqualTo(new[] { ((DateTime?)day, 2) }), "The day's two games are one series, on its day");
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(2), "The same games, not new ones");
        }
    }

    [Test]
    public async Task Import_ASeasonItsScheduleCovers_WithNoScheduledMatchNow_LosesItsOldOnes()
    {
        // Its results found since: every meeting dates a game, none is a match of its own
        await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team")));
        Context.ChangeTracker.Clear();

        var again = await _service.Import(new HistoricalArchiveInput { Seasons = new UnifiedArchive { Seasons = [Season()] }, ScheduledSeasons = [SeasonKey, "nothing/Stats"] });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(again.SeasonMatchesRemoved, Is.EqualTo(1));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
        }
    }
}
