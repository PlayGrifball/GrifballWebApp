using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.LateLeague;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>A profile's service record: MMR, and K/D and bomb scores over league seasons, matchmaking and both, with a
/// custom mode's encoded Infinite score (#217) left out of the bomb scores.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ServiceRecordTests
{
    private GrifballContext _context;
    private ProfileStatsService _profiles;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _profiles = new ProfileStatsService(_context, new SeasonStatsService(_context), new MatchmakingStatsService(_context));
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void Totals_AddUpTheLines_LessTheGoalsOfGamesWhoseScoreIsntGoals()
    {
        SeasonStatLine Line(int played, int wins, int kills, int deaths, long goals) => new()
        {
            Season = new SeasonInfoDto { SeasonID = 1 },
            Gamertag = "x",
            GamesPlayed = played,
            Wins = wins,
            Losses = played - wins,
            Kills = kills,
            Deaths = deaths,
            Goals = goals,
        };

        var totals = ServiceRecord.Totals([Line(3, 2, 30, 10, 4 + 790_240_328L), Line(2, 0, 5, 10, 1)], [790_240_328L]);
        var empty = ServiceRecord.Totals([], []);

        Assert.Multiple(() =>
        {
            Assert.That((totals.GamesPlayed, totals.Wins, totals.Losses, totals.Kills, totals.Deaths), Is.EqualTo((5, 2, 3, 35, 20)));
            Assert.That((totals.KdRatio, totals.WinPercent), Is.EqualTo(((decimal?)1.75m, (decimal?)40.0m)));
            Assert.That((totals.BombScores, totals.UncountedBombScoreGames), Is.EqualTo((5L, 1)));
            Assert.That((empty.GamesPlayed, empty.KdRatio, empty.WinPercent, empty.BombScores), Is.EqualTo((0, (decimal?)null, (decimal?)null, 0L)));
        });
    }

    [Test]
    public async Task AProfile_HasLeagueAndMatchmakingTotals_AndBoth()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = SeasonMatchTestData.HomeXbox;
        var away = SeasonMatchTestData.AwayXbox;
        // Two league games; in the second, 101's score is a custom mode's encoded counts, not goals
        var first = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-3), home, Outcomes.Won, away, Outcomes.Lost);
        var second = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-2), home, Outcomes.Won, away, Outcomes.Lost);
        _context.SeriesGames.AddRange(
            new SeriesGame { MatchID = first.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 },
            new SeriesGame { MatchID = second.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2 });
        var encoded = await _context.MatchParticipants.SingleAsync(p => p.MatchID == second.MatchID && p.XboxUserID == 101);
        encoded.Score = 504_010_048;
        // A Late League game, lost
        var queued = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-1), [201, 202], Outcomes.Won, [101, 102], Outcomes.Lost);
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value);
        MatchedTeam Side(params long[] xuids) => new() { Players = [.. xuids.Select(x => new MatchedPlayer { UserID = users[x].Id })] };
        _context.MatchedMatches.Add(new MatchedMatch { HomeTeam = Side(201, 202), AwayTeam = Side(101, 102), MatchID = queued.MatchID, Active = false });
        users[101].MMR = 1234;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var record = (await _profiles.GetAsync(users[101].Id))!.ServiceRecord!;

        Assert.Multiple(() =>
        {
            Assert.That(record.Mmr, Is.EqualTo(1234));
            // 101 has 1 kill, 1 death and 1 goal a game on the winning side, 2 deaths and no goal on the losing one
            Assert.That((record.League.GamesPlayed, record.League.Wins, record.League.Kills, record.League.Deaths), Is.EqualTo((2, 2, 2, 2)));
            Assert.That((record.League.BombScores, record.League.UncountedBombScoreGames), Is.EqualTo((1L, 1)), "The encoded game's score isn't a bomb score");
            Assert.That((record.Matchmaking.GamesPlayed, record.Matchmaking.Losses, record.Matchmaking.Deaths, record.Matchmaking.BombScores), Is.EqualTo((1, 1, 2, 0L)));
            Assert.That((record.All.GamesPlayed, record.All.Wins, record.All.Kills, record.All.Deaths, record.All.KdRatio), Is.EqualTo((3, 2, 3, 4, (decimal?)0.75m)));
            Assert.That((record.All.BombScores, record.All.UncountedBombScoreGames), Is.EqualTo((1L, 1)));
        });
    }

    [Test]
    public async Task APlayerWhoNeverPlayedMatchmaking_HasNoMmr()
    {
        var user = new User { UserName = "fresh", DisplayName = "Fresh" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var record = (await _profiles.GetAsync(user.Id))!.ServiceRecord!;

        Assert.Multiple(() =>
        {
            Assert.That(record.Mmr, Is.Null, "Everyone starts at the same default MMR");
            Assert.That((record.All.GamesPlayed, record.All.BombScores, record.All.KdRatio), Is.EqualTo((0, 0L, (decimal?)null)));
        });
    }

    [Test]
    public async Task AQueueRecordWithoutGames_ShowsTheMmr()
    {
        // The Discord queue keeps a win/loss record on the user even where its games haven't been pulled
        var user = new User { UserName = "queued", DisplayName = "Queued", MMR = 1100, Wins = 3, Losses = 1 };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var record = (await _profiles.GetAsync(user.Id))!.ServiceRecord!;

        Assert.That(record.Mmr, Is.EqualTo(1100));
    }
}
