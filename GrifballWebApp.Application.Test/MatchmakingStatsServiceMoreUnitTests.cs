using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// MatchmakingStatsService's "fromGames" path (PlayerGameStatsQuery.Matchmaking), which MatchmakingStatsServiceUnitTests
/// leaves out: an archive game of a matchmaking season (a recorded game) and the Discord queue's match decided by vote,
/// totalled per player, the games winning over a stored row of the same season. The queue's Waypoint (reported) matches
/// need a whole Halo match seeded and are Integration.Test's.
/// </summary>
[TestFixture]
internal sealed class MatchmakingStatsServiceMoreUnitTests : ApplicationDbContextTests
{
    private const int Halo3GameVersionID = 1; // GameVersionConfiguration seeds this through HasData
    private static readonly DateTime SeasonStart = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly int[] HomeUserIDs = [1];
    private static readonly int[] AwayUserIDs = [2];
    private static readonly string[] QueueNamesInOrder = ["Away Player", "Home Player"];

    private MatchmakingStatsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new MatchmakingStatsService(Context);
    }

    private async Task SeedUsers()
    {
        Context.Users.AddRange(User.Seed(userName: "one", displayName: "Home Player", id: 1), User.Seed(userName: "two", displayName: "Away Player", id: 2));
        await Context.SaveChangesAsync();
    }

    /// <summary>A matchmaking season with one archive game: user 1 won it (10 kills, 3 goals), user 2 lost it.</summary>
    private async Task<MatchmakingSeason> SeedArchiveGame()
    {
        await SeedUsers();
        var season = MatchmakingSeason.Seed("Late League 8", SeasonStart, SeasonEnd, gameVersionID: Halo3GameVersionID);
        Context.MatchmakingSeasons.Add(season);
        await Context.SaveChangesAsync();
        var game = RecordedGame.Seed("late-league-8/game-1", gameVersionID: Halo3GameVersionID);
        var home = game.SeedSide(0, outcome: Outcomes.Won);
        var away = game.SeedSide(1, outcome: Outcomes.Lost);
        game.SeedPlayer(new RecordedPlayerLine(1, 0, "One", 10, 4, 2) { Goals = 3, CarrierKills = 2 }, home);
        game.SeedPlayer(new RecordedPlayerLine(2, 1, "Two", 4, 10, 1) { Goals = 1 }, away);
        Context.MatchedMatches.Add(MatchedMatch.Archive(game, season.MatchmakingSeasonID, new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        await Context.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task GetAsync_AnArchiveGame_TotalsItsLinesPerPlayer()
    {
        var season = await SeedArchiveGame();

        var lines = await _service.GetAsync();

        Assert.That(lines, Has.Count.EqualTo(2));
        var one = lines[0];
        var two = lines[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That((one.UserID, one.Gamertag, one.FromMatches), Is.EqualTo((1, "One", true)), "most goals first");
            Assert.That((one.MatchmakingSeasonID, one.MatchmakingSeasonName), Is.EqualTo(((int?)season.MatchmakingSeasonID, "Late League 8")));
            Assert.That((one.GameVersionID, one.GameVersionName), Is.EqualTo(((int?)Halo3GameVersionID, "Halo 3")));
            Assert.That((one.GamesPlayed, one.Wins, one.Losses), Is.EqualTo((1, 1, 0)));
            Assert.That((one.Kills, one.Deaths, one.Assists, one.Goals, one.CarrierKills), Is.EqualTo((10, 4, 2, 3L, 2)));
            Assert.That(one.SeasonKey, Is.EqualTo($"matchmaking/{season.MatchmakingSeasonID}"));
            Assert.That(one.MatchmakingPlayerStatID, Is.Null);
            Assert.That((two.UserID, two.GamesPlayed, two.Wins, two.Losses, two.Goals), Is.EqualTo((2, 1, 0, 1, 1L)));
        }
    }

    [Test]
    public async Task GetAsync_GamesAndAStoredRowOfTheSameSeason_KeepsTheGames()
    {
        var season = await SeedArchiveGame();
        Context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "One (sheet)", GamesPlayed = 40, Wins = 30, Losses = 10, Kills = 400 },
            matchmakingSeasonID: season.MatchmakingSeasonID, userID: 1, gameVersionID: Halo3GameVersionID));
        await Context.SaveChangesAsync();

        var lines = await _service.GetAsync(userID: 1);

        var line = lines.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.FromMatches, Is.True);
            Assert.That(line.GamesPlayed, Is.EqualTo(1));
            Assert.That(line.Gamertag, Is.EqualTo("One"));
        }
    }

    [Test]
    public async Task GetAsync_ByUser_LeavesTheOtherPlayersOut()
    {
        await SeedArchiveGame();

        var lines = await _service.GetAsync(userID: 2);

        Assert.That(lines.Select(l => (l.UserID, l.Gamertag)), Is.EqualTo(new[] { (2, "Two") }));
    }

    [Test]
    public async Task GetSeasonsAsync_CountsTheArchiveGameAndItsPlayers()
    {
        var season = await SeedArchiveGame();

        var seasons = await _service.GetSeasonsAsync();

        var dto = seasons.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.MatchmakingSeasonID, Is.EqualTo(season.MatchmakingSeasonID));
            Assert.That(dto.GameCount, Is.EqualTo(1));
            Assert.That(dto.PlayerCount, Is.EqualTo(2));
            Assert.That((dto.StartsAt, dto.EndsAt), Is.EqualTo((SeasonStart, SeasonEnd)));
            Assert.That(dto.IsHistorical, Is.False);
            Assert.That(dto.GameVersionSlug, Is.EqualTo("halo3"));
        }
    }

    [Test]
    public async Task GetQueueAsync_AMatchDecidedByVote_CountsAWinAndALossWithNoBoxScore()
    {
        await SeedUsers();
        var match = MatchedMatch.Start(HomeUserIDs, AwayUserIDs,new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc));
        match.DecideByVote(MatchedSide.Home);
        Context.MatchedMatches.Add(match);
        await Context.SaveChangesAsync();

        var queue = await _service.GetQueueAsync();

        Assert.That(queue.Players, Has.Count.EqualTo(2));
        var winner = queue.Players.Single(p => p.UserID == 1);
        var loser = queue.Players.Single(p => p.UserID == 2);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((queue.GameCount, queue.VoteDecidedGameCount), Is.EqualTo((1, 1)));
            Assert.That(queue.Players.Select(p => p.Gamertag), Is.EqualTo(QueueNamesInOrder), "the queue's lines by name");
            Assert.That((winner.Gamertag, winner.Source, winner.MatchmakingSeasonID, winner.MatchmakingSeasonName), Is.EqualTo(("Home Player", "Matches", (int?)null, (string?)null)));
            Assert.That((winner.GamesPlayed, winner.GamesDecidedByVote, winner.Wins, winner.Losses, winner.Kills), Is.EqualTo((1, 1, 1, 0, 0)));
            Assert.That((winner.GameVersionID, winner.GameVersionName), Is.EqualTo(((int?)6, "Halo Infinite")));
            Assert.That((loser.GamesPlayed, loser.Wins, loser.Losses), Is.EqualTo((1, 0, 1)));
        }
    }
}
