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
    private static readonly string?[] RedAndBlue = ["Red Team", "Blue Team"];
    private static readonly string?[] RedAndNone = ["Red Team", null];
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
    public async Task Import_AForfeitWhoseResultChanged_IsMadeAgain_TheOldOneGone()
    {
        await _service.Import(Input(Forfeit(1, "Red Team", "Blue Team"), Forfeit(2, "Gold Team", "Red Team")));
        Context.ChangeTracker.Clear();

        var again = await _service.Import(Input(Forfeit(1, "Red Team", "Blue Team", both: true), Forfeit(2, "Red Team", "Gold Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.SeasonMatchesCreated, again.SeasonMatchesRemoved), Is.EqualTo((2, 2)));
            Assert.That(await Matches(), Is.EquivalentTo(new[]
            {
                ((string?)"Red Team", (string?)"Blue Team", (SeasonMatchResult?)SeasonMatchResult.Forfeit, (SeasonMatchResult?)SeasonMatchResult.Forfeit),
                ("Red Team", "Gold Team", SeasonMatchResult.Won, SeasonMatchResult.Forfeit),
            }));
        }
    }

    [Test]
    public async Task Import_AForfeitOfATeamTheSeasonHasnt_OrOfNoSeason_IsLeftOut_AndReported()
    {
        var report = await _service.Import(Input(Forfeit(1, "Red Team", "Nobody"), Forfeit(2, "Red Team", "red team"), Forfeit(3, "Red Team", "Blue Team", season: "nope/Stats"), Forfeit(4, "Nobody", "Red Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
            Assert.That(report.Warnings, Does.Contain("winter-league-2008/Stats: 3 forfeit(s) naming a team that is none of the season's, left out: db/Matches#1, db/Matches#2, db/Matches#4"));
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

    [Test]
    public async Task Import_AGameWhoseSidesNameTheirTeams_IsBetweenThoseTeams_WhateverTeamsItsPlayersAreOn()
    {
        // Church and Tex both played for Red Team (the season's rows), Tex for Blue Team in this game: the database says so
        var season = Entry("Winter League 2008", "halo3", "AGLA", false, Player("Church", team: "Red Team"), Player("Tex", team: "Red Team"), Player("Tex", team: "Blue Team", stint: 2));
        RecordedGameInput Game(string key, string blue) => new()
        {
            SeasonSourceId = SeasonKey,
            SourceKey = key,
            GameVersion = "halo3",
            Teams = [new RecordedTeamInput { Side = 1, Name = "Red Team", Outcome = Outcomes.Won, NamesItsTeam = true },
                     new RecordedTeamInput { Side = 2, Name = blue, Outcome = Outcomes.Lost, NamesItsTeam = true }],
            Players = [new RecordedPlayerInput { Gamertag = "Church", Side = 1 }, new RecordedPlayerInput { Gamertag = "Tex", Side = 2 }],
        };

        var report = await _service.Import(HistoricalImportUnitTestData.Input(season, [Game("db/Matches#1", "Blue Team"), Game("db/Matches#2", "(Deleted Team)")]));

        var sides = await Context.RecordedGames.AsNoTracking().OrderBy(g => g.SourceKey)
            .Select(g => g.Teams.OrderBy(t => t.Side).Select(t => t.Team == null ? null : t.Team.TeamName).ToList()).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(sides[0], Is.EqualTo(RedAndBlue));
            Assert.That(sides[1], Is.EqualTo(RedAndNone), "A deleted team is no team, not its players' other one");
            Assert.That(report.Warnings, Has.Some.Contains("1 recorded game(s) with a side that is none of the season's teams"));
        }
    }
}
