using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;

namespace GrifballWebApp.Test;

/// <summary>A profile's service record: MMR, and K/D and bomb scores over league seasons, matchmaking and both, with a
/// custom mode's Infinite score (#217) counted as decoded, or left out when it can't be read yet.</summary>
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
    public void Totals_AddUpTheLines_AndSayHowManyGamesGoalsCouldntBeRead()
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

        // The lines' goals already leave out a game whose goals can't be read (PlayerGameStatsQuery)
        var totals = ServiceRecord.Totals([Line(3, 2, 30, 10, 4), Line(2, 0, 5, 10, 1)], 1);
        var empty = ServiceRecord.Totals([], 0);

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
        _context.Set<SeriesGame>().AddRange(
            new SeriesGame { MatchID = first.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 },
            new SeriesGame { MatchID = second.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2 });
        var encoded = await _context.Set<MatchParticipant>().SingleAsync(p => p.MatchID == second.MatchID && p.XboxUserID == 101);
        _context.SetRaw(encoded, p => p.Score, 504_010_048);
        // A Late League game, lost
        var queued = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-1), [201, 202], Outcomes.Won, [101, 102], Outcomes.Lost);
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value);
        MatchedTeam Side(params long[] xuids) => new([.. xuids.Select(x => new MatchedPlayer(users[x].Id))]);
        _context.MatchedMatches.Add(MatchedMatch.Start(Side(201, 202), Side(101, 102)).Reported(queued.MatchID));
        users[101].SeedLateLeagueRecord(mmr: 1234);
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
    public async Task ADecodedCustomModeGame_CountsItsDecodedGoals_AnUndecodedOne_IsLeftOut()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 3);
        var home = SeasonMatchTestData.HomeXbox;
        var away = SeasonMatchTestData.AwayXbox;
        // Two league games, both a custom mode packing its counts into Score (#217): the first decoded (2 goals), the
        // second not yet
        var decoded = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-3), home, Outcomes.Won, away, Outcomes.Lost);
        var undecoded = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-2), home, Outcomes.Won, away, Outcomes.Lost);
        _context.Set<SeriesGame>().AddRange(
            new SeriesGame { MatchID = decoded.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 1 },
            new SeriesGame { MatchID = undecoded.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = 2 });
        _context.SetRaw(await _context.Matches.SingleAsync(m => m.MatchID == decoded.MatchID), m => m.ScoreLayout, (CustomScoreLayout?)CustomScoreLayout.B);
        var inDecoded = await _context.Set<MatchParticipant>().SingleAsync(p => p.MatchID == decoded.MatchID && p.XboxUserID == 101);
        _context.SetRaw(inDecoded, p => p.Score, 504_010_048);
        _context.SetRaw(inDecoded, p => p.Goals, (int?)2);
        _context.SetRaw(await _context.Set<MatchParticipant>().SingleAsync(p => p.MatchID == undecoded.MatchID && p.XboxUserID == 101), p => p.Score, 790_240_328);
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var record = (await _profiles.GetAsync(users[101].Id))!.ServiceRecord!;

        Assert.Multiple(() =>
        {
            Assert.That(record.League.GamesPlayed, Is.EqualTo(2));
            Assert.That((record.League.BombScores, record.League.UncountedBombScoreGames), Is.EqualTo((2L, 1)), "The decoded game's 2 goals; the other can't be read");
            Assert.That((record.All.BombScores, record.All.UncountedBombScoreGames), Is.EqualTo((2L, 1)));
        });
    }

    [Test]
    public async Task TheUncountedGames_AreTheGamesGrifballGoalsHasNoGoalsFor()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 5);
        var home = SeasonMatchTestData.HomeXbox;
        var away = SeasonMatchTestData.AwayXbox;
        // 101's four league games, one of each case: a plain score (goals), a packed one not decoded yet, a decoded one,
        // and a decoded layout with no goals on the participant
        var cases = new (CustomScoreLayout? Layout, int Score, int? Goals)[] { (null, 3, null), (null, 790_240_328, null), (CustomScoreLayout.B, 504_010_048, 2), (CustomScoreLayout.B, 504_010_048, null) };
        foreach (var (c, number) in cases.Select((c, i) => (c, i + 1)))
        {
            var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-number), home, Outcomes.Won, away, Outcomes.Lost);
            _context.Set<SeriesGame>().Add(new SeriesGame { MatchID = match.MatchID, SeasonMatchID = seeded.SeasonMatch.SeasonMatchID, GameNumber = number });
            _context.SetRaw(await _context.Matches.SingleAsync(m => m.MatchID == match.MatchID), m => m.ScoreLayout, c.Layout);
            var line = await _context.Set<MatchParticipant>().SingleAsync(p => p.MatchID == match.MatchID && p.XboxUserID == 101);
            _context.SetRaw(line, p => p.Score, c.Score);
            _context.SetRaw(line, p => p.Goals, c.Goals);
        }
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var record = (await _profiles.GetAsync(users[101].Id))!.ServiceRecord!;
        var played = await _context.Set<MatchParticipant>().Include(p => p.MatchTeam.Match).Where(p => p.XboxUserID == 101).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(record.League.UncountedBombScoreGames, Is.EqualTo(played.Count(p => GrifballGoals.Infinite(p) == null)));
            Assert.That(record.League.UncountedBombScoreGames, Is.EqualTo(2));
            Assert.That(record.League.BombScores, Is.EqualTo(5L), "The plain game's 3 and the decoded game's 2");
        });
    }

    [Test]
    public async Task APlayerWhoNeverPlayedMatchmaking_HasNoMmr()
    {
        var user = User.Seed(userName: "fresh", displayName: "Fresh");
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
        var user = User.Seed(userName: "queued", displayName: "Queued", mmr: 1100, wins: 3, losses: 1);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var record = (await _profiles.GetAsync(user.Id))!.ServiceRecord!;

        Assert.That(record.Mmr, Is.EqualTo(1100));
    }

    [Test]
    public async Task AnArchiveOnlyLateLeaguePlayer_HasNoMmr()
    {
        // An old matchmaking season's sheet and an archive match: neither rated them, so their MMR is still the default
        var user = User.Seed(userName: "archived", displayName: "Archived");
        var other = User.Seed(userName: "other", displayName: "Other");
        var late = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.AddRange(user, other, late);
        await _context.SaveChangesAsync();
        _context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.FromSheet(late.MatchmakingSeasonID, user.Id, 1, new StatSheetTotals { Gamertag = "Archived", GamesPlayed = 4, Wins = 3, Losses = 1 }));
        _context.MatchedMatches.Add(MatchedMatchTestExtensions.ArchiveGame(late.MatchmakingSeasonID, late.StartsAt, [user.Id], [other.Id]));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var record = (await _profiles.GetAsync(user.Id))!.ServiceRecord!;

        Assert.Multiple(() =>
        {
            Assert.That(record.Mmr, Is.Null, "Only the queue rates a player");
            Assert.That((record.Matchmaking.GamesPlayed, record.All.GamesPlayed), Is.EqualTo((4, 4)));
        });
    }

    [Test]
    public void Combine_AddsTheCounts_AndWorksOutTheRatiosAgain()
    {
        var a = new ServiceRecordTotalsDto { GamesPlayed = 3, Wins = 2, Losses = 1, Kills = 30, Deaths = 10, BombScores = 4, UncountedBombScoreGames = 1 };
        var b = new ServiceRecordTotalsDto { GamesPlayed = 2, Wins = 0, Losses = 2, Kills = 5, Deaths = 10, BombScores = 1 };

        var both = ServiceRecord.Combine(a, b);

        Assert.Multiple(() =>
        {
            Assert.That((both.GamesPlayed, both.Wins, both.Losses, both.Kills, both.Deaths), Is.EqualTo((5, 2, 3, 35, 20)));
            Assert.That((both.KdRatio, both.WinPercent), Is.EqualTo(((decimal?)1.75m, (decimal?)40.0m)));
            Assert.That((both.BombScores, both.UncountedBombScoreGames), Is.EqualTo((5L, 1)));
        });
    }
}
