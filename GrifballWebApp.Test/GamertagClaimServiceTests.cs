using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GamertagClaimServiceTests
{
    private GrifballContext _context;
    private GamertagClaimService _service;
    private User _owner;
    private User _placeholder;
    private Season _season;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new GamertagClaimService(_context, new UserMergeService(_context));
        _owner = new User { UserName = "owner", XboxUser = new XboxUser { XboxUserID = 10, Gamertag = "New Tag" } };
        _placeholder = new User { UserName = "historical:Old Tag", NormalizedUserName = "HISTORICAL:OLD TAG", DisplayName = "Old Tag", IsDummyUser = true };
        _season = new Season
        {
            SeasonName = "Winter League 2019",
            IsHistorical = true,
            HistoricalSourceID = "wl19/Stats",
            SeasonStart = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
        _context.AddRange(_owner, _placeholder, _season);
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 20, Gamertag = "Nobody's" });
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _placeholder.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "Old Tag", Kills = 7 });
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<GamertagClaim> Claim(long xuid = 10, GamertagClaimKind kind = GamertagClaimKind.RosterMatch, bool withSeason = true, string gamertag = "Old Tag")
    {
        var claim = new GamertagClaim
        {
            Kind = kind,
            XboxUserID = xuid,
            Gamertag = gamertag,
            NormalizedGamertag = HistoricalKeys.Normalize(gamertag),
            PlaceholderUserID = _placeholder.Id,
            SeasonID = withSeason ? _season.SeasonID : null,
        };
        _context.GamertagClaims.Add(claim);
        await _context.SaveChangesAsync();
        return claim;
    }

    [Test]
    public async Task Approve_RecordsTheGamertag_AndMergesThePlaceholderIntoTheAccountsUser()
    {
        var claim = await Claim();
        var other = await Claim(xuid: 20);

        await _service.Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        var history = await _context.XboxGamertagHistory.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((history.XboxUserID, history.Gamertag, history.Source, history.FirstSeen, history.LastSeen),
                Is.EqualTo((10L, "Old Tag", GamertagHistorySource.RosterMatch, (DateTime?)_season.SeasonStart, (DateTime?)_season.SeasonEnd)));
            Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_owner.Id));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == _placeholder.Id), Is.False);
            var claims = await _context.GamertagClaims.OrderBy(c => c.GamertagClaimID).ToListAsync();
            Assert.That(claims.Select(c => c.Status), Is.EqualTo(new[] { GamertagClaimStatus.Approved, GamertagClaimStatus.Rejected }), "The other claim on the placeholder");
            Assert.That(claims.Select(c => c.PlaceholderUserID), Is.All.Null, "The placeholder is gone; the claims stay as a record");
        });
    }

    [Test]
    public async Task Approve_AnAccountNobodyHolds_MakesThePlaceholderItsUser()
    {
        var claim = await Claim(xuid: 20, kind: GamertagClaimKind.SetGamertag, withSeason: false);

        await _service.Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        Assert.That((await _context.Users.SingleAsync(u => u.Id == _placeholder.Id)).XboxUserID, Is.EqualTo(20));
        var history = await _context.XboxGamertagHistory.SingleAsync();
        Assert.That((history.Source, history.FirstSeen, history.LastSeen), Is.EqualTo((GamertagHistorySource.Manual, (DateTime?)null, (DateTime?)null)));
    }

    [Test]
    public async Task Approve_AMergeConflict_ChangesNothing()
    {
        // Both have stats for the season: no merge rule settles that
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _owner.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "New Tag" });
        await _context.SaveChangesAsync();
        var claim = await Claim();

        await Assert.ThrowsAsync<MergeConflictException>(() => _service.Approve(claim.GamertagClaimID));

        await using var fresh = _context.NewContextLike();
        Assert.That((await fresh.GamertagClaims.SingleAsync()).Status, Is.EqualTo(GamertagClaimStatus.Pending));
        Assert.That(await fresh.XboxGamertagHistory.AnyAsync(), Is.False);
        Assert.That(await fresh.Users.AnyAsync(u => u.Id == _placeholder.Id));
    }

    [Test]
    public async Task Reject_LeavesEverything_AndADecidedClaimCantBeDecidedAgain()
    {
        var claim = await Claim();

        await _service.Reject(claim.GamertagClaimID);

        Assert.That((await _context.GamertagClaims.SingleAsync()).Status, Is.EqualTo(GamertagClaimStatus.Rejected));
        Assert.That(await _context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(_placeholder.Id));
        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Approve(claim.GamertagClaimID));
        Assert.That(ex!.Message, Does.Contain("already rejected"));
        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Reject(claim.GamertagClaimID));
    }

    [Test]
    public async Task UnknownClaims_AreRefused()
    {
        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Approve(999));
        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Reject(999));
    }

    [Test]
    public async Task List_ShowsTheClaimsWithTheAccountAndSeason_PendingOrAll()
    {
        var pending = await Claim();
        var decided = await Claim(xuid: 20);
        await _service.Reject(decided.GamertagClaimID);

        var list = await _service.List(GamertagClaimStatus.Pending);
        var all = await _service.List(null);

        Assert.Multiple(() =>
        {
            var dto = list.Single();
            Assert.That((dto.GamertagClaimID, dto.Kind, dto.Status, dto.XboxGamertag, dto.UserID, dto.Gamertag, dto.PlaceholderUserID, dto.SeasonName),
                Is.EqualTo((pending.GamertagClaimID, "RosterMatch", "Pending", "New Tag", (int?)_owner.Id, "Old Tag", (int?)_placeholder.Id, "Winter League 2019")));
            Assert.That(all, Has.Count.EqualTo(2));
            Assert.That(all.Single(c => c.XboxUserID == 20).UserID, Is.Null);
        });
    }

    [Test]
    public async Task AddHistory_RecordsAndWidens_AManualGamertag()
    {
        await _service.AddHistory(new AddGamertagHistoryRequest { XboxUserID = 10, Gamertag = " Old  Tag ", FirstSeen = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc) });
        await _service.AddHistory(new AddGamertagHistoryRequest { XboxUserID = 10, Gamertag = "old tag", FirstSeen = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc), LastSeen = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        var history = await _context.XboxGamertagHistory.AsNoTracking().SingleAsync();
        Assert.That((history.Gamertag, history.NormalizedGamertag, history.Source, history.FirstSeen!.Value.Year, history.LastSeen!.Value.Year),
            Is.EqualTo(("Old Tag", "OLD TAG", GamertagHistorySource.Manual, 2018, 2020)));
    }

    [TestCase(999L, "Tag", null, null, "no Xbox account")]
    [TestCase(10L, " ", null, null, "Give the gamertag")]
    [TestCase(10L, "Tag", 2020, 2019, "after LastSeen")]
    public async Task AddHistory_RefusesWhatItCantRecord(long xuid, string gamertag, int? first, int? last, string message)
    {
        var request = new AddGamertagHistoryRequest
        {
            XboxUserID = xuid,
            Gamertag = gamertag,
            FirstSeen = first is null ? null : new DateTime(first.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSeen = last is null ? null : new DateTime(last.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.AddHistory(request));

        Assert.That(ex!.Message, Does.Contain(message));
    }
}
