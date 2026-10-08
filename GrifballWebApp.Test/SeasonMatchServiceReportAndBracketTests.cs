using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// Shared seeding helpers for the SeasonMatch coverage tests (agent C).
/// </summary>
internal static class SeasonMatchSeedC
{
    public static readonly long[] HomeXbox = [101, 102, 103, 104];
    public static readonly long[] AwayXbox = [201, 202, 203, 204];
    public static readonly long[] OtherXbox = [301, 302, 303, 304, 305, 306, 307, 308];

    public record Seeded(Season Season, Team Home, Team Away, SeasonMatch SeasonMatch);

    /// <summary>
    /// Creates a season, two teams with four players each (with xbox accounts), plus a pool of unrelated xbox users,
    /// and one season match between the teams.
    /// </summary>
    public static async Task<Seeded> SeedAsync(GrifballContext context, int bestOf = 3, bool playersHaveXbox = true)
    {
        var season = new Season { SeasonName = "Cov Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        var home = new Team { SeasonID = season.SeasonID, TeamName = "Home Team" };
        var away = new Team { SeasonID = season.SeasonID, TeamName = "Away Team" };
        context.Teams.AddRange(home, away);
        await context.SaveChangesAsync();

        foreach (var id in HomeXbox)
            context.TeamPlayers.Add(new TeamPlayer { TeamID = home.TeamID, User = NewUser(id, playersHaveXbox) });
        foreach (var id in AwayXbox)
            context.TeamPlayers.Add(new TeamPlayer { TeamID = away.TeamID, User = NewUser(id, playersHaveXbox) });
        foreach (var id in OtherXbox)
            context.XboxUsers.Add(new XboxUser { XboxUserID = id, Gamertag = $"GT{id}" });
        if (playersHaveXbox is false)
        {
            foreach (var id in HomeXbox.Concat(AwayXbox))
                context.XboxUsers.Add(new XboxUser { XboxUserID = id, Gamertag = $"GT{id}" });
        }
        await context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = home.TeamID,
            AwayTeamID = away.TeamID,
            BestOf = bestOf,
        };
        context.SeasonMatches.Add(seasonMatch);
        await context.SaveChangesAsync();

        return new Seeded(season, home, away, seasonMatch);
    }

    private static User NewUser(long xboxId, bool withXbox)
    {
        var user = new User { UserName = $"user{xboxId}", DisplayName = $"User {xboxId}" };
        if (withXbox)
            user.XboxUser = new XboxUser { XboxUserID = xboxId, Gamertag = $"GT{xboxId}" };
        return user;
    }

    /// <summary>
    /// Adds an infinite match with two teams (TeamID 0 and 1).
    /// </summary>
    public static async Task<Match> AddMatchAsync(GrifballContext context, DateTime start,
        long[] team0, Outcomes team0Outcome, long[] team1, Outcomes team1Outcome, int team0Score = 5, int team1Score = 3)
    {
        var matchID = Guid.NewGuid();
        var match = new Match
        {
            MatchID = matchID,
            StartTime = start,
            EndTime = start.AddMinutes(10),
            Duration = TimeSpan.FromMinutes(10),
            MatchTeams = new List<MatchTeam>
            {
                new()
                {
                    MatchID = matchID, TeamID = 0, Score = team0Score, Outcome = team0Outcome,
                    MatchParticipants = team0.Select(x => new MatchParticipant { MatchID = matchID, TeamID = 0, XboxUserID = x, Kills = (int)(x % 10), Deaths = 1, Score = (int)(x % 10) }).ToList(),
                },
                new()
                {
                    MatchID = matchID, TeamID = 1, Score = team1Score, Outcome = team1Outcome,
                    MatchParticipants = team1.Select(x => new MatchParticipant { MatchID = matchID, TeamID = 1, XboxUserID = x, Kills = (int)(x % 10), Deaths = 2, Score = 0 }).ToList(),
                },
            },
        };
        context.Matches.Add(match);
        await context.SaveChangesAsync();
        return match;
    }

    public static GrifballContext NewContext(GrifballContext context)
        => new(new DbContextOptionsBuilder<GrifballContext>().UseSqlServer(context.Database.GetConnectionString()).Options);
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchServiceReportMatchCTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private IBracketService _bracketService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _bracketService = Substitute.For<IBracketService>();
        _service = new SeasonMatchService(_context, _dataPullService, _bracketService);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<SeasonMatch> Reload(int id)
    {
        await using var ctx = SeasonMatchSeedC.NewContext(_context);
        return await ctx.SeasonMatches.Include(x => x.MatchLinks).AsNoTracking().SingleAsync(x => x.SeasonMatchID == id);
    }

    [Test]
    public async Task ReportMatch_Throws_When_MatchDoesNotExist_AfterPullingIt()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var matchID = Guid.NewGuid();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, matchID));

        Assert.That(ex!.Message, Is.EqualTo("Match does not exist"));
        await _dataPullService.Received(1).GetAndSaveMatch(matchID);
    }

    [Test]
    public async Task ReportMatch_Throws_When_MatchAlreadyLinked()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);
        _context.MatchLinks.Add(new MatchLink { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        await _context.SaveChangesAsync();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Match is already associated with a season match"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_NoTeamWon()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Tie, SeasonMatchSeedC.AwayXbox, Outcomes.Tie);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("There must be a winner for the match you are reporting"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SeasonMatchDoesNotExist()
    {
        await SeasonMatchSeedC.SeedAsync(_context);
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(99999, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Season match does not exist"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SeasonMatchMissingTeam()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var tbd = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = null, BestOf = 1 };
        _context.SeasonMatches.Add(tbd);
        await _context.SaveChangesAsync();
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(tbd.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Does.Contain("does not have both teams assigned"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_ResultsAlreadyDecided()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        seeded.SeasonMatch.AwayTeamResult = SeasonMatchResult.Won;
        await _context.SaveChangesAsync();
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Results for this match have already been decided"));
    }

    [Test]
    public async Task ReportMatch_FirstGameOfBestOf3_UpdatesScoreWithoutDecidingResult()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 3);
        // Home players on team 1 to verify the home/away detection does not depend on team index
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.AwayXbox, Outcomes.Lost, SeasonMatchSeedC.HomeXbox, Outcomes.Won);

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamScore, Is.EqualTo(1));
            Assert.That(sm.AwayTeamScore, Is.EqualTo(0));
            Assert.That(sm.HomeTeamResult, Is.Null);
            Assert.That(sm.AwayTeamResult, Is.Null);
            Assert.That(sm.MatchLinks.Single().MatchID, Is.EqualTo(match.MatchID));
            Assert.That(sm.MatchLinks.Single().MatchNumber, Is.EqualTo(1));
        });
        _bracketService.DidNotReceiveWithAnyArgs().DetermineNextMatches(default!);
    }

    [Test]
    public async Task ReportMatch_HomeWinsTwo_DecidesHomeWin_AndNumbersGamesByStartTime()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 3);
        var start = new DateTime(2025, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        var early = await SeasonMatchSeedC.AddMatchAsync(_context, start, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);
        var late = await SeasonMatchSeedC.AddMatchAsync(_context, start.AddMinutes(30), SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);

        // Report the later game first; numbering must follow start time, not report order
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, late.MatchID);
        _context.ChangeTracker.Clear();
        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, early.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamScore, Is.EqualTo(2));
            Assert.That(sm.AwayTeamScore, Is.EqualTo(0));
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Loss));
            Assert.That(sm.MatchLinks.Single(x => x.MatchID == early.MatchID).MatchNumber, Is.EqualTo(1));
            Assert.That(sm.MatchLinks.Single(x => x.MatchID == late.MatchID).MatchNumber, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ReportMatch_AwayWinsBestOf1_DecidesAwayWin()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 1);
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Lost, SeasonMatchSeedC.AwayXbox, Outcomes.Won);

        await _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamScore, Is.EqualTo(0));
            Assert.That(sm.AwayTeamScore, Is.EqualTo(1));
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Loss));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Won));
        });
    }

    [Test]
    public async Task ReportMatch_Throws_When_ExceedingBestOf()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 1);
        var start = DateTime.UtcNow;
        var first = await SeasonMatchSeedC.AddMatchAsync(_context, start, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);
        // Linked but results never set (inconsistent data)
        _context.MatchLinks.Add(new MatchLink { MatchID = first.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        await _context.SaveChangesAsync();
        var second = await SeasonMatchSeedC.AddMatchAsync(_context, start.AddMinutes(20), SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, second.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Cannot go over max number of matches. Something is wrong"));
        var sm = await Reload(seeded.SeasonMatch.SeasonMatchID);
        Assert.That(sm.MatchLinks, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ReportMatch_Throws_When_PlayersDoNotMatchTeams()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var other = SeasonMatchSeedC.OtherXbox;
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, other[..4], Outcomes.Won, other[4..], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_TeamsAreMixed()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var h = SeasonMatchSeedC.HomeXbox;
        var a = SeasonMatchSeedC.AwayXbox;
        // Team 0 has two home and two away players => cannot determine which side is which
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, [h[0], h[1], a[0], a[1]], Outcomes.Won, [h[2], h[3], a[2], a[3]], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_OneTeamHasNoKnownPlayers()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.OtherXbox[..4], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }

    [Test]
    public async Task ReportMatch_Throws_When_SecondTeamIsMixed()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var h = SeasonMatchSeedC.HomeXbox;
        var a = SeasonMatchSeedC.AwayXbox;
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, [h[0], h[1], h[2], SeasonMatchSeedC.OtherXbox[0]], Outcomes.Won, [h[3], a[0], a[1], a[2]], Outcomes.Lost);

        var ex = Assert.ThrowsAsync<Exception>(() => _service.ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID));

        Assert.That(ex!.Message, Is.EqualTo("Unable to verify teams"));
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchServiceBracketCTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        // Real bracket service so that DetermineNextMatches runs against the entities loaded by SeasonMatchService
        var bracketService = new BracketService(_context, new TeamStandingsService(_context));
        _service = new SeasonMatchService(_context, _dataPullService, bracketService);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private record Bracket3(SeasonMatchSeedC.Seeded Seeded, SeasonMatch Next, SeasonMatch LoserNext, MatchBracketInfo Info);

    /// <summary>
    /// Seeded match is a winner bracket round 1 game (seeds 1 vs 2). Winner goes to the HOME slot of a winner bracket game,
    /// loser goes to the AWAY slot of a loser bracket game.
    /// </summary>
    private async Task<Bracket3> SeedWinnerBracket(Bracket bracket = Bracket.Winner, Bracket nextBracket = Bracket.Winner, Bracket loserBracket = Bracket.Loser, bool winnerIntoAway = false)
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 1);
        var info = new MatchBracketInfo
        {
            SeasonMatchID = seeded.SeasonMatch.SeasonMatchID,
            RoundNumber = 1, MatchNumber = 1,
            HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2,
            Bracket = bracket,
        };
        _context.Add(info);
        await _context.SaveChangesAsync();

        var next = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        var loserNext = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        _context.SeasonMatches.AddRange(next, loserNext);
        await _context.SaveChangesAsync();

        var nextInfo = new MatchBracketInfo
        {
            SeasonMatchID = next.SeasonMatchID, RoundNumber = 2, MatchNumber = 2, Bracket = nextBracket,
            HomeTeamPreviousMatchBracketInfoID = winnerIntoAway ? null : info.MatchBracketInfoID,
            HomeTeamSeedNumber = winnerIntoAway ? 3 : null,
            AwayTeamPreviousMatchBracketInfoID = winnerIntoAway ? info.MatchBracketInfoID : null,
            AwayTeamSeedNumber = winnerIntoAway ? null : 3,
        };
        var loserInfo = new MatchBracketInfo
        {
            SeasonMatchID = loserNext.SeasonMatchID, RoundNumber = 1, MatchNumber = 3, Bracket = loserBracket,
            HomeTeamSeedNumber = 4,
            AwayTeamPreviousMatchBracketInfoID = info.MatchBracketInfoID,
        };
        _context.AddRange(nextInfo, loserInfo);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return new Bracket3(seeded, next, loserNext, info);
    }

    private async Task<SeasonMatch> Load(int id)
    {
        await using var ctx = SeasonMatchSeedC.NewContext(_context);
        return await ctx.SeasonMatches.AsNoTracking().SingleAsync(x => x.SeasonMatchID == id);
    }

    [Test]
    public async Task HomeForfeit_InWinnerBracket_AdvancesAwayTeam_AndDropsHomeToLoserBracket()
    {
        var b = await SeedWinnerBracket();

        await _service.HomeForfeit(b.Seeded.SeasonMatch.SeasonMatchID);

        var next = await Load(b.Next.SeasonMatchID);
        var loserNext = await Load(b.LoserNext.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(next.HomeTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
            Assert.That(next.AwayTeamID, Is.Null);
            Assert.That(loserNext.AwayTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(loserNext.HomeTeamID, Is.Null);
        });
    }

    [Test]
    public async Task AwayForfeit_InWinnerBracket_WinnerIntoAwaySlot()
    {
        var b = await SeedWinnerBracket(winnerIntoAway: true);

        await _service.AwayForfeit(b.Seeded.SeasonMatch.SeasonMatchID);

        var next = await Load(b.Next.SeasonMatchID);
        var loserNext = await Load(b.LoserNext.SeasonMatchID);
        var sm = await Load(b.Seeded.SeasonMatch.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sm.HomeTeamResult, Is.EqualTo(SeasonMatchResult.Won));
            Assert.That(sm.AwayTeamResult, Is.EqualTo(SeasonMatchResult.Forfeit));
            Assert.That(next.AwayTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(next.HomeTeamID, Is.Null);
            Assert.That(loserNext.AwayTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
        });
    }

    [Test]
    public async Task ReportMatch_DecidingGameInWinnerBracket_AdvancesTeams()
    {
        var b = await SeedWinnerBracket();
        var match = await SeasonMatchSeedC.AddMatchAsync(_context, DateTime.UtcNow, SeasonMatchSeedC.HomeXbox, Outcomes.Won, SeasonMatchSeedC.AwayXbox, Outcomes.Lost);
        _context.ChangeTracker.Clear();

        await _service.ReportMatch(b.Seeded.SeasonMatch.SeasonMatchID, match.MatchID);

        var next = await Load(b.Next.SeasonMatchID);
        var loserNext = await Load(b.LoserNext.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(next.HomeTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(loserNext.AwayTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
        });
    }

    [Test]
    public async Task HomeWinsGrandFinal_DoesNotFillSuddenDeath()
    {
        // Grand final: winner -> home of sudden death, loser -> away of sudden death
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 1);
        var gf = new MatchBracketInfo { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, RoundNumber = 1, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.GrandFinal };
        _context.Add(gf);
        var sd = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        _context.SeasonMatches.Add(sd);
        await _context.SaveChangesAsync();
        _context.Add(new MatchBracketInfo
        {
            SeasonMatchID = sd.SeasonMatchID, RoundNumber = 2, MatchNumber = 2, Bracket = Bracket.GrandFinalSuddenDeath,
            HomeTeamPreviousMatchBracketInfoID = gf.MatchBracketInfoID, AwayTeamPreviousMatchBracketInfoID = gf.MatchBracketInfoID,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.AwayForfeit(seeded.SeasonMatch.SeasonMatchID); // home team wins

        var sdLoaded = await Load(sd.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sdLoaded.HomeTeamID, Is.Null);
            Assert.That(sdLoaded.AwayTeamID, Is.Null);
        });
    }

    [Test]
    public async Task AwayWinsGrandFinal_FillsSuddenDeath()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, bestOf: 1);
        var gf = new MatchBracketInfo { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, RoundNumber = 1, MatchNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 2, Bracket = Bracket.GrandFinal };
        _context.Add(gf);
        var sd = new SeasonMatch { SeasonID = seeded.Season.SeasonID, BestOf = 1 };
        _context.SeasonMatches.Add(sd);
        await _context.SaveChangesAsync();
        _context.Add(new MatchBracketInfo
        {
            SeasonMatchID = sd.SeasonMatchID, RoundNumber = 2, MatchNumber = 2, Bracket = Bracket.GrandFinalSuddenDeath,
            HomeTeamPreviousMatchBracketInfoID = gf.MatchBracketInfoID, AwayTeamPreviousMatchBracketInfoID = gf.MatchBracketInfoID,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        await _service.HomeForfeit(seeded.SeasonMatch.SeasonMatchID); // away team wins

        var sdLoaded = await Load(sd.SeasonMatchID);
        Assert.Multiple(() =>
        {
            Assert.That(sdLoaded.HomeTeamID, Is.EqualTo(seeded.Away.TeamID));
            Assert.That(sdLoaded.AwayTeamID, Is.EqualTo(seeded.Home.TeamID));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_ForPlayoffMatch_ReturnsBracketInfo()
    {
        var b = await SeedWinnerBracket();

        var page = await _service.GetSeasonMatchPage(b.Seeded.SeasonMatch.SeasonMatchID);

        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.IsPlayoff, Is.True);
            Assert.That(page.HomeTeamID, Is.EqualTo(b.Seeded.Home.TeamID));
            Assert.That(page.AwayTeamID, Is.EqualTo(b.Seeded.Away.TeamID));
            Assert.That(page.BracketInfo, Is.Not.Null);
            Assert.That(page.BracketInfo!.HomeTeamSeed, Is.EqualTo(1));
            Assert.That(page.BracketInfo.AwayTeamSeed, Is.EqualTo(2));
            Assert.That(page.BracketInfo.HomeTeamPreviousMatchID, Is.Null);
            Assert.That(page.BracketInfo.AwayTeamPreviousMatchID, Is.Null);
            Assert.That(page.BracketInfo.WinnerNextMatchID, Is.EqualTo(b.Next.SeasonMatchID));
            Assert.That(page.BracketInfo.LoserNextMatchID, Is.EqualTo(b.LoserNext.SeasonMatchID));
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_ForLaterRound_ReturnsPreviousMatchIDs()
    {
        var b = await SeedWinnerBracket();

        var page = await _service.GetSeasonMatchPage(b.Next.SeasonMatchID);

        Assert.That(page, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(page!.IsPlayoff, Is.True);
            Assert.That(page.HomeTeamName, Is.Null);
            Assert.That(page.AwayTeamID, Is.Null);
            Assert.That(page.BracketInfo!.HomeTeamSeed, Is.Null);
            Assert.That(page.BracketInfo.AwayTeamSeed, Is.EqualTo(3));
            Assert.That(page.BracketInfo.HomeTeamPreviousMatchID, Is.EqualTo(b.Seeded.SeasonMatch.SeasonMatchID));
            Assert.That(page.BracketInfo.AwayTeamPreviousMatchID, Is.Null);
            Assert.That(page.BracketInfo.WinnerNextMatchID, Is.Null);
            Assert.That(page.BracketInfo.LoserNextMatchID, Is.Null);
        });
    }

    [Test]
    public async Task GetSeasonMatchPage_IncludesActiveRescheduleRequest()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var user = await _context.Users.FirstAsync();
        var reschedule = new MatchReschedule { SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, Reason = "busy", RequestedByUserID = user.Id };
        _context.MatchReschedules.Add(reschedule);
        await _context.SaveChangesAsync();
        seeded.SeasonMatch.ActiveRescheduleRequestId = reschedule.MatchRescheduleID;
        await _context.SaveChangesAsync();

        var page = await _service.GetSeasonMatchPage(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(page!.ActiveRescheduleRequestId, Is.EqualTo(reschedule.MatchRescheduleID));
        Assert.That(page.BracketInfo, Is.Null);
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SeasonMatchServicePossibleMatchesCTests
{
    private GrifballContext _context;
    private IDataPullService _dataPullService;
    private SeasonMatchService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _service = new SeasonMatchService(_context, _dataPullService, Substitute.For<IBracketService>());
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetPossibleMatches_ReturnsEmpty_When_AwayTeamHasNoPlayers()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var lonely = new Team { SeasonID = seeded.Season.SeasonID, TeamName = "Empty" };
        _context.Teams.Add(lonely);
        await _context.SaveChangesAsync();
        var sm = new SeasonMatch { SeasonID = seeded.Season.SeasonID, HomeTeamID = seeded.Home.TeamID, AwayTeamID = lonely.TeamID, BestOf = 1 };
        _context.SeasonMatches.Add(sm);
        await _context.SaveChangesAsync();

        var result = await _service.GetPossibleMatches(sm.SeasonMatchID);

        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task GetPossibleMatches_ReturnsEmpty_When_PlayersHaveNoXboxAccount()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context, playersHaveXbox: false);

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(result, Is.Empty);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task GetPossibleMatches_DownloadsMatchesForAllPlayers_AndFiltersCandidates()
    {
        var seeded = await SeasonMatchSeedC.SeedAsync(_context);
        var h = SeasonMatchSeedC.HomeXbox;
        var a = SeasonMatchSeedC.AwayXbox;
        var o = SeasonMatchSeedC.OtherXbox;
        var start = new DateTime(2025, 3, 1, 20, 0, 0, DateTimeKind.Utc);

        // Valid, home players on team 1 (swapped)
        var valid1 = await SeasonMatchSeedC.AddMatchAsync(_context, start, a, Outcomes.Lost, h, Outcomes.Won, team0Score: 2, team1Score: 7);
        // Valid with a ringer on the away side, later start time
        var valid2 = await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(1), h, Outcomes.Won, [a[0], a[1], a[2], o[0]], Outcomes.Lost);
        // Excluded: no winner
        await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(2), h, Outcomes.Tie, a, Outcomes.Tie);
        // Excluded: team sizes are not 4v4
        await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(3), h[..3], Outcomes.Won, a, Outcomes.Lost);
        // Excluded: already linked to a season match
        var linked = await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(4), h, Outcomes.Won, a, Outcomes.Lost);
        _context.MatchLinks.Add(new MatchLink { MatchID = linked.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, MatchNumber = 1 });
        await _context.SaveChangesAsync();
        // Excluded by GetTeams: only one known player, other team all strangers
        await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(5), [h[0], o[1], o[2], o[3]], Outcomes.Won, o[4..], Outcomes.Lost);
        // Excluded by GetTeams: mixed teams
        await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(6), [h[0], h[1], a[0], a[1]], Outcomes.Won, [h[2], h[3], a[2], a[3]], Outcomes.Lost);
        // Excluded: strangers only
        await SeasonMatchSeedC.AddMatchAsync(_context, start.AddHours(7), o[..4], Outcomes.Won, o[4..], Outcomes.Lost);
        _context.ChangeTracker.Clear();

        var result = await _service.GetPossibleMatches(seeded.SeasonMatch.SeasonMatchID);

        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(
            Arg.Is<List<long>>(ids => ids.Count == 8 && h.All(ids.Contains) && a.All(ids.Contains)),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        Assert.That(result.Select(x => x.MatchID), Is.EqualTo(new[] { valid2.MatchID, valid1.MatchID }), "ordered by start time desc");

        var first = result[1]; // valid1
        Assert.Multiple(() =>
        {
            Assert.That(first.HomeTeam.TeamID, Is.EqualTo(1));
            Assert.That(first.HomeTeam.Score, Is.EqualTo(7));
            Assert.That(first.HomeTeam.Outcome, Is.EqualTo(Outcomes.Won));
            Assert.That(first.HomeTeam.Players.Select(p => p.XboxUserID), Is.EquivalentTo(h));
            Assert.That(first.HomeTeam.Players.All(p => p.IsOnTeam), Is.True);
            Assert.That(first.AwayTeam.TeamID, Is.EqualTo(0));
            Assert.That(first.AwayTeam.Score, Is.EqualTo(2));
            Assert.That(first.AwayTeam.Outcome, Is.EqualTo(Outcomes.Lost));
            var p201 = first.AwayTeam.Players.Single(p => p.XboxUserID == 201);
            Assert.That(p201.Gamertag, Is.EqualTo("GT201"));
            Assert.That(p201.Kills, Is.EqualTo(1));
            Assert.That(p201.Deaths, Is.EqualTo(1));
            Assert.That(p201.Score, Is.EqualTo(1));
        });

        var second = result[0]; // valid2, ringer o[0]
        Assert.Multiple(() =>
        {
            Assert.That(second.HomeTeam.TeamID, Is.EqualTo(0));
            Assert.That(second.AwayTeam.Players.Single(p => p.XboxUserID == o[0]).IsOnTeam, Is.False);
            Assert.That(second.AwayTeam.Players.Count(p => p.IsOnTeam), Is.EqualTo(3));
        });
    }
}
