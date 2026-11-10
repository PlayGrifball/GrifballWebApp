using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.UserManagement;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Every merge writes an audit record (UserMerges) with a manifest of what it changed, in its own transaction, and a
/// sysadmin can undo it: the rows come back exactly as they were, unless something changed them since.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeAuditTests
{
    private static readonly string[] TriggersNewestFirst = ["DuplicateSuggestion", "SetGamertag"];

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
        _real = User.Seed(userName: "real", displayName: "Real", mmr: 1200, wins: 3, losses: 1, xboxUser: XboxUser.FromXbox(10, "Real Tag"));
        _placeholder = User.Seed(userName: "historical:Old Tag", normalizedUserName: "HISTORICAL:OLD TAG", displayName: "Old Tag", isDummyUser: true);
        _season = Season.Seed(seasonName: "Winter League 2019", historicalSourceID: "wl19/Stats").WithDates(seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        _context.AddRange(_real, _placeholder, _season);
        await _context.SaveChangesAsync();
        _team = Team.Seed(seasonID: _season.SeasonID, teamName: "Hammers");
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
        _team.SeedPlayer(_real.Id);
        _team.SeedPlayer(_placeholder.Id, draftCaptainOrder: 2);
        await _context.SaveChangesAsync();
        _team.Captained(_placeholder.Id);
        _context.Set<PlayerChampionship>().AddRange(
            PlayerChampionship.Seed(userID: _real.Id, seasonID: _season.SeasonID, teamID: _team.TeamID, tier: "Pro", gamertagUsed: "Real Tag"),
            PlayerChampionship.Seed(userID: _placeholder.Id, seasonID: _season.SeasonID, teamID: _team.TeamID, tier: "Pro", gamertagUsed: "Old Tag", isCaptain: true));
        _context.Set<UserExperience>().AddRange(
            new UserExperience(1) { UserID = _real.Id },
            new UserExperience(1) { UserID = _placeholder.Id },
            new UserExperience(2) { UserID = _placeholder.Id });
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
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
        rows.AddRange((await context.Set<TeamPlayer>().AsNoTracking().OrderBy(t => t.TeamPlayerID).ToListAsync())
            .Select(t => $"TeamPlayer {t.TeamPlayerID} {t.TeamID} {t.UserID} {t.DraftCaptainOrder} {t.DraftRound}"));
        rows.AddRange((await context.Teams.AsNoTracking().OrderBy(t => t.TeamID).ToListAsync()).Select(t => $"Team {t.TeamID} {t.CaptainID}"));
        rows.AddRange((await context.Set<PlayerChampionship>().AsNoTracking().OrderBy(c => c.PlayerChampionshipID).ToListAsync())
            .Select(c => $"Title {c.PlayerChampionshipID} {c.UserID} {c.Tier} {c.IsCaptain} {c.GamertagUsed}"));
        rows.AddRange((await context.Set<UserExperience>().AsNoTracking().OrderBy(e => e.UserID).ThenBy(e => e.GameVersionID).ToListAsync())
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
        GamertagClaim Claim(User claimant) => GamertagClaim.ClaimedByUser(claimant.Id, "Shared Tag", "SHARED TAG");
        var targets = Claim(_real);
        var sources = Claim(_placeholder);
        _context.GamertagClaims.AddRange(targets, sources);
        await _context.SaveChangesAsync();

        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.Manual, "test", 99));

        _context.ChangeTracker.Clear();
        var claims = await _context.GamertagClaims.AsNoTracking().ToDictionaryAsync(c => c.GamertagClaimID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((claims[targets.GamertagClaimID].Status, claims[targets.GamertagClaimID].ClaimantUserID), Is.EqualTo((GamertagClaimStatus.Pending, (int?)_real.Id)));
            Assert.That((claims[sources.GamertagClaimID].Status, claims[sources.GamertagClaimID].ClaimantUserID, claims[sources.GamertagClaimID].RejectionReason),
                Is.EqualTo((GamertagClaimStatus.Rejected, (int?)_real.Id, "The same claim was made by the account it was merged into")));
        }
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
        using (Assert.EnterMultipleScope())
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
        }
    }

    [TestCase(GamertagClaimKind.RosterMatch, MergeTrigger.ClaimApproval)]
    [TestCase(GamertagClaimKind.SetGamertag, MergeTrigger.ClaimApproval)]
    [TestCase(GamertagClaimKind.XuidLookup, MergeTrigger.XuidLookup)]
    public async Task ApprovingAClaim_RecordsItsMerge(GamertagClaimKind kind, MergeTrigger trigger)
    {
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        var claim = kind == GamertagClaimKind.XuidLookup
            ? GamertagClaim.FromXuidLookup(10, "Old Tag", "OLD TAG", _placeholder.Id, "Now Tag", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            : GamertagClaim.Suggest(kind, 10, "Old Tag", "OLD TAG", _placeholder.Id);
        _context.GamertagClaims.Add(claim);
        await _context.SaveChangesAsync();

        await new GrifballWebApp.Application.Historical.GamertagClaimService(_context, _merges).Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((record.Trigger, record.Reason, record.SourceUserID, record.TargetUserID), Is.EqualTo((trigger, $"claim {claim.GamertagClaimID}", _placeholder.Id, _real.Id)));
            Assert.That(manifest.Entries.Select(e => (e.Op, e.Table)), Does.Contain((MergeManifestOp.Deleted, "Auth.Users")).And.Contain((MergeManifestOp.Modified, "Event.PlayerSeasonStats")));
        }
    }

    [Test]
    public async Task SettingAGamertag_RecordsTheMergesOfItsPastGamertagsPlaceholders()
    {
        var person = User.Seed(userName: "person");
        _context.Users.Add(person);
        _context.XboxUsers.Add(XboxUser.FromXbox(77, "New Tag"));
        await _context.SaveChangesAsync();
        (await _context.XboxUsers.WithPastGamertags().SingleAsync(x => x.XboxUserID == 77)).RecordPastGamertag("Old Tag", null, null, GamertagHistorySource.Manual);
        await _context.SaveChangesAsync();
        var getsert = NSubstitute.Substitute.For<GrifballWebApp.Application.HaloInfinite.IGetsertXboxUserService>();
        getsert.GetsertXboxUserByGamertag("New Tag", NSubstitute.Arg.Any<CancellationToken>())
            .Returns(async _ => ((XboxUser?)await _context.XboxUsers.Include(x => x.User).FirstAsync(x => x.XboxUserID == 77), (string?)null));

        var error = await new SetGamertagService(_context, getsert, _merges, new GrifballWebApp.Application.Historical.GamertagMatching(_context)).SetGamertag(person.Id, "New Tag");

        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That((record.Trigger, record.Reason, record.SourceUserID, record.TargetUserID, record.TriggeredByUserID),
                Is.EqualTo((MergeTrigger.SetGamertag, "past gamertag Old Tag", _placeholder.Id, person.Id, (int?)person.Id)));
        }
    }

    [Test]
    public async Task Merge_ThatIsRefused_WritesNoRecord()
    {
        var other = Team.Seed(seasonID: _season.SeasonID, teamName: "Swords");
        _context.Teams.Add(other);
        await _context.SaveChangesAsync();
        _context.Set<TeamPlayer>().AddRange(TeamPlayer.Seed(teamID: _team.TeamID, userID: _real.Id), TeamPlayer.Seed(teamID: other.TeamID, userID: _placeholder.Id));
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
    public async Task Undo_OfAMergeOfTwoPlayersOfARecordedGame_BringsTheDroppedLineBack()
    {
        var game = RecordedGame.Seed("mcc/1", seasonID: _season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(_placeholder.Id, null, "Old Tag", 3, 0, 0));
        game.SeedPlayer(new RecordedPlayerLine(_real.Id, null, "Real Tag", 5, 0, 0));
        _context.RecordedGames.Add(game);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var before = await _context.Set<RecordedGameParticipant>().OrderBy(p => p.RecordedGameParticipantID)
            .Select(p => new { p.RecordedGameParticipantID, p.UserID, p.Kills }).ToListAsync();

        await _merges.Merge(_real.Id, _placeholder.Id);
        _context.ChangeTracker.Clear();
        var record = await _context.UserMerges.SingleAsync();
        var dropped = MergeManifest.Parse(record.Manifest).Entries
            .Where(e => e.Op == MergeManifestOp.Deleted && e.Table.EndsWith("GameParticipants", StringComparison.Ordinal)).ToList();
        var merged = await _context.Set<RecordedGameParticipant>().Select(p => p.UserID).ToListAsync();

        await _audit.Undo(record.UserMergeID, "wrong person");

        _context.ChangeTracker.Clear();
        var after = await _context.Set<RecordedGameParticipant>().OrderBy(p => p.RecordedGameParticipantID)
            .Select(p => new { p.RecordedGameParticipantID, p.UserID, p.Kills }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged, Is.EqualTo(new[] { _real.Id }), "Merged: one line, the target's");
            Assert.That(dropped, Has.Count.EqualTo(1), "The source's line is in the manifest, deleted");
            Assert.That(after, Is.EqualTo(before), "Undone: both lines back, with their keys, users and stats");
        }
    }

    [Test]
    public async Task Undo_OfAClaimApproval_BringsTheClaimsBack()
    {
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        _context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", _placeholder.Id));
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
        var someone = User.Seed(userName: "someone");
        _context.Users.Add(someone);
        await _context.SaveChangesAsync();
        var stat = await _context.PlayerSeasonStats.SingleAsync();
        stat.ReassignUser(stat.UserID, someone.Id);
        await _context.SaveChangesAsync();
        var after = await Snapshot();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("changed since"));
            Assert.That(await Snapshot(), Is.EqualTo(after), "Nothing changed");
        }
        await using var fresh = _context.NewContextLike();
        Assert.That((await fresh.UserMerges.SingleAsync()).UndoneAt, Is.Null);
    }

    [Test]
    public async Task Undo_IsRefused_WhileALaterMergeOfTheSameRowsStands()
    {
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        var second = User.Seed(userName: "second");
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
        _context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        var second = User.Seed(userName: "second");
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
    public async Task Undo_IsRefused_WhenAnotherUndoLandsBetweenTheReadAndTheTransaction()
    {
        // Two sysadmins at once: both read the merge standing, so the first check passes on both. The rival undoes the record
        // just before this undo's transaction begins (not inside it: Microsoft.Data.Sqlite's BEGIN takes the writer lock),
        // so only the record's own check inside the transaction can refuse, and the rows it put back roll back.
        await GiveThePlaceholderEverything();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await _context.UserMerges.SingleAsync()).UserMergeID;
        var merged = await Snapshot();
        var rivalAt = _time.GetUtcNow().UtcDateTime.AddMinutes(-1);
        var rival = new RivalUndo(_context, mergeId, rivalAt);
        await using var racing = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).AddInterceptors(rival).Options);

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new UserMergeAuditService(racing, _time).Undo(mergeId, "mine"));

        await using var fresh = _context.NewContextLike();
        var record = await fresh.UserMerges.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rival.Undid, Is.True, "The rival ran in the gap");
            Assert.That(ex!.Message, Does.Contain("undone already"));
            Assert.That(await Snapshot(), Is.EqualTo(merged), "The rows put back were rolled back");
            Assert.That((record.UndoneByUserID, record.UndoNote), Is.EqualTo(((int?)42, "theirs")), "The first undo stands");
            Assert.That(record.UndoneAt, Is.EqualTo(rivalAt).Within(TimeSpan.FromSeconds(1)));
        }
    }

    /// <summary>Undoes the record as user 42, through a second context, once: just before the undo's transaction begins.</summary>
    private sealed class RivalUndo(GrifballContext like, int mergeId, DateTime at) : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public bool Undid { get; private set; }

        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbTransaction>> TransactionStartingAsync(
            System.Data.Common.DbConnection connection, Microsoft.EntityFrameworkCore.Diagnostics.TransactionStartingEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbTransaction> result, CancellationToken cancellationToken = default)
        {
            if (!Undid)
            {
                Undid = true;
                await using var ctx = like.NewContextLike();
                var record = await ctx.UserMerges.SingleAsync(m => m.UserMergeID == mergeId, cancellationToken);
                record.Undo(at, 42, "theirs");
                await ctx.SaveChangesAsync(cancellationToken);
            }
            return result;
        }
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
        // Past the record's methods, as only something outside the class could (raw SQL, a bad migration)
        _context.Entry(record).Property(r => r.Manifest).CurrentValue = manifest.Serialize();
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(record.UserMergeID, null));

        Assert.That(ex!.Message, Does.Contain("taken"));
    }

    /// <summary>
    /// A merge made before the UserAggregate migration snapshotted its removed source as it was then: e.g. the source kept
    /// its win streak when its wins moved, which CK_Auth_Users_LateLeagueRecord now refuses. The undo settles the row as
    /// the migration settles live ones, and puts it back.
    /// </summary>
    [TestCase(0, 0, 3, 0, null, 2, 0, 0, null, TestName = "Undo_OfAnOldMerge_SettlesTheSource_AStreakWithNoWins")]
    [TestCase(5, 4, 2, 1, 1, null, 0, 0, 1, TestName = "Undo_OfAnOldMerge_SettlesTheSource_TwoStreaks_ARegionWithNoChooser")]
    [TestCase(5, 2, 0, 4, 1, 2, 0, 2, 2, TestName = "Undo_OfAnOldMerge_SettlesTheSource_ALossStreakLongerThanItsLosses")]
    [TestCase(5, 2, 3, 0, null, null, 3, 0, null, TestName = "Undo_OfAnOldMerge_KeepsAValidSourceAsItWas")]
    public async Task Undo_OfAMergeMadeBeforeTheUserConstraints_SettlesTheRestoredSource(
        int wins, int losses, int winStreak, int lossStreak, int? regionId, int? regionSource,
        int settledWinStreak, int settledLossStreak, int? settledRegionSource)
    {
        await _merges.Merge(_real.Id, _placeholder.Id);
        var record = await _context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        var userRow = manifest.Entries.Single(e => e.Op == MergeManifestOp.Deleted && e.Table == "Auth.Users");
        var values = userRow.Values!;
        values[nameof(User.Wins)] = System.Text.Json.JsonSerializer.SerializeToElement(wins);
        values[nameof(User.Losses)] = System.Text.Json.JsonSerializer.SerializeToElement(losses);
        values[nameof(User.WinStreak)] = System.Text.Json.JsonSerializer.SerializeToElement(winStreak);
        values[nameof(User.LossStreak)] = System.Text.Json.JsonSerializer.SerializeToElement(lossStreak);
        values[nameof(User.RegionID)] = System.Text.Json.JsonSerializer.SerializeToElement(regionId);
        values[nameof(User.RegionSource)] = System.Text.Json.JsonSerializer.SerializeToElement(regionSource);
        // Past the record's methods: the manifest as an older merge wrote it
        _context.Entry(record).Property(r => r.Manifest).CurrentValue = manifest.Serialize();
        await _context.SaveChangesAsync();

        await _audit.Undo(record.UserMergeID, null);

        await using var context = _context.NewContextLike();
        var source = await context.Users.AsNoTracking().SingleAsync(u => u.Id == _placeholder.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((source.Wins, source.Losses), Is.EqualTo((wins, losses)));
            Assert.That((source.WinStreak, source.LossStreak), Is.EqualTo((settledWinStreak, settledLossStreak)));
            Assert.That(source.RegionID, Is.EqualTo(regionId));
            Assert.That((int?)source.RegionSource, Is.EqualTo(settledRegionSource));
        }
    }

    [Test]
    public async Task Undo_IsRefused_WhenARowItAddedWasChangedOrIsGone()
    {
        _context.Set<UserExperience>().Add(new UserExperience(2) { UserID = _placeholder.Id });
        await _context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await _context.UserMerges.SingleAsync()).UserMergeID;
        _context.Set<UserExperience>().Remove(await _context.Set<UserExperience>().SingleAsync());
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        Assert.That(ex!.Message, Does.Contain("gone"));
    }

    [Test]
    public async Task List_IsNewestFirst_AndGetSummarisesTheTables()
    {
        await GiveThePlaceholderEverything();
        var other = User.Seed(userName: "historical:Other", normalizedUserName: "HISTORICAL:OTHER", isDummyUser: true);
        _context.Users.Add(other);
        await _context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.SetGamertag, "set gamertag Real Tag", _real.Id));
        await new UserMergeService(_context, _time).Merge(_real.Id, other.Id, MergeOptions.For(MergeTrigger.DuplicateSuggestion));

        var page = await _audit.List(new PaginationFilter(1, 10));
        var detail = await _audit.Get(page.Results.Last().UserMergeID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(page.TotalCount, Is.EqualTo(2));
            Assert.That(page.Results.Select(r => r.Trigger), Is.EqualTo(TriggersNewestFirst));
            Assert.That(page.Results.Last().TriggeredByUserName, Is.EqualTo("real"));
            Assert.That(detail!.Tables.Select(t => t.Table), Does.Contain("Auth.Users"));
            Assert.That(detail.Tables.Sum(t => t.Removed), Is.EqualTo(detail.RowsRemoved));
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list.TotalCount, Is.EqualTo(1));
            Assert.That(found.Value!.UserMergeID, Is.EqualTo(mergeId));
            Assert.That(missing.Result, Is.TypeOf<NotFoundResult>());
            Assert.That(undone, Is.TypeOf<NoContentResult>());
            Assert.That(await _context.NewContextLike().Users.AnyAsync(u => u.Id == _placeholder.Id), Is.True);
        }
    }

    [Test]
    public void MergeUndoRefused_IsAConflictForTheUser()
    {
        // GlobalExceptionHandler is internal: its Expected through reflection
        var type = typeof(GrifballWebApp.Server.Program).Assembly.GetType("GrifballWebApp.Server.GlobalExceptionHandler", throwOnError: true)!;
        var expected = type.GetMethod("Expected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        var problem = (ProblemDetails)expected.Invoke(null, [new MergeUndoRefusedException("Can't undo: x")])!;
        Assert.That((problem.Status, problem.Detail), Is.EqualTo(((int?)409, "Can't undo: x")));
    }

    [Test]
    public async Task Merge_OfBothPlayersOfAnEndedMatch_DropsTheSources_AndUndoBringsItBackWithItsVotes()
    {
        var match = MatchedMatch.Start([_real.Id], [_placeholder.Id]);
        _context.MatchedMatches.Add(match);
        await _context.SaveChangesAsync();
        match.VoteForWinner(_placeholder.Id, WinnerVote.Away);
        match.VoteToKick(_placeholder.Id, _real.Id);
        match.VoteToKick(_real.Id, _placeholder.Id);
        match.DecideByVote(MatchedSide.Away);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        async Task<(int[] Players, int WinnerVotes, int KickVotes)> Rows()
        {
            await using var fresh = _context.NewContextLike();
            return (await fresh.Set<MatchedPlayer>().Select(p => p.UserID).OrderBy(u => u).ToArrayAsync(),
                await fresh.Set<MatchedWinnerVote>().CountAsync(), await fresh.Set<MatchedKickVote>().CountAsync());
        }
        var before = await Rows();

        await _merges.Merge(_real.Id, _placeholder.Id);
        var merged = await Rows();
        await _audit.Undo((await _context.UserMerges.SingleAsync()).UserMergeID, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(before, Is.EqualTo((new[] { _real.Id, _placeholder.Id }.Order().ToArray(), 1, 2)));
            Assert.That(merged, Is.EqualTo((new[] { _real.Id }, 0, 0)), "One person once: the source's player went, with its votes and those against it");
            Assert.That(await Rows(), Is.EqualTo(before));
        }
    }
}
