using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// Every merge writes an audit record (UserMerges) with a manifest of what it changed, and a sysadmin can undo it: the
/// rows come back exactly as they were, unless something changed them since. Two sysadmins racing the undo are two
/// fake contexts on one connection, the other's undo saved as this one opens its transaction (BeforeTransaction).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class UserMergeAuditServiceUnitTests : ApplicationDbContextTests
{
    private static readonly string[] TriggersNewestFirst = ["DuplicateSuggestion", "SetGamertag"];

    private FakeTimeProvider _time = null!;
    private UserMergeService _merges = null!;
    private UserMergeAuditService _audit = null!;
    private User _real = null!;
    private User _placeholder = null!;
    private Season _season = null!;
    private Team _team = null!;

    [SetUp]
    public async Task SetUp()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));
        _merges = new UserMergeService(Context, _time);
        _audit = new UserMergeAuditService(Context, _time);
        _real = User.Seed(userName: "real", displayName: "Real", mmr: 1200, wins: 3, losses: 1, xboxUser: XboxUser.FromXbox(10, "Real Tag"));
        _placeholder = User.Seed(userName: "historical:Old Tag", normalizedUserName: "HISTORICAL:OLD TAG", displayName: "Old Tag", isDummyUser: true);
        _season = Season.Seed(seasonName: "Winter League 2019", historicalSourceID: "wl19/Stats")
            .WithDates(seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        Context.AddRange(_real, _placeholder, _season);
        await Context.SaveChangesAsync();
        _team = Team.Seed(seasonID: _season.SeasonID, teamName: "Hammers");
        Context.Teams.Add(_team);
        await Context.SaveChangesAsync();
    }

    /// <summary>
    /// The placeholder has a bit of everything a merge touches: a second row on the real user's team (it is the
    /// captain's), titles of a tier both won (captaincy combines), a game version both played and one only it did,
    /// stats, a role, and an XUID lookup the database would cascade to.
    /// </summary>
    private async Task GiveThePlaceholderEverything()
    {
        await Context.Teams.WithRoster().Where(t => t.TeamID == _team.TeamID).LoadAsync();
        _team.AddPlayer(_real.Id);
        _team.AddPlayer(_placeholder.Id);
        await Context.SaveChangesAsync();
        _team.SetCaptain(_placeholder.Id);
        Context.Set<PlayerChampionship>().AddRange(
            PlayerChampionship.Seed(userID: _real.Id, seasonID: _season.SeasonID, teamID: _team.TeamID, tier: "Pro", gamertagUsed: "Real Tag"),
            PlayerChampionship.Seed(userID: _placeholder.Id, seasonID: _season.SeasonID, teamID: _team.TeamID, tier: "Pro", gamertagUsed: "Old Tag", isCaptain: true));
        Context.Set<UserExperience>().AddRange(
            new UserExperience(1) { UserID = _real.Id },
            new UserExperience(1) { UserID = _placeholder.Id },
            new UserExperience(2) { UserID = _placeholder.Id });
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        Context.UserRoles.Add(new UserRole { UserId = _placeholder.Id, RoleId = await Context.Roles.Select(r => r.Id).FirstAsync() });
        Context.XuidLookups.Add(new XuidLookup { UserID = _placeholder.Id, LookedUpAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), Status = XuidLookupStatus.NotFound });
        await Context.SaveChangesAsync();
    }

    /// <summary>The rows a merge touches, as text without audit columns.</summary>
    private async Task<List<string>> Snapshot()
    {
        Context.ChangeTracker.Clear();
        var rows = new List<string>();
        rows.AddRange((await Context.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync())
            .Select(u => $"User {u.Id} {u.UserName} {u.NormalizedUserName} {u.DisplayName} {u.IsDummyUser} {u.XboxUserID} {u.DiscordUserID} {u.MMR} {u.Wins} {u.Losses} {u.WinStreak} {u.LossStreak} {u.SecurityStamp} {u.ConcurrencyStamp}"));
        rows.AddRange((await Context.Set<TeamPlayer>().AsNoTracking().OrderBy(t => t.TeamPlayerID).ToListAsync())
            .Select(t => $"TeamPlayer {t.TeamPlayerID} {t.TeamID} {t.UserID} {t.DraftCaptainOrder} {t.DraftRound}"));
        rows.AddRange((await Context.Teams.AsNoTracking().OrderBy(t => t.TeamID).ToListAsync()).Select(t => $"Team {t.TeamID} {t.CaptainID}"));
        rows.AddRange((await Context.Set<PlayerChampionship>().AsNoTracking().OrderBy(c => c.PlayerChampionshipID).ToListAsync())
            .Select(c => $"Title {c.PlayerChampionshipID} {c.UserID} {c.Tier} {c.IsCaptain} {c.GamertagUsed}"));
        rows.AddRange((await Context.Set<UserExperience>().AsNoTracking().OrderBy(e => e.UserID).ThenBy(e => e.GameVersionID).ToListAsync())
            .Select(e => $"Experience {e.UserID} {e.GameVersionID}"));
        rows.AddRange((await Context.PlayerSeasonStats.AsNoTracking().OrderBy(s => s.PlayerSeasonStatID).ToListAsync())
            .Select(s => $"Stat {s.PlayerSeasonStatID} {s.UserID} {s.Kills}"));
        rows.AddRange((await Context.UserRoles.AsNoTracking().OrderBy(r => r.UserId).ThenBy(r => r.RoleId).ToListAsync()).Select(r => $"Role {r.UserId} {r.RoleId}"));
        rows.AddRange((await Context.XuidLookups.AsNoTracking().OrderBy(l => l.UserID).ToListAsync()).Select(l => $"Lookup {l.UserID} {l.Status} {l.LookedUpAt:O}"));
        rows.AddRange((await Context.GamertagClaims.AsNoTracking().OrderBy(c => c.GamertagClaimID).ToListAsync())
            .Select(c => $"Claim {c.GamertagClaimID} {c.Status} {c.PlaceholderUserID} {c.ClaimantUserID}"));
        return rows;
    }

    [Test]
    public async Task Merge_WritesAnAuditRecord_WithEverythingItChanged()
    {
        await GiveThePlaceholderEverything();

        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.Manual, "test", 99));

        Context.ChangeTracker.Clear();
        var record = await Context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        var removed = manifest.Entries.Where(e => e.Op == MergeManifestOp.Deleted).Select(e => e.Table).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((record.Trigger, record.Reason, record.TriggeredByUserID, record.SourceUserID, record.SourceUserName, record.TargetUserID, record.TargetUserName),
                Is.EqualTo((MergeTrigger.Manual, "test", (int?)99, _placeholder.Id, "historical:Old Tag", _real.Id, "real")));
            Assert.That(record.MergedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
            Assert.That(removed, Does.Contain("Auth.Users"), "The source user row itself");
            Assert.That(removed, Does.Contain("Xbox.XuidLookups"), "What deleting the source cascades to");
            Assert.That(removed.Count(t => t.EndsWith("UserExperiences")), Is.EqualTo(2));
            Assert.That(manifest.Entries.Count(e => e.Op == MergeManifestOp.Added), Is.EqualTo(1), "The version only the placeholder played");
            Assert.That(record.RowsRemoved, Is.EqualTo(removed.Count));
            Assert.That(record.RowsAdded, Is.EqualTo(1));
            Assert.That(record.RowsMoved, Is.GreaterThan(0));
            var captain = manifest.Entries.Single(e => e.Op == MergeManifestOp.Modified && e.Changes!.ContainsKey(nameof(Team.CaptainID)));
            Assert.That(captain.Changes![nameof(Team.CaptainID)].New.GetInt32(), Is.Not.EqualTo(captain.Changes[nameof(Team.CaptainID)].Old.GetInt32()));
        }
    }

    [Test]
    public async Task Merge_ThatIsRefused_WritesNoRecord()
    {
        var other = Team.Seed(seasonID: _season.SeasonID, teamName: "Swords");
        Context.Teams.Add(other);
        await Context.SaveChangesAsync();
        Context.Set<TeamPlayer>().AddRange(TeamPlayer.Seed(teamID: _team.TeamID, userID: _real.Id), TeamPlayer.Seed(teamID: other.TeamID, userID: _placeholder.Id));
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<MergeConflictException>(() => _merges.Merge(_real.Id, _placeholder.Id));

        Assert.That(await Context.UserMerges.AnyAsync(), Is.False);
    }

    [TestCase(GamertagClaimKind.RosterMatch, MergeTrigger.ClaimApproval)]
    [TestCase(GamertagClaimKind.SetGamertag, MergeTrigger.ClaimApproval)]
    [TestCase(GamertagClaimKind.XuidLookup, MergeTrigger.XuidLookup)]
    public async Task ApprovingAClaim_RecordsItsMerge(GamertagClaimKind kind, MergeTrigger trigger)
    {
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        var claim = kind == GamertagClaimKind.XuidLookup
            ? GamertagClaim.FromXuidLookup(10, "Old Tag", "OLD TAG", _placeholder.Id, "Now Tag", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            : GamertagClaim.Suggest(kind, 10, "Old Tag", "OLD TAG", _placeholder.Id);
        Context.GamertagClaims.Add(claim);
        await Context.SaveChangesAsync();

        await new GamertagClaimService(Context, _merges).Approve(claim.GamertagClaimID);

        Context.ChangeTracker.Clear();
        var record = await Context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((record.Trigger, record.Reason, record.SourceUserID, record.TargetUserID), Is.EqualTo((trigger, $"claim {claim.GamertagClaimID}", _placeholder.Id, _real.Id)));
            Assert.That(manifest.Entries.Select(e => (e.Op, e.Table)), Does.Contain((MergeManifestOp.Deleted, "Auth.Users")).And.Contain((MergeManifestOp.Modified, "Event.PlayerSeasonStats")));
        }
    }

    [Test]
    public async Task Undo_PutsEverythingBackExactly()
    {
        await GiveThePlaceholderEverything();
        var before = await Snapshot();
        await _merges.Merge(_real.Id, _placeholder.Id);
        Assert.That(await Snapshot(), Is.Not.EqualTo(before));

        await _audit.Undo((await Context.UserMerges.SingleAsync()).UserMergeID, "wrong person");

        Assert.That(await Snapshot(), Is.EqualTo(before));
        Context.ChangeTracker.Clear();
        var record = await Context.UserMerges.SingleAsync();
        Assert.That((record.UndoneAt, record.UndoNote), Is.EqualTo(((DateTime?)_time.GetUtcNow().UtcDateTime, "wrong person")));
    }

    [Test]
    public async Task Undo_OfAMergeOfTwoPlayersOfARecordedGame_BringsTheDroppedLineBack()
    {
        var game = RecordedGame.Seed("mcc/1", seasonID: _season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(_placeholder.Id, null, "Old Tag", 3, 0, 0));
        game.SeedPlayer(new RecordedPlayerLine(_real.Id, null, "Real Tag", 5, 0, 0));
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var before = await Context.Set<RecordedGameParticipant>().OrderBy(p => p.RecordedGameParticipantID)
            .Select(p => new { p.RecordedGameParticipantID, p.UserID, p.Kills }).ToListAsync();

        await _merges.Merge(_real.Id, _placeholder.Id);
        Context.ChangeTracker.Clear();
        var record = await Context.UserMerges.SingleAsync();
        var dropped = MergeManifest.Parse(record.Manifest).Entries
            .Where(e => e.Op == MergeManifestOp.Deleted && e.Table.EndsWith("GameParticipants", StringComparison.Ordinal)).ToList();
        var merged = await Context.Set<RecordedGameParticipant>().Select(p => p.UserID).ToListAsync();

        await _audit.Undo(record.UserMergeID, "wrong person");

        Context.ChangeTracker.Clear();
        var after = await Context.Set<RecordedGameParticipant>().OrderBy(p => p.RecordedGameParticipantID)
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
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        Context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 10, "Old Tag", "OLD TAG", _placeholder.Id));
        await Context.SaveChangesAsync();
        var before = await Snapshot();
        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.ClaimApproval, "claim 1"));

        await _audit.Undo((await Context.UserMerges.SingleAsync()).UserMergeID, null);

        Assert.That(await Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public async Task Undo_IsRefused_WhenAMovedRowWasChangedSince()
    {
        await GiveThePlaceholderEverything();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await Context.UserMerges.SingleAsync()).UserMergeID;
        var someone = User.Seed(userName: "someone");
        Context.Users.Add(someone);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var stat = await Context.PlayerSeasonStats.SingleAsync();
        stat.ReassignUser(stat.UserID, someone.Id);
        await Context.SaveChangesAsync();
        var after = await Snapshot();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("changed since"));
            Assert.That(await Snapshot(), Is.EqualTo(after), "Nothing changed");
        }
        Context.ChangeTracker.Clear();
        Assert.That((await Context.UserMerges.SingleAsync()).UndoneAt, Is.Null);
    }

    [Test]
    public async Task Undo_IsRefused_WhileALaterMergeOfTheSameRowsStands()
    {
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        var second = User.Seed(userName: "second");
        Context.Users.Add(second);
        await Context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id);
        // Each merge and undo is a request of its own, with a fresh context: none reads another's tracked rows
        Context.ChangeTracker.Clear();
        // The stats move again: the real user is merged into another account
        await new UserMergeService(Context, _time).Merge(second.Id, _real.Id);
        Context.ChangeTracker.Clear();
        var merges = await Context.UserMerges.OrderBy(m => m.UserMergeID).ToListAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(merges[0].UserMergeID, null));

        Assert.That(ex!.Message, Does.Contain($"Merge {merges[1].UserMergeID}"));
    }

    [Test]
    public async Task Undo_InReverseOrder_Works()
    {
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.FromSheet(_season.SeasonID, _placeholder.Id, 1, new StatSheetTotals { Gamertag = "Old Tag", Kills = 7 }));
        var second = User.Seed(userName: "second");
        Context.Users.Add(second);
        await Context.SaveChangesAsync();
        var before = await Snapshot();
        // Each merge and undo is a request of its own, with a fresh context: none reads another's tracked rows
        await _merges.Merge(_real.Id, _placeholder.Id);
        Context.ChangeTracker.Clear();
        await new UserMergeService(Context, _time).Merge(second.Id, _real.Id);
        Context.ChangeTracker.Clear();
        var merges = await Context.UserMerges.OrderBy(m => m.UserMergeID).Select(m => m.UserMergeID).ToListAsync();

        await _audit.Undo(merges[1], null);
        Context.ChangeTracker.Clear();
        await _audit.Undo(merges[0], null);

        Assert.That(await Snapshot(), Is.EqualTo(before));
    }

    [Test]
    public async Task Undo_Twice_IsRefused()
    {
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await Context.UserMerges.SingleAsync()).UserMergeID;
        await _audit.Undo(mergeId, null);

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        Assert.That(ex!.Message, Does.Contain("undone already"));
    }

    [Test]
    public async Task Undo_UndoneByAnotherSysadminMeanwhile_IsRefused_AndPutsNothingBack()
    {
        await GiveThePlaceholderEverything();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await Context.UserMerges.SingleAsync()).UserMergeID;
        var afterMerge = await Snapshot();
        var options = new DbContextOptionsBuilder<FakeApplicationDbContext>().UseSqlite(Context.Database.GetDbConnection()).Options;
        var theirs = new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc);

        // As this undo opens its transaction (after it read the merge as standing), another sysadmin's undo is saved
        await using (var racing = new FakeApplicationDbContext(options)
        {
            BeforeTransaction = async ct =>
            {
                await using var other = new FakeApplicationDbContext(options);
                _ = (await other.UserMerges.SingleAsync(m => m.UserMergeID == mergeId, ct)).Undo(theirs, null, "theirs");
                await other.SaveChangesAsync(ct);
            },
        })
        {
            var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new UserMergeAuditService(racing, _time).Undo(mergeId, "mine"));
            Assert.That(ex!.Message, Is.EqualTo($"Merge {mergeId} was undone already"));
        }

        Assert.That(await Snapshot(), Is.EqualTo(afterMerge), "this undo put nothing back");
        var record = await Context.UserMerges.AsNoTracking().SingleAsync();
        Assert.That((record.UndoneAt, record.UndoNote), Is.EqualTo(((DateTime?)theirs, "theirs")), "the other undo's stamp stays");
    }

    [Test]
    public async Task Undo_IsRefused_WhenARowCantBePutBack()
    {
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await Context.UserMerges.SingleAsync()).UserMergeID;
        // Since the merge, another user took the removed placeholder's user name (unique): it can't come back as it was
        Context.Users.Add(User.Seed(userName: "historical:Old Tag", normalizedUserName: "HISTORICAL:OLD TAG", displayName: "Taker"));
        await Context.SaveChangesAsync();
        var before = await Snapshot();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.StartWith("Can't undo: a row could not be put back as it was ("));
            Assert.That(await Snapshot(), Is.EqualTo(before), "nothing left half undone");
            Assert.That((await Context.UserMerges.AsNoTracking().SingleAsync()).UndoneAt, Is.Null);
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
        Context.ChangeTracker.Clear();
        var record = await Context.UserMerges.SingleAsync();
        // A hand-made manifest whose removed row's key is in use: the real user's own
        var manifest = MergeManifest.Parse(record.Manifest);
        var userRow = manifest.Entries.Single(e => e.Op == MergeManifestOp.Deleted && e.Table == "Auth.Users");
        userRow.Key["Id"] = System.Text.Json.JsonSerializer.SerializeToElement(_real.Id);
        // Past the record's methods, as only something outside the class could
        Context.Entry(record).Property(r => r.Manifest).CurrentValue = manifest.Serialize();
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(record.UserMergeID, null));

        Assert.That(ex!.Message, Does.Contain("taken"));
    }

    /// <summary>
    /// A merge made before the UserAggregate migration snapshotted its removed source as it was then: e.g. the source
    /// kept its win streak when its wins moved, which CK_Auth_Users_LateLeagueRecord now refuses. The undo settles the
    /// row as the migration settles live ones, and puts it back.
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
        Context.ChangeTracker.Clear();
        var record = await Context.UserMerges.SingleAsync();
        var manifest = MergeManifest.Parse(record.Manifest);
        var userRow = manifest.Entries.Single(e => e.Op == MergeManifestOp.Deleted && e.Table == "Auth.Users");
        var values = userRow.Values!;
        values[nameof(User.Wins)] = System.Text.Json.JsonSerializer.SerializeToElement(wins);
        values[nameof(User.Losses)] = System.Text.Json.JsonSerializer.SerializeToElement(losses);
        values[nameof(User.WinStreak)] = System.Text.Json.JsonSerializer.SerializeToElement(winStreak);
        values[nameof(User.LossStreak)] = System.Text.Json.JsonSerializer.SerializeToElement(lossStreak);
        values[nameof(User.RegionID)] = System.Text.Json.JsonSerializer.SerializeToElement(regionId);
        values[nameof(User.RegionSource)] = System.Text.Json.JsonSerializer.SerializeToElement(regionSource);
        Context.Entry(record).Property(r => r.Manifest).CurrentValue = manifest.Serialize();
        await Context.SaveChangesAsync();

        await _audit.Undo(record.UserMergeID, null);

        Context.ChangeTracker.Clear();
        var source = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == _placeholder.Id);
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
        Context.Set<UserExperience>().Add(new UserExperience(2) { UserID = _placeholder.Id });
        await Context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id);
        var mergeId = (await Context.UserMerges.SingleAsync()).UserMergeID;
        Context.Set<UserExperience>().Remove(await Context.Set<UserExperience>().SingleAsync());
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => _audit.Undo(mergeId, null));

        Assert.That(ex!.Message, Does.Contain("gone"));
    }

    [Test]
    public async Task List_IsNewestFirst_AndGetSummarisesTheTables()
    {
        await GiveThePlaceholderEverything();
        var other = User.Seed(userName: "historical:Other", normalizedUserName: "HISTORICAL:OTHER", isDummyUser: true);
        Context.Users.Add(other);
        await Context.SaveChangesAsync();
        await _merges.Merge(_real.Id, _placeholder.Id, MergeOptions.For(MergeTrigger.SetGamertag, "set gamertag Real Tag", _real.Id));
        await new UserMergeService(Context, _time).Merge(_real.Id, other.Id, MergeOptions.For(MergeTrigger.DuplicateSuggestion));

        var page = await _audit.List(new GrifballWebApp.Application.Dtos.PaginationFilter(1, 10));
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
    public async Task Merge_OfBothPlayersOfAnEndedMatch_DropsTheSources_AndUndoBringsItBackWithItsVotes()
    {
        var match = MatchedMatch.Start([_real.Id], [_placeholder.Id]);
        Context.MatchedMatches.Add(match);
        await Context.SaveChangesAsync();
        match.VoteForWinner(_placeholder.Id, WinnerVote.Away);
        match.VoteToKick(_placeholder.Id, _real.Id);
        match.VoteToKick(_real.Id, _placeholder.Id);
        match.DecideByVote(MatchedSide.Away);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        async Task<(int[] Players, int WinnerVotes, int KickVotes)> Rows()
        {
            Context.ChangeTracker.Clear();
            return (await Context.Set<MatchedPlayer>().Select(p => p.UserID).OrderBy(u => u).ToArrayAsync(),
                await Context.Set<MatchedWinnerVote>().CountAsync(), await Context.Set<MatchedKickVote>().CountAsync());
        }
        var before = await Rows();

        await _merges.Merge(_real.Id, _placeholder.Id);
        var merged = await Rows();
        await _audit.Undo((await Context.UserMerges.SingleAsync()).UserMergeID, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merged, Is.EqualTo((new[] { _real.Id }, 0, 0)), "One person once: the source's player went, with its votes and those against it");
            Assert.That(await Rows(), Is.EqualTo(before));
        }
    }
}
