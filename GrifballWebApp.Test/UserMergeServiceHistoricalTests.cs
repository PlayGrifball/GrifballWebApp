using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

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
        _real = new User { UserName = "real", DisplayName = "Real" };
        _dummy = new User { UserName = "historical:Old Tag", DisplayName = "Old Tag", IsDummyUser = true };
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
        _context.PlayerChampionships.Add(Title(_dummy, _season, "Pro"));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.Multiple(async () =>
        {
            Assert.That(await _context.Users.AnyAsync(u => u.Id == _dummy.Id), Is.False);
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_real.Id));
            Assert.That(await _context.PlayerChampionships.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_real.Id));
        });
    }

    [Test]
    public async Task Merge_WaypointStatsReplaceTheOthersManualStats_ForThatPhaseOnly()
    {
        _context.PlayerSeasonStats.AddRange(
            Stat(_real, _season, kills: 100, source: StatSource.Waypoint),
            Stat(_dummy, _season, kills: 10),
            Stat(_dummy, _season, kills: 5, playoffs: true),
            Stat(_dummy, _other, kills: 7));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        var stats = await _context.PlayerSeasonStats.OrderBy(s => s.SeasonID).ToListAsync();
        Assert.That(stats.Select(s => (s.SeasonID, s.UserID, s.Kills, s.Source)), Is.EquivalentTo(new[]
        {
            (_season.SeasonID, _real.Id, 100, StatSource.Waypoint),
            (_season.SeasonID, _real.Id, 5, StatSource.Manual), // the playoffs have no Waypoint row
            (_other.SeasonID, _real.Id, 7, StatSource.Manual),
        }));
    }

    [Test]
    public async Task Merge_TheSourcesWaypointStatsReplaceTheTargetsManualStats()
    {
        _context.PlayerSeasonStats.AddRange(
            Stat(_real, _season, kills: 10),
            Stat(_dummy, _season, kills: 100, source: StatSource.Waypoint));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        var stat = await _context.PlayerSeasonStats.SingleAsync();
        Assert.That((stat.UserID, stat.Kills, stat.Source), Is.EqualTo((_real.Id, 100, StatSource.Waypoint)));
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
        _context.PlayerChampionships.Add(Title(_dummy, _season, "Pro"));
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
        _context.PlayerChampionships.AddRange(Title(_real, _season, "Pro"), Title(_dummy, _season, "pro"), Title(_dummy, _other, "Pro"));
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        Assert.That(await _context.PlayerChampionships.CountAsync(c => c.UserID == _real.Id), Is.EqualTo(2));
    }

    private static Season HistoricalSeason(string name, string sourceId) => new()
    {
        SeasonName = name,
        HistoricalSourceID = sourceId,
        IsHistorical = true,
        GameVersionID = 1,
        SignupsOpen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SignupsClose = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DraftStart = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SeasonStart = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        SeasonEnd = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    private static PlayerSeasonStat Stat(User user, Season season, int kills, bool playoffs = false, int stint = 1, StatSource source = StatSource.Manual) => new()
    {
        UserID = user.Id,
        SeasonID = season.SeasonID,
        GameVersionID = 1,
        Gamertag = user.DisplayName!,
        Kills = kills,
        IsPlayoffs = playoffs,
        StintIndex = stint,
        Source = source,
    };

    private static PlayerChampionship Title(User user, Season season, string tier) => new()
    {
        UserID = user.Id,
        SeasonID = season.SeasonID,
        Tier = tier,
        Team = new Team { SeasonID = season.SeasonID, TeamName = $"{tier} champs {user.Id}" },
        GamertagUsed = user.DisplayName!,
    };

    [Test]
    public async Task Merge_SettlesThePlaceholdersPendingClaims()
    {
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 1, Gamertag = "Mine" }, new XboxUser { XboxUserID = 2, Gamertag = "Theirs" });
        await _context.SaveChangesAsync();
        _real.XboxUserID = 1;
        _context.GamertagClaims.AddRange(
            new GamertagClaim { Kind = GamertagClaimKind.RosterMatch, XboxUserID = 1, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", PlaceholderUserID = _dummy.Id },
            new GamertagClaim { Kind = GamertagClaimKind.RosterMatch, XboxUserID = 2, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", PlaceholderUserID = _dummy.Id });
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);
        _context.ChangeTracker.Clear();

        var claims = await _context.GamertagClaims.OrderBy(c => c.GamertagClaimID).Select(c => new { c.XboxUserID, c.Status, c.PlaceholderUserID }).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(claims[0].Status, Is.EqualTo(GamertagClaimStatus.Approved), "The placeholder is the account's now");
            Assert.That(claims[1].Status, Is.EqualTo(GamertagClaimStatus.Rejected), "It can't be another account's any more");
        });
    }

    [Test]
    public async Task Merge_IntoAnotherPlaceholder_MovesItsPendingClaims()
    {
        var target = new User { UserName = "historical:Target", DisplayName = "Target", IsDummyUser = true };
        _context.Users.Add(target);
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 3, Gamertag = "X" });
        await _context.SaveChangesAsync();
        _context.GamertagClaims.Add(new GamertagClaim { Kind = GamertagClaimKind.RosterMatch, XboxUserID = 3, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", PlaceholderUserID = _dummy.Id });
        await _context.SaveChangesAsync();

        await _service.Merge(target.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        var claim = await _context.GamertagClaims.SingleAsync();
        Assert.That((claim.Status, claim.PlaceholderUserID), Is.EqualTo((GamertagClaimStatus.Pending, (int?)target.Id)));
    }

    [Test]
    public async Task Merge_ATitleBothHold_KeepsTheCaptaincy()
    {
        var source = Title(_dummy, _season, "Pro");
        source.IsCaptain = true;
        _context.PlayerChampionships.AddRange(Title(_real, _season, "Pro"), source);
        await _context.SaveChangesAsync();

        await _service.Merge(_real.Id, _dummy.Id);

        _context.ChangeTracker.Clear();
        var title = await _context.PlayerChampionships.SingleAsync();
        Assert.That((title.UserID, title.IsCaptain), Is.EqualTo((_real.Id, true)));
    }
}
