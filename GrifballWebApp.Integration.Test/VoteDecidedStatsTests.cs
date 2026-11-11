using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Stats;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A Late League match decided by its players' vote (#296) counts, with its winner, in the queue's games and in its players'
/// games played, wins and losses: only for the players still in it (a kicked player neither wins nor loses), and with no box
/// score, so it adds no kills or goals and lowers no stat per game.
/// </summary>
[TestFixture]
public class VoteDecidedStatsTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await TestDatabase.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task A_vote_decided_line_names_a_player_with_no_Xbox_account_as_the_app_does_never_by_their_login()
    {
        // Two players whose Xbox accounts were unlinked: one with a Discord account, one with only a display name
        var discord = User.Seed(userName: "login-discord", displayName: "Shown Discord", discordUser: DiscordUser.FromDiscord(9001, "disc-name"));
        var display = User.Seed(userName: "login-display", displayName: "Shown Display");
        _context.Users.AddRange(discord, display);
        await _context.SaveChangesAsync();
        var match = MatchedMatch.Start([discord.Id], [display.Id]);
        match.DecideByVote(MatchedSide.Home);
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var lines = (await new MatchmakingStatsService(_context).GetQueueAsync()).Players.ToDictionary(p => p.UserID, p => p.Gamertag);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lines[discord.Id], Is.EqualTo("disc-name"), "The Discord name, before the display name");
            Assert.That(lines[display.Id], Is.EqualTo("Shown Display"));
            Assert.That(lines.Values, Has.None.StartsWith("login-"), "A login name is private");
        }
    }

    [Test]
    public async Task Vote_decided_games_count_in_games_played_wins_and_losses_with_no_box_score()
    {
        await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value, u => u.Id);
        // A reported game: 101 and 102 beat 201 and 202 in Halo
        var halo = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-2), [101, 102], Outcomes.Won, [201, 202], Outcomes.Lost);
        // 101 stopped the carrier 3 times in it (a line that records stops)
        _context.SetRaw(halo.Participants.Single(p => p.XboxUserID == 101), p => p.CarrierKills, (int?)3);
        var reported = MatchedMatch.Start([users[101], users[102]], [users[201], users[202]]).Reported(halo.MatchID);
        // A game decided by vote, Away the winner; 103 was kicked out of it first (4 of the 6 voted so)
        var voted = MatchedMatch.Start([users[101], users[102], users[103]], [users[201], users[202], users[203]]);
        foreach (var voter in new[] { 101L, 102, 201, 202 })
            voted.VoteToKick(users[voter], users[103]);
        voted.DecideByVote(MatchedSide.Away);
        // Neither of these counts: called off, and still being played
        var canceled = MatchedMatch.Start([users[101]], [users[201]]).Canceled();
        var active = MatchedMatch.Start([users[104]], [users[204]]);
        _context.MatchedMatches.AddRange(reported, voted, canceled, active);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // The model's results: one for each player still in the vote-decided match, none for the kicked one or other matches
        var results = (await _context.MatchedMatches.VoteResults().ToListAsync()).OrderBy(r => r.UserID)
            .Select(r => (r.UserID, r.MatchmakingSeasonID, r.Won));
        Assert.That(results, Is.EqualTo(new[]
        {
            (users[101], (int?)null, false),
            (users[102], null, false),
            (users[201], null, true),
            (users[202], null, true),
            (users[203], null, true),
        }.OrderBy(r => r.Item1)));

        var stats = new MatchmakingStatsService(_context);
        var queue = await stats.GetQueueAsync();
        var lines = queue.Players.ToDictionary(p => p.Gamertag);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((queue.GameCount, queue.VoteDecidedGameCount), Is.EqualTo((2, 1)));
            // 101: won in Halo (1 kill, 1 death), lost the vote
            Assert.That((lines["GT101"].GamesPlayed, lines["GT101"].GamesDecidedByVote, lines["GT101"].Wins, lines["GT101"].Losses, lines["GT101"].Kills, lines["GT101"].Deaths),
                Is.EqualTo((2, 1, 1, 1, 1, 1)));
            Assert.That(lines["GT101"].WinPercent, Is.EqualTo(50m), "A vote-decided game is in the record");
            // 201: lost in Halo, won the vote
            Assert.That((lines["GT201"].GamesPlayed, lines["GT201"].Wins, lines["GT201"].Losses), Is.EqualTo((2, 1, 1)));
            // 203 played only the vote-decided game: a win and nothing else
            Assert.That((lines["GT203"].GamesPlayed, lines["GT203"].GamesDecidedByVote, lines["GT203"].Wins, lines["GT203"].Losses, lines["GT203"].Kills, lines["GT203"].Goals, lines["GT203"].GameVersionID),
                Is.EqualTo((1, 1, 1, 0, 0, 0L, (int?)6)));
            Assert.That(lines, Does.Not.ContainKey("GT103"), "Kicked: neither won nor lost it");
            Assert.That(lines, Does.Not.ContainKey("GT104"), "An active match counts for nobody yet");
        }

        // A profile's career: the game counts, but every stat per game is over the games with a box score
        var profile = (await new ProfileStatsService(_context, new SeasonStatsService(_context), stats).GetAsync(users[101]))!;
        var infinite = profile.CareerByGame.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((infinite.GamesPlayed, infinite.GamesDecidedByVote, infinite.GamesWithStats), Is.EqualTo((2, 1, 1)));
            Assert.That(infinite.GoalsPerGame, Is.EqualTo((decimal)infinite.Goals!.Value), "Goals over the one game with a box score");
            Assert.That((profile.OverallCareer!.GamesPlayed, profile.OverallCareer.GamesDecidedByVote), Is.EqualTo((2, 1)));
            Assert.That((profile.ServiceRecord!.Matchmaking.GamesPlayed, profile.ServiceRecord.Matchmaking.Wins, profile.ServiceRecord.Matchmaking.Losses),
                Is.EqualTo((2, 1, 1)));
            Assert.That(profile.LateLeague.Single().GamesDecidedByVote, Is.EqualTo(1));
            Assert.That(infinite.StopsPerGame, Is.EqualTo(3m), "Stops over the one game with a box score, not 3 over 2");
        }

        // The leaderboards: each per-game figure is over the games with a box score, and the board says how many of the games
        // played were decided by vote, so GP and per game add up
        var boards = await new LeaderboardsService(_context, new SeasonStatsService(_context), stats,
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<LeaderboardsService>.Instance).GetAsync(null);
        static (long, int, int, decimal?) Row(MetricLeaderDto l) => (l.StatValue, l.GamesPlayed, l.GamesDecidedByVote, l.PerGame);
        var goals = boards.GoalLeaders.Single(l => l.UserID == users[101]);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Row(boards.KillLeaders.Single(l => l.UserID == users[101])), Is.EqualTo((1L, 2, 1, (decimal?)1m)), "1 kill in the 1 game with a box score");
            Assert.That(Row(goals), Is.EqualTo((goals.StatValue, 2, 1, (decimal?)goals.StatValue)), "Goals per game over the 1 game with a box score");
            Assert.That(Row(boards.CarrierStopLeaders.Single(l => l.UserID == users[101])), Is.EqualTo((3L, 2, 1, (decimal?)3m)), "3 stops in the 1 game with a box score");
            Assert.That(boards.KillLeaders.Any(l => l.UserID == users[203]), Is.False, "A vote-only player has no kills");
        }
    }
}
