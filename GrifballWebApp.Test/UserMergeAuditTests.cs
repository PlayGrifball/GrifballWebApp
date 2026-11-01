using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Test;

/// <summary>
/// Every merge writes an audit record (UserMerges) with a manifest of what it changed, in its own transaction, and a
/// sysadmin can undo it: the rows come back exactly as they were, unless something changed them since.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeAuditTests
{
    private GrifballContext _context;
    private FakeTimeProvider _time;
    private UserMergeService _merges;
    private UserMergeAuditService _audit;
    private User _real;
    private User _placeholder;
    private Season _season;
    private Team _team;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _merges = new UserMergeService(_context, _time);
        _audit = new UserMergeAuditService(_context, _time);
        _real = new User { UserName = "real", DisplayName = "Real", MMR = 1200, Wins = 3, Losses = 1, XboxUser = new XboxUser { XboxUserID = 10, Gamertag = "Real Tag" } };
        _placeholder = new User { UserName = "historical:Old Tag", NormalizedUserName = "HISTORICAL:OLD TAG", DisplayName = "Old Tag", IsDummyUser = true };
        _season = new Season
        {
            SeasonName = "Winter League 2019",
            IsHistorical = true,
            HistoricalSourceID = "wl19/Stats",
            SeasonStart = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SeasonEnd = new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };
        _context.AddRange(_real, _placeholder, _season);
        await _context.SaveChangesAsync();
        _team = new Team { SeasonID = _season.SeasonID, TeamName = "Hammers" };
        _context.Teams.Add(_team);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    /// <summary>
    /// The placeholder has a bit of everything a merge touches: a second row on the real user's team (it is the
    /// captain's), titles of a tier both won (captaincy combines), a game version both played and one only it did, stats,
    /// a role, and an XUID lookup the database would cascade to.
    /// </summary>
    private async Task GiveThePlaceholderEverything()
    {
        var realRow = new TeamPlayer { TeamID = _team.TeamID, UserID = _real.Id };
        var placeholderRow = new TeamPlayer { TeamID = _team.TeamID, UserID = _placeholder.Id, DraftCaptainOrder = 2 };
        _context.TeamPlayers.AddRange(realRow, placeholderRow);
        await _context.SaveChangesAsync();
        _team.CaptainID = placeholderRow.TeamPlayerID;
        _context.PlayerChampionships.AddRange(
            new PlayerChampionship { UserID = _real.Id, SeasonID = _season.SeasonID, TeamID = _team.TeamID, Tier = "Pro", GamertagUsed = "Real Tag" },
            new PlayerChampionship { UserID = _placeholder.Id, SeasonID = _season.SeasonID, TeamID = _team.TeamID, Tier = "Pro", GamertagUsed = "Old Tag", IsCaptain = true });
        _context.UserExperiences.AddRange(
            new UserExperience { UserID = _real.Id, GameVersionID = 1 },
            new UserExperience { UserID = _placeholder.Id, GameVersionID = 1 },
            new UserExperience { UserID = _placeholder.Id, GameVersionID = 2 });
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _placeholder.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "Old Tag", Kills = 7 });
        _context.UserRoles.Add(new UserRole { UserId = _placeholder.Id, RoleId = await _context.Roles.Select(r => r.Id).FirstAsync() });
        _context.XuidLookups.Add(new XuidLookup { UserID = _placeholder.Id, LookedUpAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Status = XuidLookupStatus.NotFound });
        await _context.SaveChangesAsync();
    }

    /// <summary>The rows a merge touches, as text without audit columns, read with a fresh context.</summary>
    private async Task<List<string>> Snapshot()
    {
        await using var context = _context.NewContextLike();
        var rows = new List<string>();
        rows.AddRange((await context.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync())
            .Select(u => $"User {u.Id} {u.UserName} {u.NormalizedUserName} {u.DisplayName} {u.IsDummyUser} {u.XboxUserID} {u.DiscordUserID} {u.MMR} {u.Wins} {u.Losses} {u.WinStreak} {u.LossStreak} {u.SecurityStamp} {u.ConcurrencyStamp}"));
        rows.AddRange((await context.TeamPlayers.AsNoTracking().OrderBy(t => t.TeamPlayerID).ToListAsync())
            .Select(t => $"TeamPlayer {t.TeamPlayerID} {t.TeamID} {t.UserID} {t.DraftCaptainOrder} {t.DraftRound}"));
        rows.AddRange((await context.Teams.AsNoTracking().OrderBy(t => t.TeamID).ToListAsync()).Select(t => $"Team {t.TeamID} {t.CaptainID}"));
        rows.AddRange((await context.PlayerChampionships.AsNoTracking().OrderBy(c => c.PlayerChampionshipID).ToListAsync())
            .Select(c => $"Title {c.PlayerChampionshipID} {c.UserID} {c.Tier} {c.IsCaptain} {c.GamertagUsed}"));
        rows.AddRange((await context.UserExperiences.AsNoTracking().OrderBy(e => e.UserID).ThenBy(e => e.GameVersionID).ToListAsync())
            .Select(e => $"Experience {e.UserID} {e.GameVersionID}"));
        rows.AddRange((await context.PlayerSeasonStats.AsNoTracking().OrderBy(s => s.PlayerSeasonStatID).ToListAsync())
            .Select(s => $"Stat {s.PlayerSeasonStatID} {s.UserID} {s.Kills}"));
        rows.AddRange((await context.UserRoles.AsNoTracking().OrderBy(r => r.UserId).ThenBy(r => r.RoleId).ToListAsync()).Select(r => $"Role {r.UserId} {r.RoleId}"));
        rows.AddRange((await context.XuidLookups.AsNoTracking().OrderBy(l => l.UserID).ToListAsync()).Select(l => $"Lookup {l.UserID} {l.Status} {l.LookedUpAt:O}"));
        rows.AddRange((await context.GamertagClaims.AsNoTracking().OrderBy(c => c.GamertagClaimID).ToListAsync())
            .Select(c => $"Claim {c.GamertagClaimID} {c.Status} {c.PlaceholderUserID} {c.ClaimantUserID}"));
        return rows;
    }

    [Test]
    public async Task Merge_TheSameClaimMadeByBoth_StaysOnlyTheTargets()
    {
        GamertagClaim Claim(User claimant) => new()
        {
            Kind = GamertagClaimKind.UserClaim, ClaimantUserID = claimant.Id, Gamertag = "Shared Tag", NormalizedGamertag = "SHARED TAG", Status = GamertagClaimStatus.Pending,
        };
        var targets = Claim(_real);
        var sources = Claim(_placeholder);
        _context.GamertagClaims.AddRange(targets, sources);
        await _context.SaveChangesAsync();

        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.Manual, "test", 99));

        _context.ChangeTracker.Clear();
        var claims = await _context.GamertagClaims.AsNoTracking().ToDictionaryAsync(c => c.GamertagClaimID);
        Assert.Multiple(() =>
        {
            Assert.That((claims[targets.GamertagClaimID].Status, claims[targets.GamertagClaimID].ClaimantUserID), Is.EqualTo((GamertagClaimStatus.Pending, (int?)_real.Id)));
            Assert.That((claims[sources.GamertagClaimID].Status, claims[sources.GamertagClaimID].ClaimantUserID, claims[sources.GamertagClaimID].RejectionReason),
                Is.EqualTo((GamertagClaimStatus.Rejected, (int?)_real.Id, "The same claim was made by the account it was merged into")));
        });
    }

    [Test]
    public async Task Merge_WritesAnAuditRecord_WithEverythingItChanged()
    {
        await GiveThePlaceholderEverything();

        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.Manual, "test", 99));

        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        var removed = manifest.Entries.Where(e => e.Op == MergeManifestOp.Deleted).Select(e => e.Table).ToList();
        Assert.Multiple(() =>
        {
            Assert.That((record.Trigger, record.Reason, record.TriggeredByUserID, record.SourceUserID, record.SourceUserName, record.TargetUserID, record.TargetUserName),
                Is.EqualTo((MergeTrigger.Manual, "test", (int?)99, _placeholder.Id, "historical:Old Tag", _real.Id, "real")));
            Assert.That(record.MergedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
            Assert.That(removed, Does.Contain("Auth.Users"), "The source user row itself");
            Assert.That(removed, Does.Contain("Xbox.XuidLookups"), "What deleting the source cascades to");
            Assert.That(removed, Does.Contain("Event.TeamPlayers"), "The de-duplicated team row");
            Assert.That(removed.Count(t => t.EndsWith("UserExperiences")), Is.EqualTo(2));
            Assert.That(manifest.Entries.Count(e => e.Op == MergeManifestOp.Added), Is.EqualTo(1), "The version only the placeholder played");
            Assert.That(record.RowsRemoved, Is.EqualTo(removed.Count));
            Assert.That(record.RowsAdded, Is.EqualTo(1));
            Assert.That(record.RowsMoved, Is.GreaterThan(0));
            var captain = manifest.Entries.Single(e => e.Op == MergeManifestOp.Modified && e.Changes!.ContainsKey(nameof(Team.CaptainID)));
            Assert.That(captain.Changes![nameof(Team.CaptainID)].New.GetInt32(), Is.Not.EqualTo(captain.Changes[nameof(Team.CaptainID)].Old.GetInt32()));
            Assert.That(manifest.Entries.SelectMany(e => e.Values?.Keys ?? Enumerable.Empty<string>()), Has.None.EqualTo("PeriodStart"), "No temporal period columns");
        });
    }

    [TestCase(GamertagClaimKind.RosterMatch, MergeTrigger.ClaimApproval)]
    [TestCase(GamertagClaimKind.SetGamertag, MergeTrigger.ClaimApproval)]
    [TestCase(GamertagClaimKind.XuidLookup, MergeTrigger.XuidLookup)]
    public async Task ApprovingAClaim_RecordsItsMerge(GamertagClaimKind kind, MergeTrigger trigger)
    {
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _placeholder.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "Old Tag", Kills = 7 });
        var claim = new GamertagClaim { Kind = kind, XboxUserID = 10, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", PlaceholderUserID = _placeholder.Id };
        _context.GamertagClaims.Add(claim);
        await _context.SaveChangesAsync();

        await new GrifballWebApp.Server.Historical.GamertagClaimService(_context, _merges).Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        Assert.That((record.Trigger, record.Reason, record.SourceUserID, record.TargetUserID), Is.EqualTo((trigger, $"claim {claim.GamertagClaimID}", _placeholder.Id, _real.Id)));
        Assert.That(manifest.Entries.Select(e => (e.Op, e.Table)), Does.Contain((MergeManifestOp.Deleted, "Auth.Users")).And.Contain((MergeManifestOp.Modified, "Event.PlayerSeasonStats")));
    }

    [Test]
    public async Task SettingAGamertag_RecordsTheMergesOfItsPastGamertagsPlaceholders()
    {
        var person = new User { UserName = "person" };
        _context.Users.Add(person);
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 77, Gamertag = "New Tag" });
        await _context.SaveChangesAsync();
        await _context.UpsertGamertagHistoryAsync(77, "Old Tag", null, null, GamertagHistorySource.Manual, CancellationToken.None);
        await _context.SaveChangesAsync();
        var getsert = NSubstitute.Substitute.For<GrifballWebApp.Server.Services.IGetsertXboxUserService>();
        getsert.GetsertXboxUserByGamertag("New Tag", NSubstitute.Arg.Any<CancellationToken>())
            .Returns(async _ => ((XboxUser?)await _context.XboxUsers.Include(x => x.User).FirstAsync(x => x.XboxUserID == 77), (string?)null));

        var error = await new SetGamertagService(_context, getsert, _merges, new GrifballWebApp.Server.Historical.GamertagMatching(_context)).SetGamertag(person.Id, "New Tag");

        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        Assert.That(error, Is.Null);
        Assert.That((record.Trigger, record.Reason, record.SourceUserID, record.TargetUserID, record.TriggeredByUserID),
            Is.EqualTo((MergeTrigger.SetGamertag, "past gamertag Old Tag", _placeholder.Id, person.Id, (int?)person.Id)));
    }

    [Test]
    public async Task Merge_ThatIsRefused_WritesNoRecord()
    {
        var other = new Team { SeasonID = _season.SeasonID, TeamName = "Swords" };
        _context.Teams.Add(other);
        await _context.SaveChangesAsync();
        _context.TeamPlayers.AddRange(new TeamPlayer { TeamID = _team.TeamID, UserID = _real.Id }, new TeamPlayer { TeamID = other.TeamID, UserID = _placeholder.Id });
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<MergeConflictException>(() => _merges.Merge(_real.Id, _placeholder.Id));

        Assert.That(await _context.UserMerges.AnyAsync(), Is.False);
    }

    [Test]
    public async Task Undo_PutsEverythingBackExactly()
    {
        await GiveThePlaceholderEverything();
        var before = await Snapshot();
        await _merges.Merge(_real.Id, _placeholder.Id);
        Assert.That(await Snapshot(), Is.Not.EqualTo(before));

        await _audit.Undo((await _context.UserMerges.SingleAsync()).UserMergeID, "wrong person");

        Assert.That(await Snapshot(), Is.EqualTo(before));
        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        Assert.That((record.UndoneAt, record.UndoNote), Is.EqualTo(((DateTime?)_time.GetUtcNow().UtcDateTime, "wrong person")));
    }

    [Test]
    public async Task Undo_OfAClaimApproval_BringsTheClaimsBack()
    {
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _placeholder.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "Old Tag", Kills = 7 });
        _context.GamertagClaims.Add(new GamertagClaim { Kind = GamertagClaimKind.RosterMatch, XboxUserID = 10, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", PlaceholderUserID = _placeholder.Id });
        await _context.SaveChangesAsync();
        var before = await Snapshot();
        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.ClaimApproval, "claim 1"));

        await _audit.Undo((await _context.UserMerges.SingleAsync()).UserMergeID, null);

        Assert.That(await Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public async Task Undo_IsRefused_WhenAMovedRowWasChangedSince()
    {
        await GiveThePlaceholderEverything();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await _context.UserMerges.SingleAsync()).UserMergeID;
        // A commissioner moved the stats on to someone else
        var someone = new User { UserName = "someone" };
        _context.Users.Add(someone);
        await _context.SaveChangesAsync();
        (await _context.PlayerSeasonStats.SingleAsync()).UserID = someone.Id;
        await _context.SaveChangesAsync();
        var after = await Snapshot();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        Assert.That(ex!.Message, Does.Contain("changed since"));
        Assert.That(await Snapshot(), Is.EqualTo(after), "Nothing changed");
        await using var fresh = _context.NewContextLike();
        Assert.That((await fresh.UserMerges.SingleAsync()).UndoneAt, Is.Null);
    }

    [Test]
    public async Task Undo_IsRefused_WhileALaterMergeOfTheSameRowsStands()
    {
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _placeholder.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "Old Tag", Kills = 7 });
        var second = new User { UserName = "second" };
        _context.Users.Add(second);
        await _context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id);
        // The stats move again: the real user is merged into another account
        await new UserMergeService(_context, _time).Merge(second.Id, _real.Id);
        var merges = await _context.UserMerges.OrderBy(m => m.UserMergeID).ToListAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(merges[0].UserMergeID, null));

        Assert.That(ex!.Message, Does.Contain($"Merge {merges[1].UserMergeID}"));
    }

    [Test]
    public async Task Undo_InReverseOrder_Works()
    {
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = _placeholder.Id, SeasonID = _season.SeasonID, GameVersionID = 1, Gamertag = "Old Tag", Kills = 7 });
        var second = new User { UserName = "second" };
        _context.Users.Add(second);
        await _context.SaveChangesAsync();
        var before = await Snapshot();
        await _merges.Merge(_real.Id, _placeholder.Id);
        await new UserMergeService(_context, _time).Merge(second.Id, _real.Id);
        var merges = await _context.UserMerges.OrderBy(m => m.UserMergeID).Select(m => m.UserMergeID).ToListAsync();

        await new UserMergeAuditService(_context, _time).Undo(merges[1], null);
        await new UserMergeAuditService(_context.NewContextLike(), _time).Undo(merges[0], null);

        Assert.That(await Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public async Task Undo_Twice_IsRefused()
    {
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await _context.UserMerges.SingleAsync()).UserMergeID;
        await _audit.Undo(mergeId, null);

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new UserMergeAuditService(_context.NewContextLike(), _time).Undo(mergeId, null));

        Assert.That(ex!.Message, Does.Contain("undone already"));
    }

    [Test]
    public async Task Undo_OfNoSuchMerge_IsRefused()
    {
        await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(999, null));
    }

    [Test]
    public async Task Undo_IsRefused_WhenTheSourcesKeyIsTaken()
    {
        await _merges.Merge(_real.Id, _placeholder.Id);
        var record = await _context.UserMerges.SingleAsync();
        // A hand-made manifest whose removed row's key is in use: the real user's own
        var manifest = MergeManifest.Parse(record.Manifest);
        var userRow = manifest.Entries.Single(e => e.Op == MergeManifestOp.Deleted && e.Table == "Auth.Users");
        userRow.Key["Id"] = System.Text.Json.JsonSerializer.SerializeToElement(_real.Id);
        record.Manifest = manifest.Serialize();
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(record.UserMergeID, null));

        Assert.That(ex!.Message, Does.Contain("taken"));
    }

    [Test]
    public async Task Undo_IsRefused_WhenARowItAddedWasChangedOrIsGone()
    {
        _context.UserExperiences.Add(new UserExperience { UserID = _placeholder.Id, GameVersionID = 2 });
        await _context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await _context.UserMerges.SingleAsync()).UserMergeID;
        _context.UserExperiences.Remove(await _context.UserExperiences.SingleAsync());
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        Assert.That(ex!.Message, Does.Contain("gone"));
    }

    [Test]
    public async Task List_IsNewestFirst_AndGetSummarisesTheTables()
    {
        await GiveThePlaceholderEverything();
        var other = new User { UserName = "historical:Other", NormalizedUserName = "HISTORICAL:OTHER", IsDummyUser = true };
        _context.Users.Add(other);
        await _context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.SetGamertag, "set gamertag Real Tag", _real.Id));
        await new UserMergeService(_context, _time).Merge(_real.Id, other.Id, MergeOptions.For(MergeTrigger.DuplicateSuggestion));

        var page = await _audit.List(new PaginationFilter(1, 10));
        var detail = await _audit.Get(page.Results.Last().UserMergeID);

        Assert.Multiple(() =>
        {
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Results.Select(r => r.Trigger), Is.EqualTo(new[] { "DuplicateSuggestion", "SetGamertag" }));
            Assert.That(page.Results.Last().TriggeredByUserName, Is.EqualTo("real"));
            Assert.That(detail!.Tables.Select(t => t.Table), Does.Contain("Auth.Users"));
            Assert.That(detail.Tables.Sum(t => t.Removed), Is.EqualTo(detail.RowsRemoved));
        });
        Assert.That(await _audit.Get(999), Is.Null);
    }

    [Test]
    public async Task Controller_ListsGetsAndUndoes()
    {
        await _merges.Merge(_real.Id, _placeholder.Id);
        var controller = new UserMergesController(_audit);
        var mergeId = (await _context.UserMerges.SingleAsync()).UserMergeID;

        var list = await controller.List(new PaginationFilter(), CancellationToken.None);
        var found = await controller.Get(mergeId, CancellationToken.None);
        var missing = await controller.Get(999, CancellationToken.None);
        var undone = await controller.Undo(mergeId, new UndoMergeRequest { Note = "oops" }, CancellationToken.None);

        Assert.Multiple(async () =>
        {
            Assert.That(list.TotalCount, Is.EqualTo(1));
            Assert.That(found.Value!.UserMergeID, Is.EqualTo(mergeId));
            Assert.That(missing.Result, Is.TypeOf<NotFoundResult>());
            Assert.That(undone, Is.TypeOf<NoContentResult>());
            Assert.That(await _context.NewContextLike().Users.AnyAsync(u => u.Id == _placeholder.Id), Is.True);
        });
    }

    [Test]
    public void MergeUndoRefused_IsAConflictForTheUser()
    {
        // GlobalExceptionHandler is internal: its Expected through reflection
        var type = typeof(UserMergeService).Assembly.GetType("GrifballWebApp.Server.GlobalExceptionHandler", throwOnError: true)!;
        var expected = type.GetMethod("Expected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var problem = (ProblemDetails)expected.Invoke(null, [new MergeUndoRefusedException("Can't undo: x")])!;
        Assert.That((problem.Status, problem.Detail), Is.EqualTo(((int?)409, "Can't undo: x")));
    }
}
