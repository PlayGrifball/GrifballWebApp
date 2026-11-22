using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using static GrifballWebApp.Application.Test.SeasonMatchUnitTestData;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// StatFlagService's rules (#548) and its reconciliation, against the fake IApplicationDbContext: a hand-entered game's
/// rules run through SeasonMatchService.AddManualGame/EditManualGame (the real hook), an "any game" rule against a
/// RecordedGame or a Halo match seeded directly, and re-running the checks replaces an open flag and leaves a dismissed
/// one alone unless its reason changed.
/// </summary>
[TestFixture]
internal sealed class StatFlagServiceUnitTests : ApplicationDbContextTests
{
    private FakeTimeProvider _time = null!;
    private StatFlagService _statFlags = null!;
    private StaffStatFlagService _staff = null!;
    private SeasonMatchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // Within SeasonMatchUnitTestData's season (1 Jan to 1 Mar 2026)
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero));
        _statFlags = new StatFlagService(Context, _time);
        _staff = new StaffStatFlagService(Context, _time);
        var championships = new ChampionshipService(Context);
        var bracketService = new BracketService(Context, new TeamStandingsService(Context), championships);
        _service = new SeasonMatchService(Context, Substitute.For<IDataPullService>(), bracketService, championships, _statFlags);
    }

    private static ManualGameRequestDto Entry(int home, int away, DateTime? playedAt = null, params ManualGamePlayerRequestDto[] players) => new()
    {
        GameVersionID = Halo3,
        HomeTeamScore = home,
        AwayTeamScore = away,
        PlayedAt = playedAt,
        Players = players.ToList(),
    };

    private static ManualGamePlayerRequestDto Line(int userID, bool isHomeTeam, int? goals = null) => new() { UserID = userID, IsHomeTeam = isHomeTeam, Goals = goals };

    [Test]
    public async Task AddManualGame_WhereTheScoreDoesNotMatchThePlayersGoals_IsFlagged()
    {
        var seeded = await SeedAsync(Context);
        var dto = Entry(3, 1,
            players:
            [
                Line(seeded.HomeUsers[0].Id, true, goals: 1),
                Line(seeded.HomeUsers[1].Id, true, goals: 1),
                Line(seeded.AwayUsers[0].Id, false, goals: 1),
            ]);

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals);
        Assert.That(flag.Reason, Does.Contain("add up to 2"));
    }

    [Test]
    public async Task AddManualGame_WhereThePlayersGoalsMatchTheScore_IsNotFlagged()
    {
        // Full rosters (4v4, the season's format) and matching scores/goals/dates: nothing here is worth a look
        var seeded = await SeedAsync(Context);
        var dto = Entry(2, 1, playedAt: InSeason,
            players:
            [
                Line(seeded.HomeUsers[0].Id, true, goals: 1),
                Line(seeded.HomeUsers[1].Id, true, goals: 1),
                Line(seeded.HomeUsers[2].Id, true, goals: 0),
                Line(seeded.HomeUsers[3].Id, true, goals: 0),
                Line(seeded.AwayUsers[0].Id, false, goals: 1),
                Line(seeded.AwayUsers[1].Id, false, goals: 0),
                Line(seeded.AwayUsers[2].Id, false, goals: 0),
                Line(seeded.AwayUsers[3].Id, false, goals: 0),
            ]);

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task AddManualGame_WhereASideFieldsFewerPlayersThanTheFormat_IsFlagged()
    {
        var seeded = await SeedAsync(Context);
        var dto = Entry(1, 0,
            players:
            [
                Line(seeded.HomeUsers[0].Id, true),
                Line(seeded.HomeUsers[1].Id, true),
                Line(seeded.AwayUsers[0].Id, false),
                Line(seeded.AwayUsers[1].Id, false),
                Line(seeded.AwayUsers[2].Id, false),
                Line(seeded.AwayUsers[3].Id, false),
            ]);

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.TeamSmallerThanFormat);
        Assert.That(flag.Reason, Does.Contain("Home Team"));
    }

    [Test]
    public async Task AddManualGame_PlayedBeforeTheSeasonStarted_IsFlagged()
    {
        var seeded = await SeedAsync(Context);
        var dto = Entry(1, 0, playedAt: SeasonStart.AddDays(-1));

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.DateOutsideSeason);
        Assert.That(flag.Reason, Does.Contain("before the season started"));
    }

    [Test]
    public async Task AddManualGame_PlayedInTheFuture_IsFlagged()
    {
        var seeded = await SeedAsync(Context);
        var dto = Entry(1, 0, playedAt: _time.GetUtcNow().UtcDateTime.AddDays(1));

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.DateOutsideSeason);
        Assert.That(flag.Reason, Does.Contain("future"));
    }

    [Test]
    public async Task EditManualGame_FixingAMismatch_RemovesItsOpenFlag()
    {
        var seeded = await SeedAsync(Context);
        var gameID = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Entry(3, 1, players: [Line(seeded.HomeUsers[0].Id, true, goals: 1)]));
        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals), Is.True);

        await _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, gameID, Entry(1, 0, players: [Line(seeded.HomeUsers[0].Id, true, goals: 1)]));

        // The rosters are still short-handed (TeamSmallerThanFormat), but the score/goals mismatch this test is about is gone
        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals && f.DismissedAt == null), Is.False);
    }

    [Test]
    public async Task EditManualGame_StillMismatched_RewritesTheSameFlagRatherThanAddingAnother()
    {
        var seeded = await SeedAsync(Context);
        var gameID = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Entry(3, 1, players: [Line(seeded.HomeUsers[0].Id, true, goals: 1)]));

        await _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, gameID, Entry(5, 1, players: [Line(seeded.HomeUsers[0].Id, true, goals: 1)]));

        var flags = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals).ToListAsync();
        Assert.That(flags, Has.Count.EqualTo(1));
        Assert.That(flags[0].Reason, Does.Contain("is 5"));
    }

    [Test]
    public async Task EditManualGame_FixingADismissedMismatch_LeavesItDismissed()
    {
        var seeded = await SeedAsync(Context);
        var gameID = await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, Entry(3, 1, players: [Line(seeded.HomeUsers[0].Id, true, goals: 1)]));
        var flagID = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals).Select(f => f.StatFlagID).SingleAsync();
        await _staff.Dismiss(flagID, CancellationToken.None);

        await _service.EditManualGame(seeded.SeasonMatch.SeasonMatchID, gameID, Entry(1, 0, players: [Line(seeded.HomeUsers[0].Id, true, goals: 1)]));

        var flag = await Context.StatFlags.SingleAsync(f => f.StatFlagID == flagID);
        Assert.That(flag.IsOpen, Is.False);
    }

    [Test]
    public async Task CheckRecordedGameAsync_ANegativeStat_IsFlagged()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("negative-stat-game", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT101", Kills: -1, Deaths: 2, Assists: 0));
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.NegativeStat);
        Assert.That(flag.Reason, Does.Contain("-1 kills"));
    }

    [Test]
    public async Task CheckMatchAsync_OverTheMaxAccuracy_IsFlagged()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(101, "GT101"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddParticipant(0, 101, new MatchParticipantStats { Accuracy = 150f, ShotsFired = 10, ShotsHit = 10 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match.MatchID);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.ImpossibleAccuracy);
        Assert.That(flag.Reason, Does.Contain("150"));
    }

    [Test]
    public async Task CheckMatchAsync_ShotsHitAboveShotsFired_IsFlagged()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(101, "GT101"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddParticipant(0, 101, new MatchParticipantStats { ShotsFired = 10, ShotsHit = 20 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match.MatchID);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.ImpossibleAccuracy);
        Assert.That(flag.Reason, Does.Contain("20 shots hit"));
    }

    [Test]
    public async Task CheckMatchAsync_AnUndecodedCustomScore_IsFlagged()
    {
        var match = Domain.Halo.Match.Seed(Guid.NewGuid(), scoreLayout: CustomScoreLayout.Undecoded);
        match.AddTeam(0, 1, Outcomes.Won);
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match.MatchID);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.UndecodedCustomScore), Is.True);
    }

    [Test]
    public async Task CheckMatchAsync_ANegativeStat_IsFlagged_NamingThePlayerWhenTheyreLinked()
    {
        // HomeXbox[0] (101) is already seeded.HomeUsers[0]'s own Xbox account (SeasonMatchUnitTestData.NewUser)
        var seeded = await SeedAsync(Context);
        Context.XboxUsers.Add(XboxUser.FromXbox(202, "GT202"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddTeam(1, 0, Outcomes.Lost);
        match.AddParticipant(0, HomeXbox[0], new MatchParticipantStats { Kills = -1, Deaths = 2 });
        match.AddParticipant(1, 202, new MatchParticipantStats { Assists = -3 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match.MatchID);

        var flags = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.NegativeStat).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags, Has.Count.EqualTo(2));
            Assert.That(flags.Single(f => f.PlayerUserID == seeded.HomeUsers[0].Id).Reason, Does.Contain("-1 kills"));
            // The second player's Xbox account isn't anyone's: the flag still names them (by gamertag), just not a site player
            Assert.That(flags.Single(f => f.PlayerUserID == null).Reason, Does.Contain("-3 assists"));
        }
    }

    [Test]
    public async Task CheckMatchAsync_ThatDoesNotExist_DoesNothing()
    {
        await _statFlags.CheckMatchAsync(Guid.NewGuid());

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_WhenTheSeriesScoreDoesNotAddUpToItsGames_IsFlagged()
    {
        var season = Season.Seed(seasonName: "Archive Season", historicalSourceID: "src/sheet").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Reds");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Blues");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();

        // The archive's standings say 2-0, but only one of the series' games was itself recorded
        var match = SeasonMatch.Imported(season.SeasonID, home.TeamID, away.TeamID, homeWins: 2, awayWins: 0, bestOf: 3, scheduledTime: null, bracketMatch: null);
        var game = RecordedGame.Seed("archive-game-1", gameVersionID: Halo3);
        game.SeedSide(1, home.TeamID, Outcomes.Won, 1);
        game.SeedSide(2, away.TeamID, Outcomes.Lost, 0);
        Context.RecordedGames.Add(game);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        match.AddImportedGame(null, game, 1);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(match.SeasonMatchID);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Reason, Does.Contain("2-0"));
            Assert.That(flag.Reason, Does.Contain("1-0"));
            // A flag is of one game: the series' only recorded one
            Assert.That(flag.RecordedGameID, Is.EqualTo(game.RecordedGameID));
        }
    }

    [Test]
    public async Task CheckSeriesGamesAsync_WhenAGameOfTheSeriesIsAHaloPull_IsSkipped()
    {
        var seeded = await SeedAsync(Context, bestOf: 1);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.AddGame(waypoint, 1);
        seeded.SeasonMatch.DecideByGames(1, 0);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_ThatDoesNotExist_DoesNothing()
    {
        await _statFlags.CheckSeriesGamesAsync(999);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }
}
