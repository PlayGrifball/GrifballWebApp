using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.LateLeague;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

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
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public void A_vote_decided_match_counts_the_winners_and_losers_still_in_it()
    {
        var match = MatchedMatch.Start([1, 2], [3, 4]);
        Assert.Multiple(() =>
        {
            Assert.That(match.VoteWinners(), Is.Empty, "Not decided yet");
            Assert.That(match.VoteLosers(), Is.Empty);
        });
        match.VoteToKick(1, 4);
        match.VoteToKick(2, 4);
        match.VoteToKick(3, 4);
        match.DecideByVote(MatchedSide.Away);

        Assert.Multiple(() =>
        {
            Assert.That(match.VoteWinners().Select(p => p.UserID), Is.EqualTo(new[] { 3 }), "4 was kicked: no win for them");
            Assert.That(match.VoteLosers().Select(p => p.UserID), Is.EqualTo(new[] { 1, 2 }));
            var home = MatchedMatch.Start([1], [2]);
            home.DecideByVote(MatchedSide.Home);
            Assert.That((home.VoteWinners().Single().UserID, home.VoteLosers().Single().UserID), Is.EqualTo((1, 2)), "Home the winner");
            Assert.That(MatchedMatch.Start([1], [2]).Canceled().VoteWinners(), Is.Empty, "A canceled match has no winner");
            Assert.That(MatchedMatch.Start([1], [2]).Reported(Guid.NewGuid()).VoteLosers(), Is.Empty, "A reported one's result is its Halo match's");
        });
    }

    [Test]
    public async Task Vote_decided_games_count_in_games_played_wins_and_losses_with_no_box_score()
    {
        await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        var users = await _context.Users.Where(u => u.XboxUserID != null).ToDictionaryAsync(u => u.XboxUserID!.Value, u => u.Id);
        // A reported game: 101 and 102 beat 201 and 202 in Halo
        var halo = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddHours(-2), [101, 102], Outcomes.Won, [201, 202], Outcomes.Lost);
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
        var results = (await _context.VoteResults().ToListAsync()).OrderBy(r => r.UserID)
            .Select(r => (r.UserID, r.Gamertag, r.MatchmakingSeasonID, r.Won));
        Assert.That(results, Is.EqualTo(new[]
        {
            (users[101], (string?)"GT101", (int?)null, false),
            (users[102], "GT102", null, false),
            (users[201], "GT201", null, true),
            (users[202], "GT202", null, true),
            (users[203], "GT203", null, true),
        }.OrderBy(r => r.Item1)));

        var stats = new MatchmakingStatsService(_context);
        var queue = await stats.GetQueueAsync();
        var lines = queue.Players.ToDictionary(p => p.Gamertag);

        Assert.Multiple(() =>
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
        });

        // A profile's career: the game counts, but every stat per game is over the games with a box score
        var profile = (await new ProfileStatsService(_context, new SeasonStatsService(_context), stats).GetAsync(users[101]))!;
        var infinite = profile.CareerByGame.Single();
        Assert.Multiple(() =>
        {
            Assert.That((infinite.GamesPlayed, infinite.GamesDecidedByVote, infinite.GamesWithStats), Is.EqualTo((2, 1, 1)));
            Assert.That(infinite.GoalsPerGame, Is.EqualTo((decimal)infinite.Goals!.Value), "Goals over the one game with a box score");
            Assert.That((profile.OverallCareer!.GamesPlayed, profile.OverallCareer.GamesDecidedByVote), Is.EqualTo((2, 1)));
            Assert.That((profile.ServiceRecord!.Matchmaking.GamesPlayed, profile.ServiceRecord.Matchmaking.Wins, profile.ServiceRecord.Matchmaking.Losses),
                Is.EqualTo((2, 1, 1)));
            Assert.That(profile.LateLeague.Single().GamesDecidedByVote, Is.EqualTo(1));
        });
    }
}
