using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Historical.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using static GrifballWebApp.Historical.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// The website databases' results imported: forfeits as forfeited season matches (reconciled run after run), and a
/// recorded game's grabs kept on its players' lines. Against the fake SQLite context, as HistoricalImportServiceUnitTests.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalImportForfeitsUnitTests : ApplicationDbContextTests
{
    private const string SeasonKey = "winter-league-2008/Stats";
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    private static ArchiveSeason Season() => Entry("Winter League 2008", "halo3", "AGLA", false,
        Player("Church", team: "Red Team"), Player("Tex", team: "Blue Team"), Player("Sarge", team: "Gold Team"));

    private static ArchiveForfeitInput Forfeit(int row, string winner, string loser, bool both = false, string season = SeasonKey) => new()
    {
        SeasonSourceId = season,
        SourceKey = $"db/Matches#{row}",
        Winner = winner,
        Loser = loser,
        BothForfeited = both,
        PlayedAt = new DateTime(2008, 1, row, 0, 0, 0, DateTimeKind.Utc),
    };

    private static HistoricalArchiveInput Input(params ArchiveForfeitInput[] forfeits)
        => new() { Seasons = new UnifiedArchive { Seasons = [Season()] }, Forfeits = [.. forfeits] };

    private async Task<List<(string? Home, string? Away, SeasonMatchResult? HomeResult, SeasonMatchResult? AwayResult)>> Matches()
        => (await Context.SeasonMatches.AsNoTracking().OrderBy(m => m.SeasonMatchID)
            .Select(m => new { Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName, m.HomeTeamResult, m.AwayTeamResult })
            .ToListAsync())
            .Select(m => ((string?)m.Home, (string?)m.Away, m.HomeTeamResult, m.AwayTeamResult)).ToList();

    [Test]
    public async Task Import_AForfeit_IsAForfeitedMatchTheWinnerWon_AndADoubleForfeitNobodyWon()
    {
        var report = await _service.Import(Input(Forfeit(1, "Red Team", "Blue Team"), Forfeit(2, "Gold Team", "Red Team", both: true)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await Matches(), Is.EqualTo(new[]
            {
                ((string?)"Red Team", (string?)"Blue Team", (SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Forfeit),
                ("Gold Team", "Red Team", SeasonMatchResult.Forfeit, SeasonMatchResult.Forfeit),
            }));
            Assert.That(await Context.SeasonMatches.Select(m => m.ScheduledTime).FirstAsync(), Is.EqualTo(new DateTime(2008, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(report.SeasonMatchesCreated, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_AgainWithTheSameForfeits_ChangesNothing_AndOneNoLongerListedGoes()
    {
        await _service.Import(Input(Forfeit(1, "Red Team", "Blue Team"), Forfeit(2, "Gold Team", "Red Team")));
        var first = await Context.SeasonMatches.Select(m => m.SeasonMatchID).OrderBy(id => id).FirstAsync();
        Context.ChangeTracker.Clear();

        var again = await _service.Import(Input(Forfeit(1, "Red Team", "Blue Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.SeasonMatchesCreated, again.SeasonMatchesRemoved), Is.EqualTo((0, 1)));
            Assert.That(await Context.SeasonMatches.Select(m => m.SeasonMatchID).ToListAsync(), Is.EqualTo(new[] { first }), "The same match kept, not made again");
        }
    }

    [Test]
    public async Task Import_AForfeitOfATeamTheSeasonHasnt_OrOfNoSeason_IsLeftOut_AndReported()
    {
        var report = await _service.Import(Input(Forfeit(1, "Red Team", "Nobody"), Forfeit(2, "Red Team", "red team"), Forfeit(3, "Red Team", "Blue Team", season: "nope/Stats")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
            Assert.That(report.Warnings, Does.Contain("winter-league-2008/Stats: 2 forfeit(s) naming a team that is none of the season's, left out: db/Matches#1, db/Matches#2"));
            Assert.That(report.Skipped, Does.Contain("Forfeit db/Matches#3: no season with the key nope/Stats in the archive"));
        }
    }

    [Test]
    public async Task Import_ARecordedGamesGrabs_AreKeptOnItsPlayersLines()
    {
        var game = new RecordedGameInput
        {
            SeasonSourceId = SeasonKey,
            SourceKey = "db/Matches#15",
            GameVersion = "halo3",
            Teams = [new RecordedTeamInput { Side = 1, Name = "Red Team", Outcome = Outcomes.Won, Score = 7 },
                     new RecordedTeamInput { Side = 2, Name = "Blue Team", Outcome = Outcomes.Lost, Score = 2 }],
            Players = [new RecordedPlayerInput { Gamertag = "Church", Side = 1, Kills = 52, Deaths = 40, Goals = 3, Grabs = 6 },
                       new RecordedPlayerInput { Gamertag = "Tex", Side = 2, Kills = 40, Deaths = 52, Goals = 1 }],
        };

        var report = await _service.Import(HistoricalImportUnitTestData.Input(Season(), [game]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await Context.RecordedGameLines.OrderBy(p => p.Gamertag).Select(p => p.Grabs).ToListAsync(), Is.EqualTo(new int?[] { 6, null }));
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.EqualTo(1), "The game is a series of its own");
        }
    }
}
