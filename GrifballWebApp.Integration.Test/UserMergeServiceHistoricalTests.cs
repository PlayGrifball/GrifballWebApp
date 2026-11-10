using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Integration.Test;

/// <summary>A merge moves historical stats and championships before the source is deleted (their foreign keys
/// are restricted), and refuses what it can't settle before changing anything.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeServiceHistoricalTests
{
    private GrifballContext _context;
    private UserMergeService _service;
    private User _real;
    private User _dummy;
    private Season _season;
    private Season _other;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new UserMergeService(_context);
        _real = User.Seed(userName: "real", displayName: "Real");
        _dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        _season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        _other = HistoricalSeason("Spring League 2019", "agla/spring-league-2019");
        _context.AddRange(_real, _dummy, _season, _other);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Merge_MovesStatsAndChampionships_ThenDeletesThePlaceholder()
    {
        _context.PlayerSeasonStats.Add(Stat(_dummy, _season, kills: 10));
        _context.Set<PlayerChampionship>().Add(Title(_dummy, _season, "Pro"));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Users.AnyAsync(u => u.Id == _dummy.Id), Is.False);
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_real.Id));
            Assert.That(await _context.Set<PlayerChampionship>().Select(s => s.UserID).SingleAsync(), Is.EqualTo(_real.Id));
        }
    }

    [Test]
    public async Task Merge_MovesThePlaceholdersRecordedGames()
    {
        var game = RecordedGame.Seed("mcc/1", seasonID: _season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(_dummy.Id, null, "Old Tag", 3, 0, 0));
        _context.RecordedGames.Add(game);
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.Set<RecordedGameParticipant>().Select(p => p.UserID).SingleAsync(), Is.EqualTo(_real.Id), "Moved before the placeholder is deleted");
    }

    [Test]
    public async Task Merge_BothInARecordedGame_LeavesTheTargetsLineOnly()
    {
        var game = RecordedGame.Seed("mcc/2", seasonID: _season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(_dummy.Id, null, "Old Tag", 3, 0, 0));
        game.SeedPlayer(new RecordedPlayerLine(_real.Id, null, "Real", 5, 0, 0));
        _context.RecordedGames.Add(game);
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.Set<RecordedGameParticipant>().Select(p => new { p.UserID, p.Kills }).ToListAsync(),
            Is.EqualTo(new[] { new { UserID = _real.Id, Kills = 5 } }), "One person, one line: the stats count them once");
    }

    private MatchmakingSeason LateLeague()
    {
        var late = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.MatchmakingSeasons.Add(late);
        return late;
    }

    private static MatchmakingPlayerStat LateStat(User user, MatchmakingSeason season)
        => MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = user.DisplayName!, Kills = 4, GamesPlayed = 1 }, gameVersionID: 1, matchmakingSeason: season, user: user);

    [Test]
    public async Task Merge_MovesThePlaceholdersLateLeagueStats()
    {
        _context.MatchmakingPlayerStats.Add(LateStat(_dummy, LateLeague()));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.MatchmakingPlayerStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_real.Id));
    }

    [Test]
    public async Task Merge_BothWithLateLeagueStatsForOneSeason_IsRefused_NamingIt()
    {
        var late = LateLeague();
        _context.MatchmakingPlayerStats.AddRange(LateStat(_real, late), LateStat(_dummy, late));
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(_real.Id, _dummy.Id));

        Assert.That(ex!.Message, Does.Contain("Late League Season 8 (Halo 3)"));
    }

    [Test]
    public async Task DeletingTheSource_WithoutMovingItsLateLeagueStats_IsRefused()
    {
        _context.MatchmakingPlayerStats.Add(LateStat(_dummy, LateLeague()));
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<UserHasHistoricalStatsException>(() => _service.Merge(_real.Id, _dummy.Id, new MergeOptions { TransferSeasonStats = false }));
    }

    [Test]
    public async Task Merge_BothWithStatsForOneSeasonAndStint_IsRefused_AndNothingChanges()
    {
        _context.PlayerSeasonStats.AddRange(Stat(_real, _season, kills: 1), Stat(_dummy, _season, kills: 2));
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(_real.Id, _dummy.Id));

        Assert.That(ex!.Message, Does.Contain("Winter League 2019"));
        _context.ChangeTracker.Clear();
        Assert.That(await _context.Users.AnyAsync(u => u.Id == _dummy.Id));
        Assert.That(await _context.PlayerSeasonStats.CountAsync(s => s.UserID == _dummy.Id), Is.EqualTo(1));
    }

    [Test]
    public async Task Merge_BothWithPlayoffStatsForOneSeason_IsRefused_NamingThePlayoffs()
    {
        _context.PlayerSeasonStats.AddRange(Stat(_real, _season, kills: 1, playoffs: true), Stat(_dummy, _season, kills: 2, playoffs: true));
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(_real.Id, _dummy.Id));

        Assert.That(ex!.Message, Does.Contain("the playoffs of season Winter League 2019"));
    }

    [Test]
    public async Task TheMergeClashQueries_AgreeWithTheRowsBools()
    {
        // The real user's rows against the placeholder's: the same stint, another stint, the playoffs of another season
        PlayerSeasonStat[] mine = [Stat(_real, _season, kills: 1), Stat(_real, _season, kills: 1, stint: 2), Stat(_real, _other, kills: 1, playoffs: true)];
        PlayerSeasonStat[] theirs = [Stat(_dummy, _season, kills: 2), Stat(_dummy, _season, kills: 2, playoffs: true), Stat(_dummy, _other, kills: 2, playoffs: true)];
        var late = LateLeague();
        var later = MatchmakingSeason.Seed("Late League Season 9 (Halo 3)", new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2021, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.MatchmakingSeasons.Add(later);
        MatchmakingPlayerStat[] mineLate = [LateStat(_real, late), LateStat(_real, later)];
        MatchmakingPlayerStat[] theirsLate = [LateStat(_dummy, late)];
        _context.PlayerSeasonStats.AddRange(mine.Concat(theirs));
        _context.MatchmakingPlayerStats.AddRange(mineLate.Concat(theirsLate));
        await _context.SaveChangesAsync();

        var queried = await _context.PlayerSeasonStats.Where(PlayerSeasonStat.ClashesOnMerge(_context.PlayerSeasonStats, _dummy.Id, _real.Id))
            .Select(s => s.PlayerSeasonStatID).OrderBy(id => id).ToListAsync();
        var queriedLate = await _context.MatchmakingPlayerStats.Where(MatchmakingPlayerStat.ClashesOnMerge(_context.MatchmakingPlayerStats, _dummy.Id, _real.Id))
            .Select(s => s.MatchmakingPlayerStatID).OrderBy(id => id).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(queried, Is.EqualTo(new[] { mine[0].PlayerSeasonStatID, mine[2].PlayerSeasonStatID }));
            Assert.That(mine.Where(m => theirs.Any(t => t.ClashesWith(m))).Select(m => m.PlayerSeasonStatID).Order(), Is.EqualTo(queried), "The bool, on the loaded rows");
            Assert.That(queriedLate, Is.EqualTo(new[] { mineLate[0].MatchmakingPlayerStatID }));
            Assert.That(mineLate.Where(m => theirsLate.Any(t => t.ClashesWith(m))).Select(m => m.MatchmakingPlayerStatID), Is.EqualTo(queriedLate), "The bool, on the loaded rows");
            Assert.That((theirs[0].IsSameStintAs(mine[0]), theirsLate[0].IsSameSlotAs(mineLate[0])), Is.EqualTo((false, false)), "A slot is a user's: only the clash ignores them");
        }
    }

    [Test]
    public async Task Merge_DifferentStintsOfOneSeason_AreBothKept()
    {
        _context.PlayerSeasonStats.AddRange(Stat(_real, _season, kills: 1), Stat(_dummy, _season, kills: 2, stint: 2));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.PlayerSeasonStats.CountAsync(s => s.UserID == _real.Id), Is.EqualTo(2));
    }

    [Test]
    public async Task DeletingTheSource_WithoutMovingItsStats_IsRefusedWith409_AndNothingChanges()
    {
        _context.PlayerSeasonStats.Add(Stat(_dummy, _season, kills: 10));
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UserHasHistoricalStatsException>(() =>
            _service.Merge(_real.Id, _dummy.Id, new MergeOptions { TransferSeasonStats = false }));

        Assert.That(ex!.UserId, Is.EqualTo(_dummy.Id));
        _context.ChangeTracker.Clear();
        Assert.That(await _context.Users.AnyAsync(u => u.Id == _dummy.Id));
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_dummy.Id));
    }

    [Test]
    public async Task DeletingTheSource_WithoutMovingItsTitles_IsRefused()
    {
        _context.Set<PlayerChampionship>().Add(Title(_dummy, _season, "Pro"));
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<UserHasHistoricalStatsException>(() =>
            _service.Merge(_real.Id, _dummy.Id, new MergeOptions { TransferChampionships = false }));
    }

    [Test]
    public async Task KeepingTheSource_LeavesWhatIsNotMoved()
    {
        _context.PlayerSeasonStats.Add(Stat(_dummy, _season, kills: 10));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id, new MergeOptions { TransferSeasonStats = false, DeleteMergeFrom = false });

        _context.ChangeTracker.Clear();
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_dummy.Id));
    }

    [Test]
    public async Task Merge_ATitleBothHold_IsKeptOnce()
    {
        _context.Set<PlayerChampionship>().AddRange(Title(_real, _season, "Pro"), Title(_dummy, _season, "pro"), Title(_dummy, _other, "Pro"));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.Set<PlayerChampionship>().CountAsync(c => c.UserID == _real.Id), Is.EqualTo(2));
    }

    private static Season HistoricalSeason(string name, string sourceId) => Season.Seed(seasonName: name, historicalSourceID: sourceId, gameVersionID: 1).WithDates(signupsOpen: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), signupsClose: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), draftStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static PlayerSeasonStat Stat(User user, Season season, int kills, bool playoffs = false, int stint = 1)
        => PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = user.DisplayName!, Kills = kills }, stintIndex: stint, isPlayoffs: playoffs);

    private static PlayerChampionship Title(User user, Season season, string tier, bool isCaptain = false) => PlayerChampionship.Seed(
        userID: user.Id, seasonID: season.SeasonID, tier: tier, isCaptain: isCaptain, gamertagUsed: user.DisplayName!,
        team: Team.Seed(seasonID: season.SeasonID, teamName: $"{tier} champs {user.Id}"));

    [Test]
    public async Task Merge_SettlesThePlaceholdersPendingClaims()
    {
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "Mine"), XboxUser.FromXbox(2, "Theirs"));
        await _context.SaveChangesAsync();
        _real.TieToXbox(1);
        _context.GamertagClaims.AddRange(
            GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 1, "Old Tag", "OLD TAG", _dummy.Id),
            GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 2, "Old Tag", "OLD TAG", _dummy.Id));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);
        _context.ChangeTracker.Clear();

        var claims = await _context.GamertagClaims.OrderBy(c => c.GamertagClaimID).Select(c => new { c.XboxUserID, c.Status, c.PlaceholderUserID }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(claims[0].Status, Is.EqualTo(GamertagClaimStatus.Approved), "The placeholder is the account's now");
            Assert.That(claims[1].Status, Is.EqualTo(GamertagClaimStatus.Rejected), "It can't be another account's any more");
        }
    }

    [Test]
    public async Task Merge_IntoAnotherPlaceholder_MovesItsPendingClaims()
    {
        var target = User.Seed(userName: "historical:Target", displayName: "Target", isDummyUser: true);
        _context.Users.Add(target);
        _context.XboxUsers.Add(XboxUser.FromXbox(3, "X"));
        await _context.SaveChangesAsync();
        _context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 3, "Old Tag", "OLD TAG", _dummy.Id));
        await _context.SaveChangesAsync();

        await _service.Merge(target.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        var claim = await _context.GamertagClaims.SingleAsync();
        Assert.That((claim.Status, claim.PlaceholderUserID), Is.EqualTo((GamertagClaimStatus.Pending, (int?)target.Id)));
    }

    [Test]
    public async Task Merge_ATitleBothHold_KeepsTheCaptaincy()
    {
        var source = Title(_dummy, _season, "Pro", isCaptain: true);
        _context.Set<PlayerChampionship>().AddRange(Title(_real, _season, "Pro"), source);
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        var title = await _context.Set<PlayerChampionship>().SingleAsync();
        Assert.That((title.UserID, title.IsCaptain), Is.EqualTo((_real.Id, true)));
    }
}
