using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
    public async Task AddManualGame_TheDecidingGameOfABestOfOneSeries_KeepsItsScoreFlag_OnceTheSeriesCheckAlsoRuns()
    {
        // Bo1: reporting this one game both flags its own score/goals mismatch (CheckRecordedGameAsync) and decides
        // the series (CheckSeriesGamesAsync, run right after by AddManualGame) - the series check must not remove a
        // flag it doesn't own on the very game it just reconciled
        var seeded = await SeedAsync(Context, bestOf: 1);
        var dto = Entry(3, 1,
            players:
            [
                Line(seeded.HomeUsers[0].Id, true, goals: 1),
                Line(seeded.HomeUsers[1].Id, true, goals: 1),
                Line(seeded.AwayUsers[0].Id, false, goals: 1),
            ]);

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals && f.DismissedAt == null), Is.True);
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
    public async Task CheckRecordedGameAsync_WhereTheWinnerDoesNotMatchTheScores_IsFlagged()
    {
        // Built directly, bypassing SeasonMatchService.SetManualSidesAndPlayers (which always derives the winner from
        // the scores): WinnerDoesNotMatchScoresFindings is a safety net for a save path that sets them separately
        var game = RecordedGame.Seed("mismatched-winner", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        game.SeedSide(1, outcome: Outcomes.Lost, score: 5, name: "Home");
        game.SeedSide(2, outcome: Outcomes.Won, score: 1, name: "Away");
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.WinnerDoesNotMatchScores);
        Assert.That(flag.Reason, Does.Contain("Home"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_WhereTheWinnerDoesNotMatchTheScores_WithNoTeamName_NamesTheSideByNumber()
    {
        // Same safety net as above, but with no side name recorded: the reason falls back to "Side <n>"
        var game = RecordedGame.Seed("mismatched-winner-unnamed", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        game.SeedSide(1, outcome: Outcomes.Lost, score: 5);
        game.SeedSide(2, outcome: Outcomes.Won, score: 1);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.WinnerDoesNotMatchScores);
        Assert.That(flag.Reason, Does.Contain("Side 1"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_WithOnlyOneSide_SkipsTheWinnerCheck_WithoutThrowing()
    {
        // Another safety net (same as above): a game somehow saved with other than two sides can't have "the side
        // with the higher score" compared against anything
        var game = RecordedGame.Seed("one-side-only", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        game.SeedSide(1, outcome: Outcomes.Won, score: 5, name: "Home");
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.WinnerDoesNotMatchScores), Is.False);
    }

    [Test]
    public async Task CheckRecordedGamesAsync_WithNoGames_DoesNothing()
    {
        await _statFlags.CheckRecordedGamesAsync([]);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckRecordedGamesAsync_WithNothingToFlag_SavesNothing()
    {
        var seeded = await SeedAsync(Context);
        var clean = RecordedGame.Seed("batch-clean-only", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        clean.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT1", Kills: 1, Deaths: 2, Assists: 0));
        Context.RecordedGames.Add(clean);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGamesAsync([clean]);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckRecordedGamesAsync_ChecksEveryGameAtOnce_FlaggingOnlyTheOneWithANegativeStat()
    {
        // The import's batched path (one flags query, one save) rather than once a game
        var seeded = await SeedAsync(Context);
        var flagged = RecordedGame.Seed("batch-negative", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        flagged.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT1", Kills: -1, Deaths: 2, Assists: 0));
        var clean = RecordedGame.Seed("batch-clean", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        clean.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[1].Id, 1, "GT2", Kills: 1, Deaths: 2, Assists: 0));
        Context.RecordedGames.AddRange(flagged, clean);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGamesAsync([flagged, clean]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.StatFlags.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.StatFlags.AnyAsync(f => f.RecordedGameID == flagged.RecordedGameID), Is.True);
        }
    }

    [Test]
    public async Task CheckRecordedGameAsync_WhereTheScoreDoesNotMatchGoals_WithNoTeamName_NamesTheSideByNumber()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("score-mismatch-unnamed", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        var home = game.SeedSide(1, score: 3);
        game.SeedSide(2, score: 1);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT1", Kills: 0, Deaths: 0, Assists: 0) { Goals = 1 }, home);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.ScoreDoesNotMatchGoals);
        Assert.That(flag.Reason, Does.Contain("Side 1's score is 3"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_WhereATeamIsSmallerThanTheFormat_WithNoTeamName_NamesTheSideByNumber()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("team-size-unnamed", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        var home = game.SeedSide(1);
        var away = game.SeedSide(2);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT1", Kills: 0, Deaths: 0, Assists: 0), home);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[0].Id, 2, "GT2", Kills: 0, Deaths: 0, Assists: 0), away);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[1].Id, 2, "GT3", Kills: 0, Deaths: 0, Assists: 0), away);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[2].Id, 2, "GT4", Kills: 0, Deaths: 0, Assists: 0), away);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[3].Id, 2, "GT5", Kills: 0, Deaths: 0, Assists: 0), away);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.TeamSmallerThanFormat);
        Assert.That(flag.Reason, Does.Contain("Side 1 fielded 1 player"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_BothSidesShortOfTheFormat_AreTwoFlags_AndFixingOneLeavesTheOther()
    {
        // Side 1 and side 2 are both too small: without a discriminator both findings would share the same
        // (TeamSmallerThanFormat, null) key
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("both-sides-short", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        var home = game.SeedSide(1);
        var away = game.SeedSide(2);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT1", Kills: 0, Deaths: 0, Assists: 0), home);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[0].Id, 2, "GT2", Kills: 0, Deaths: 0, Assists: 0), away);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flags = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.TeamSmallerThanFormat).ToListAsync();
        Assert.That(flags, Has.Count.EqualTo(2));

        // Side 1 is brought up to the format's minimum (4); side 2 is left short. A shared key would have rewritten
        // one of these flags twice and never cleared the other
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[1].Id, 1, "GT3", Kills: 0, Deaths: 0, Assists: 0), home);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[2].Id, 1, "GT4", Kills: 0, Deaths: 0, Assists: 0), home);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[3].Id, 1, "GT5", Kills: 0, Deaths: 0, Assists: 0), home);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var open = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.TeamSmallerThanFormat && f.DismissedAt == null).ToListAsync();
        Assert.That(open.Single().Reason, Does.Contain("Side 2"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_ATeamLargerThanTheFormat_IsFlagged()
    {
        // #548, fourth review: the format is now a range (default 4-5), not just a floor - a side that's grown well
        // past it is worth a look too
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("team-size-large", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        var home = game.SeedSide(1);
        game.SeedSide(2);
        var sixPlayers = seeded.HomeUsers.Concat(seeded.AwayUsers.Take(2)).ToArray();
        for (var i = 0; i < sixPlayers.Length; i++)
            game.SeedPlayer(new RecordedPlayerLine(sixPlayers[i].Id, 1, $"GT{i}", Kills: 0, Deaths: 0, Assists: 0), home);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.TeamSmallerThanFormat && f.Discriminator == 1);
        Assert.That(flag.Reason, Does.Contain("Side 1 fielded 6 player"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_WithACustomMaxTeamSize_OnlyFlagsPastIt()
    {
        // #548, fourth review: StatFlagOptions.MaxTeamSize is configurable - a non-default value must actually
        // change what gets flagged, not just read differently
        var seeded = await SeedAsync(Context);
        var statFlags = new StatFlagService(Context, _time, options: Options.Create(new StatFlagOptions { MaxTeamSize = 6 }));
        var game = RecordedGame.Seed("team-size-custom-max", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        var home = game.SeedSide(1);
        var sixPlayers = seeded.HomeUsers.Concat(seeded.AwayUsers.Take(2)).ToArray();
        for (var i = 0; i < sixPlayers.Length; i++)
            game.SeedPlayer(new RecordedPlayerLine(sixPlayers[i].Id, 1, $"GT{i}", Kills: 0, Deaths: 0, Assists: 0), home);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.TeamSmallerThanFormat && f.Discriminator == 1), Is.False);
    }

    [Test]
    public async Task CheckMatchAsync_WithACustomMaxAccuracy_OnlyFlagsPastIt()
    {
        // #548, fourth review: StatFlagOptions.MaxAccuracy is configurable - a non-default value must actually
        // change what gets flagged
        Context.XboxUsers.Add(XboxUser.FromXbox(101, "GT101"));
        var statFlags = new StatFlagService(Context, _time, options: Options.Create(new StatFlagOptions { MaxAccuracy = 200f }));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddParticipant(0, 101, new MatchParticipantStats { Accuracy = 150f, ShotsFired = 10, ShotsHit = 10 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await statFlags.CheckMatchAsync(match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.ImpossibleAccuracy), Is.False);
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

        await _statFlags.CheckMatchAsync(match);

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

        await _statFlags.CheckMatchAsync(match);

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

        await _statFlags.CheckMatchAsync(match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.UndecodedCustomScore), Is.True);
    }

    [Test]
    public async Task CheckMatchAsync_ANegativeStat_IsFlagged_NamingThePlayerWhenTheyreLinked()
    {
        // HomeXbox[0] (101) is already seeded.HomeUsers[0]'s own Xbox account (SeasonMatchUnitTestData.NewUser); 999
        // is nobody's (HomeXbox, AwayXbox and OtherXbox never reach it)
        var seeded = await SeedAsync(Context);
        Context.XboxUsers.Add(XboxUser.FromXbox(999, "GT999"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddTeam(1, 0, Outcomes.Lost);
        match.AddParticipant(0, HomeXbox[0], new MatchParticipantStats { Kills = -1, Deaths = 2 });
        match.AddParticipant(1, 999, new MatchParticipantStats { Assists = -3 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match);

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
    public async Task CheckMatchAsync_ANegativeScoreAlone_IsNotFlagged()
    {
        // #548: Score includes suicides and betrayals, which normal play can easily make negative; kills, deaths,
        // assists and goals only, as the rule's own doc says
        Context.XboxUsers.Add(XboxUser.FromXbox(101, "GT101"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddParticipant(0, 101, new MatchParticipantStats { Score = -5, Kills = 1, Deaths = 1, Assists = 1 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.NegativeStat), Is.False);
    }

    [Test]
    public async Task CheckMatchAsync_TwoUnlinkedPlayersBothNegative_AreTwoFlags_AndFixingOneLeavesTheOther()
    {
        // Neither 998 nor 999 is anyone's Xbox account: without a discriminator both findings would share the same
        // (NegativeStat, null) key
        Context.XboxUsers.Add(XboxUser.FromXbox(998, "GT998"));
        Context.XboxUsers.Add(XboxUser.FromXbox(999, "GT999"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddTeam(1, 0, Outcomes.Lost);
        match.AddParticipant(0, 998, new MatchParticipantStats { Assists = -1 });
        match.AddParticipant(1, 999, new MatchParticipantStats { Assists = -2 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match);

        var flags = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.NegativeStat).ToListAsync();
        Assert.That(flags, Has.Count.EqualTo(2));

        // A second look at the same match, 998's line no longer negative (it's immutable once pulled, so this is a
        // fresh in-memory copy standing in for a corrected re-pull): its flag should clear, 999's should stay - a
        // shared key would have left both open, or cleared the wrong one
        var corrected = Domain.Halo.Match.Seed(match.MatchID);
        corrected.AddTeam(0, 1, Outcomes.Won);
        corrected.AddTeam(1, 0, Outcomes.Lost);
        corrected.AddParticipant(0, 998, new MatchParticipantStats { Assists = 1 });
        corrected.AddParticipant(1, 999, new MatchParticipantStats { Assists = -2 });
        await _statFlags.CheckMatchAsync(corrected);

        var open = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.NegativeStat && f.DismissedAt == null).ToListAsync();
        Assert.That(open.Single().Reason, Does.Contain("-2 assists"));
    }

    [Test]
    public async Task CheckMatchAsync_GivenLookupsAlready_UsesThemRatherThanQueryingItsOwn()
    {
        // A bulk pull looks these up once for every match it saves, not once a match (DataPullService.SaveMatchesAsync)
        Context.XboxUsers.Add(XboxUser.FromXbox(101, "GT101"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddParticipant(0, 101, new MatchParticipantStats { Assists = -1 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match, userIDByXuid: new Dictionary<long, int>(), gamertagByXuid: new Dictionary<long, string> { [101] = "GivenGamertag" });

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.NegativeStat);
        Assert.That(flag.Reason, Does.StartWith("GivenGamertag:"));
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
            // #548, third review: a series flag is of the series itself, never a particular game
            Assert.That(flag.SeasonMatchID, Is.EqualTo(match.SeasonMatchID));
            Assert.That(flag.RecordedGameID, Is.Null);
        }
    }

    [Test]
    public async Task CheckRecordedGameAsync_ReimportingTheSameGame_LeavesTheSeriesFlagAlone()
    {
        // The historical import re-checks a game's "any game" rules every time it's (re-)imported
        // (HistoricalImportService.ImportRecordedGames): that must not touch a SeriesDoesNotMatchGames flag, which is
        // of the series itself (StatFlag.SeasonMatchID, third review), never reachable through the game at all
        var season = Season.Seed(seasonName: "Archive Season", historicalSourceID: "src/sheet").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Reds");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Blues");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();

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
        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames), Is.True);

        // A re-import re-checks the game itself (never seasonMatch, per HistoricalImportService.ImportRecordedGames)
        await _statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames && f.DismissedAt == null), Is.True);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_OnceTheSeriesIsReopened_ClearsItsFlagRatherThanLeavingItStuckOpen()
    {
        var season = Season.Seed(seasonName: "Archive Season", historicalSourceID: "src/sheet").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Reds");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Blues");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();

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
        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames && f.DismissedAt == null), Is.True);

        // No longer decided by games (UndoResult re-checks right after Reopen, before it clears the games)
        match.Reopen();
        await Context.SaveChangesAsync();
        await _statFlags.CheckSeriesGamesAsync(match.SeasonMatchID);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames && f.DismissedAt == null), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_AllWaypoint_ThatAddsUp_IsNotFlagged()
    {
        // #548, fourth review: a series with a Waypoint game is actually checked now (by its rosters, SeriesSides),
        // not skipped outright - this one adds up
        var seeded = await SeedAsync(Context, bestOf: 1);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.AddGame(waypoint, 1);
        seeded.SeasonMatch.DecideByGames(1, 0);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_AMixedSeriesThatAddsUp_IsNotFlagged()
    {
        // #548, fourth review: a series mixing a Waypoint game and a recorded one - the Waypoint game's side is
        // decided by its rosters, the recorded game's by its sides, and the series adds up
        var seeded = await SeedAsync(Context, bestOf: 3);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.AddGame(waypoint, 1);
        var recorded = RecordedGame.Seed("mixed-adds-up", gameVersionID: Halo3);
        recorded.SeedSide(1, seeded.Home.TeamID, Outcomes.Won, 1);
        recorded.SeedSide(2, seeded.Away.TeamID, Outcomes.Lost, 0);
        Context.RecordedGames.Add(recorded);
        seeded.SeasonMatch.Game(2, recorded: recorded);
        seeded.SeasonMatch.DecideByGames(2, 0);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(seeded.SeasonMatch.SeasonMatchID);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_AMixedSeriesThatDoesNotAddUp_IsFlagged()
    {
        var seeded = await SeedAsync(Context, bestOf: 3);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, HomeXbox, Outcomes.Won, AwayXbox, Outcomes.Lost);
        seeded.SeasonMatch.AddGame(waypoint, 1);
        // Away actually won this one, but the series is recorded as a 2-0 sweep
        var recorded = RecordedGame.Seed("mixed-mismatch", gameVersionID: Halo3);
        recorded.SeedSide(1, seeded.Home.TeamID, Outcomes.Lost, 0);
        recorded.SeedSide(2, seeded.Away.TeamID, Outcomes.Won, 1);
        Context.RecordedGames.Add(recorded);
        seeded.SeasonMatch.Game(2, recorded: recorded);
        seeded.SeasonMatch.DecideByGames(2, 0);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(seeded.SeasonMatch.SeasonMatchID);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Reason, Does.Contain("2-0"));
            Assert.That(flag.Reason, Does.Contain("1-1"));
        }
    }

    [Test]
    public async Task CheckSeriesGamesAsync_AWaypointGamesRosterNoLongerMatchesEitherTeam_IsFlaggedAsUndecidable()
    {
        // #548, fourth review: a trade or a merge since means the game's players are on neither roster any more -
        // the series can't be checked, and that's flagged by name rather than silently skipped
        var seeded = await SeedAsync(Context, bestOf: 1);
        var waypoint = await AddMatchAsync(Context, 1, InSeason, OtherXbox[..4], Outcomes.Won, OtherXbox[4..8], Outcomes.Lost);
        seeded.SeasonMatch.AddGame(waypoint, 1);
        seeded.SeasonMatch.DecideByGames(1, 0);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(seeded.SeasonMatch.SeasonMatchID);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Reason, Does.Contain("Game 1"));
            Assert.That(flag.Reason, Does.Contain("can't be checked"));
        }
    }

    [Test]
    public async Task CheckSeriesGamesAsync_ThatDoesNotExist_DoesNothing()
    {
        await _statFlags.CheckSeriesGamesAsync(999);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_AGameWithANonSeasonSide_IsCountedByPosition_NotTeamID()
    {
        // #548, third review: the import counts a series' wins by a game's recorded side (position), the other way
        // round when the teams played swapped - never by matching a side's TeamID to the series' (RecordedGame.WonBy),
        // which undercounts a side the import never resolved to a season team (TeamID null). Here game 1's side 1
        // (home position, unswapped) has no TeamID at all, yet won: the import's own tally (stored 1-0) must still
        // match, rather than counting it toward neither side.
        var season = Season.Seed(seasonName: "Archive Season", historicalSourceID: "src/sheet").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Reds");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Blues");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();

        var match = SeasonMatch.Imported(season.SeasonID, home.TeamID, away.TeamID, homeWins: 1, awayWins: 0, bestOf: 1, scheduledTime: null, bracketMatch: null);
        var game = RecordedGame.Seed("archive-null-side", gameVersionID: Halo3);
        game.SeedSide(1, null, Outcomes.Won, 1); // Unresolved side, but still the home position: it won
        game.SeedSide(2, away.TeamID, Outcomes.Lost, 0);
        Context.RecordedGames.Add(game);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        match.AddImportedGame(null, game, 1);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync(match.SeasonMatchID);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.SeriesDoesNotMatchGames), Is.False);
    }

    [Test]
    public async Task CheckSeriesGamesAsync_Batched_ChecksEverySeriesAtOnce()
    {
        // #548, third review: the import's batched path (one load, one flags query, one save) rather than once a series
        var season = Season.Seed(seasonName: "Archive Season", historicalSourceID: "src/sheet").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Reds");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Blues");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();

        var mismatched = SeasonMatch.Imported(season.SeasonID, home.TeamID, away.TeamID, homeWins: 2, awayWins: 0, bestOf: 3, scheduledTime: null, bracketMatch: null);
        var mismatchedGame = RecordedGame.Seed("archive-batch-mismatch", gameVersionID: Halo3);
        mismatchedGame.SeedSide(1, home.TeamID, Outcomes.Won, 1);
        mismatchedGame.SeedSide(2, away.TeamID, Outcomes.Lost, 0);
        var matching = SeasonMatch.Imported(season.SeasonID, home.TeamID, away.TeamID, homeWins: 1, awayWins: 0, bestOf: 1, scheduledTime: null, bracketMatch: null);
        var matchingGame = RecordedGame.Seed("archive-batch-match", gameVersionID: Halo3);
        matchingGame.SeedSide(1, home.TeamID, Outcomes.Won, 1);
        matchingGame.SeedSide(2, away.TeamID, Outcomes.Lost, 0);
        Context.RecordedGames.AddRange(mismatchedGame, matchingGame);
        Context.SeasonMatches.AddRange(mismatched, matching);
        await Context.SaveChangesAsync();
        mismatched.AddImportedGame(null, mismatchedGame, 1);
        matching.AddImportedGame(null, matchingGame, 1);
        await Context.SaveChangesAsync();

        await _statFlags.CheckSeriesGamesAsync([mismatched.SeasonMatchID, matching.SeasonMatchID]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.StatFlags.CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.StatFlags.AnyAsync(f => f.SeasonMatchID == mismatched.SeasonMatchID), Is.True);
        }
    }

    [Test]
    public async Task CheckSeriesGamesAsync_Batched_WithNoSeries_DoesNothing()
    {
        await _statFlags.CheckSeriesGamesAsync([]);

        Assert.That(await Context.StatFlags.AnyAsync(), Is.False);
    }

    [Test]
    public async Task CheckMatchAsync_ANegativeStat_KeepsItsFlag_WhenThePlayerIsLinkedLater()
    {
        // #548, third review: a Halo finding's identity is always the player's XUID, linked or not - linking an
        // account later must rewrite the same flag in place, never orphan it (leaving it stuck open forever) and
        // raise a new one for the same finding
        Context.XboxUsers.Add(XboxUser.FromXbox(555, "GT555"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 1, Outcomes.Won);
        match.AddParticipant(0, 555, new MatchParticipantStats { Kills = -1, Deaths = 2 });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();

        await _statFlags.CheckMatchAsync(match, userIDByXuid: new Dictionary<long, int>(), gamertagByXuid: new Dictionary<long, string>());
        var before = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.NegativeStat);
        Assert.That(before.PlayerUserID, Is.Null);

        await _statFlags.CheckMatchAsync(match, userIDByXuid: new Dictionary<long, int> { [555] = 42 }, gamertagByXuid: new Dictionary<long, string>());

        var flags = await Context.StatFlags.Where(f => f.Rule == StatFlagRule.NegativeStat).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flags, Has.Count.EqualTo(1), "The same flag, not orphaned with a new one raised alongside it");
            Assert.That(flags.Single().StatFlagID, Is.EqualTo(before.StatFlagID));
            Assert.That(flags.Single().PlayerUserID, Is.EqualTo(42));
        }
    }

    [Test]
    public async Task CheckRecordedGameAsync_TheSameFinding_KeepsItsFoundAt_OnARerun()
    {
        // #548, third review: a re-run that finds exactly the same thing must not bump FoundAt (or it would jump to
        // the top of the staff list's newest-first order, and look freshly found when it's not)
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("rerun-same-finding", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "GT1", Kills: -1, Deaths: 2, Assists: 0));
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        await _statFlags.CheckRecordedGameAsync(game, null);
        var before = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.NegativeStat);
        _time.Advance(TimeSpan.FromDays(1));

        await _statFlags.CheckRecordedGameAsync(game, null);

        var after = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.NegativeStat);
        Assert.That(after.FoundAt, Is.EqualTo(before.FoundAt));
    }

    [Test]
    public async Task AddManualGame_PlayedAFewMinutesInTheFuture_IsNotFlagged()
    {
        // #548, third review: a small future margin (StatFlagThresholds.FutureMargin) lets a clock a little fast, or
        // entering the game moments after it ended, through without a flag
        var seeded = await SeedAsync(Context);
        var dto = Entry(1, 0, playedAt: _time.GetUtcNow().UtcDateTime.AddMinutes(10));

        await _service.AddManualGame(seeded.SeasonMatch.SeasonMatchID, dto);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.DateOutsideSeason), Is.False);
    }

    private async Task<RecordedGame> SeedKillsAndDeathsGameAsync(string key, (int Kills, int Deaths) home, (int Kills, int Deaths) away)
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed(key, gameVersionID: Halo3, origin: RecordedGameOrigin.Manual);
        var homeSide = game.SeedSide(1);
        var awaySide = game.SeedSide(2);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "H", home.Kills, home.Deaths, 0), homeSide);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[0].Id, 2, "A", away.Kills, away.Deaths, 0), awaySide);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        return game;
    }

    [Test]
    public async Task CheckRecordedGameAsync_ASideWithFarMoreKillsThanTheOtherHasDeaths_IsFlagged()
    {
        // 40 kills against 10 deaths: past 10 * 1.5 + 10 = 25
        var game = await SeedKillsAndDeathsGameAsync("kills-over-deaths", home: (40, 30), away: (20, 10));

        await _statFlags.CheckRecordedGameAsync(game, null);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.KillsDoNotMatchDeaths);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Discriminator, Is.EqualTo(1));
            Assert.That(flag.Reason, Does.Contain("40 kills").And.Contain("only 10 deaths"));
        }
    }

    [Test]
    public async Task CheckRecordedGameAsync_KillsWithinTheAllowanceOfTheOthersDeaths_AreNotFlagged()
    {
        // suicides and betrayals make deaths exceed kills, never the other way: 20 kills vs 10 deaths is inside 25
        var game = await SeedKillsAndDeathsGameAsync("kills-within-allowance", home: (20, 30), away: (30, 10));

        await _statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.KillsDoNotMatchDeaths), Is.False);
    }

    [Test]
    public async Task CheckRecordedGameAsync_WithACustomKillsAllowance_OnlyFlagsPastIt()
    {
        var game = await SeedKillsAndDeathsGameAsync("kills-custom-allowance", home: (40, 30), away: (20, 10));
        var statFlags = new StatFlagService(Context, _time, options: Options.Create(new StatFlagOptions { KillsOverDeathsPercent = 400f }));

        await statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.KillsDoNotMatchDeaths), Is.False);
    }

    [Test]
    public async Task CheckRecordedGameAsync_KillsAndDeathsFixed_RemovesItsOpenFlag()
    {
        var game = await SeedKillsAndDeathsGameAsync("kills-fixed", home: (40, 30), away: (20, 10));
        await _statFlags.CheckRecordedGameAsync(game, null);
        Assert.That(await Context.StatFlags.CountAsync(f => f.Rule == StatFlagRule.KillsDoNotMatchDeaths), Is.EqualTo(1));

        game.SetPlayers([
            new RecordedPlayerLine(game.Participants.First(p => p.Gamertag == "H").UserID, 1, "H", 10, 30, 0),
            new RecordedPlayerLine(game.Participants.First(p => p.Gamertag == "A").UserID, 2, "A", 20, 10, 0)]);
        await Context.SaveChangesAsync();
        await _statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.KillsDoNotMatchDeaths), Is.False);
    }

    [Test]
    public async Task CheckRecordedGameAsync_NotManual_SkipsTheKillsAndDeathsCheck()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("kills-imported", gameVersionID: Halo3, seasonID: seeded.Season.SeasonID);
        var homeSide = game.SeedSide(1);
        var awaySide = game.SeedSide(2);
        game.SeedPlayer(new RecordedPlayerLine(seeded.HomeUsers[0].Id, 1, "H", 99, 0, 0), homeSide);
        game.SeedPlayer(new RecordedPlayerLine(seeded.AwayUsers[0].Id, 2, "A", 0, 1, 0), awaySide);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, null);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.KillsDoNotMatchDeaths), Is.False);
    }

    private async Task<(RecordedGame Game, SeasonMatch Match)> SeedScheduledManualGameAsync(string key, DateTime scheduled, DateTime playedAt)
    {
        var seeded = await SeedAsync(Context);
        seeded.SeasonMatch.ScheduleAt(scheduled);
        var game = RecordedGame.Seed(key, gameVersionID: Halo3, origin: RecordedGameOrigin.Manual, playedAt: playedAt);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        return (game, seeded.SeasonMatch);
    }

    [Test]
    public async Task CheckRecordedGameAsync_PlayedWellAwayFromTheScheduledTime_IsFlagged()
    {
        var (game, match) = await SeedScheduledManualGameAsync("week-far", InSeason, InSeason.AddDays(5));

        await _statFlags.CheckRecordedGameAsync(game, match);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.DateOutsideMatchWeek);
        Assert.That(flag.Reason, Does.Contain("5 day(s)"));
    }

    [Test]
    public async Task CheckRecordedGameAsync_PlayedWithinAFewDaysOfTheScheduledTime_IsNotFlagged()
    {
        var (game, match) = await SeedScheduledManualGameAsync("week-near", InSeason, InSeason.AddDays(-2));

        await _statFlags.CheckRecordedGameAsync(game, match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.DateOutsideMatchWeek), Is.False);
    }

    [Test]
    public async Task CheckRecordedGameAsync_OfAMatchNeverScheduled_SkipsTheMatchWeekCheck()
    {
        var seeded = await SeedAsync(Context);
        var game = RecordedGame.Seed("week-unscheduled", gameVersionID: Halo3, origin: RecordedGameOrigin.Manual, playedAt: InSeason);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _statFlags.CheckRecordedGameAsync(game, seeded.SeasonMatch);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.DateOutsideMatchWeek), Is.False);
    }

    [Test]
    public async Task CheckRecordedGameAsync_WithACustomMaxDaysFromScheduled_OnlyFlagsPastIt()
    {
        var (game, match) = await SeedScheduledManualGameAsync("week-custom", InSeason, InSeason.AddDays(5));
        var statFlags = new StatFlagService(Context, _time, options: Options.Create(new StatFlagOptions { MaxDaysFromScheduled = 6 }));

        await statFlags.CheckRecordedGameAsync(game, match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.DateOutsideMatchWeek), Is.False);
    }

    private async Task<Domain.Halo.Match> SeedTimedMatchAsync(TimeSpan duration, int kills, int deaths, int goals)
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(101, "GT101"));
        var match = Domain.Halo.Match.Seed(Guid.NewGuid(), duration: duration);
        match.AddTeam(0, 1, Outcomes.Won);
        // An undecoded match's goals are its Score (at most Match.PlainMaxScore)
        match.AddParticipant(0, 101, new MatchParticipantStats { Kills = kills, Deaths = deaths, Score = goals });
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();
        return match;
    }

    [Test]
    public async Task CheckMatchAsync_KillsBeyondTheMatchLength_AreFlagged()
    {
        // 70 kills in 10 minutes is past 6 a minute
        var match = await SeedTimedMatchAsync(TimeSpan.FromMinutes(10), kills: 70, deaths: 5, goals: 1);

        await _statFlags.CheckMatchAsync(match);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.StatsExceedGameLength);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(flag.Discriminator, Is.EqualTo(101));
            Assert.That(flag.Reason, Does.Contain("70 kills").And.Contain("10-minute"));
        }
    }

    [Test]
    public async Task CheckMatchAsync_GoalsBeyondTheMatchLength_AreFlagged()
    {
        // 15 goals in 3 minutes is past 2 a minute
        var match = await SeedTimedMatchAsync(TimeSpan.FromMinutes(3), kills: 5, deaths: 5, goals: 15);

        await _statFlags.CheckMatchAsync(match);

        var flag = await Context.StatFlags.SingleAsync(f => f.Rule == StatFlagRule.StatsExceedGameLength);
        Assert.That(flag.Reason, Does.Contain("15 goals"));
    }

    [Test]
    public async Task CheckMatchAsync_StatsWithinTheMatchLength_AreNotFlagged()
    {
        var match = await SeedTimedMatchAsync(TimeSpan.FromMinutes(10), kills: 40, deaths: 30, goals: 5);

        await _statFlags.CheckMatchAsync(match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.StatsExceedGameLength), Is.False);
    }

    [Test]
    public async Task CheckMatchAsync_WithNoDuration_SkipsTheMatchLengthCheck()
    {
        var match = await SeedTimedMatchAsync(TimeSpan.Zero, kills: 500, deaths: 5, goals: 100);

        await _statFlags.CheckMatchAsync(match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.StatsExceedGameLength), Is.False);
    }

    [Test]
    public async Task CheckMatchAsync_WithACustomRate_OnlyFlagsPastIt()
    {
        var match = await SeedTimedMatchAsync(TimeSpan.FromMinutes(10), kills: 70, deaths: 5, goals: 1);
        var statFlags = new StatFlagService(Context, _time, options: Options.Create(new StatFlagOptions { MaxKillsOrDeathsPerMinute = 10f }));

        await statFlags.CheckMatchAsync(match);

        Assert.That(await Context.StatFlags.AnyAsync(f => f.Rule == StatFlagRule.StatsExceedGameLength), Is.False);
    }
}
