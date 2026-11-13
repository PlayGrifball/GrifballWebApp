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
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// UserMergeService.Merge: what each MergeOptions flag moves or leaves, the conflicts it refuses before touching
/// anything, external logins, and the audit record every path writes. A "live two-context race" scenario
/// (UserMergeServiceCombineTests.Matchmaking_BothPutInAMatchOnceTheMergeBegan_...) needs a second connection to the
/// same database to start a match after the merge's own check; the fake context's SQLite connection is private to one
/// instance, so that race isn't reproducible here - GrifballWebApp.Integration.Test covers it.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class UserMergeServiceUnitTests : ApplicationDbContextTests
{
    private static readonly string[] DiscordThenGoogle = ["Discord", "Google"];
    private static readonly string[] BothLoginKeys = ["111", "222"];

    private UserMergeService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new UserMergeService(Context);
    }

    private async Task<(User To, User From)> TwoUsers(long? fromDiscord = null, long? toDiscord = null)
    {
        var to = User.Seed(userName: "mergeto", displayName: "Merge To", discordUserID: toDiscord);
        var from = User.Seed(userName: "mergefrom", displayName: "Merge From", discordUserID: fromDiscord);
        Context.Users.AddRange(to, from);
        await Context.SaveChangesAsync();
        return (to, from);
    }

    private async Task AddLogin(int userId, string provider, string key)
    {
        Context.UserLogins.Add(new UserLogin { UserId = userId, LoginProvider = provider, ProviderKey = key, ProviderDisplayName = provider });
        await Context.SaveChangesAsync();
    }

    private async Task AddDiscordUser(long id)
    {
        Context.DiscordUsers.Add(DiscordUser.FromDiscord(id, "discord" + id));
        await Context.SaveChangesAsync();
    }

    private Task<List<UserLogin>> LoginsOf(int userId) =>
        Context.UserLogins.AsNoTracking().Where(l => l.UserId == userId).OrderBy(l => l.LoginProvider).ToListAsync();

    [Test]
    public async Task Merge_IntoItself_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.Merge(5, 5));
        Assert.That(ex!.Message, Does.Contain("itself"));
    }

    [Test]
    public async Task Merge_Should_ThrowException_When_MergeToUserDoesNotExist()
    {
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From");
        Context.Users.Add(mergeFromUser);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(999, mergeFromUser.Id, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Did not find user 999"));
    }

    [Test]
    public async Task Merge_Should_ThrowException_When_MergeFromUserDoesNotExist()
    {
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        Context.Users.Add(mergeToUser);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(mergeToUser.Id, 999, null, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("Did not find user 999"));
    }

    [Test]
    public async Task Merge_Twice_SecondFailsWithUserNotFound()
    {
        var (to, from) = await TwoUsers();
        await _service.Merge(to.Id, from.Id);

        var ex = await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(to.Id, from.Id));
        Assert.That(ex!.UserId, Is.EqualTo(from.Id));
    }

    [Test]
    public async Task Merge_Should_TransferXboxID_When_TransferXboxIsTrue()
    {
        var xboxUser = XboxUser.FromXbox(12345, "TestGamer");
        Context.XboxUsers.Add(xboxUser);
        await Context.SaveChangesAsync();
        var mergeToUser = User.Seed(userName: "mergeto", displayName: "Merge To");
        var mergeFromUser = User.Seed(userName: "mergefrom", displayName: "Merge From", xboxUserID: xboxUser.XboxUserID);
        Context.Users.AddRange(mergeToUser, mergeFromUser);
        await Context.SaveChangesAsync();

        await _service.Merge(mergeToUser.Id, mergeFromUser.Id, new MergeOptions { TransferXbox = true });

        var updated = await Context.Users.FindAsync(mergeToUser.Id);
        Assert.That(updated!.XboxUserID, Is.EqualTo(xboxUser.XboxUserID));
    }

    [Test]
    public async Task Merge_Should_TransferTeamPlayers_AndCaptaincy_When_TransferTeamPlayersIsTrue()
    {
        var (to, from) = await TwoUsers();
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Test Team");
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        await Context.Teams.WithRoster().Where(t => t.TeamID == team.TeamID).LoadAsync();
        team.AddPlayer(from.Id);
        team.SetCaptain(from.Id);
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferTeamPlayers = true });

        Context.ChangeTracker.Clear();
        var players = await Context.Teams.Rosters().Where(tp => tp.TeamID == team.TeamID).ToListAsync();
        var dbTeam = await Context.Teams.SingleAsync(t => t.TeamID == team.TeamID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(players.Select(p => p.UserID), Is.EqualTo(new[] { to.Id }));
            Assert.That(dbTeam.CaptainID, Is.EqualTo(players.Single().TeamPlayerID), "The source captained the team: the target now does");
        }
    }

    [Test]
    public async Task TeamPlayers_OnDifferentTeamsInOneSeason_IsAConflict_AndNothingChanges()
    {
        var (to, from) = await TwoUsers();
        var season = Season.Seed(seasonName: "Clash").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var a = Team.Seed(seasonID: season.SeasonID, teamName: "A");
        var b = Team.Seed(seasonID: season.SeasonID, teamName: "B");
        Context.Teams.AddRange(a, b);
        await Context.SaveChangesAsync();
        Context.Add(TeamPlayer.Seed(teamID: a.TeamID, userID: to.Id));
        Context.Add(TeamPlayer.Seed(teamID: b.TeamID, userID: from.Id));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(to.Id, from.Id));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("Clash"));
            Assert.That(await Context.Users.AnyAsync(u => u.Id == from.Id), Is.True);
        }
    }

    [Test]
    public async Task Merge_Should_TransferSignups_When_TransferSignupsIsTrue()
    {
        var (to, from) = await TwoUsers();
        var season = Season.Seed(seasonName: "Test Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var signup = SeasonSignup.Seed(season.SeasonID, from.Id, timestamp: DateTime.UtcNow);
        Context.SeasonSignups.Add(signup);
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferSignups = true });

        var updated = await Context.SeasonSignups.FirstOrDefaultAsync(s => s.SeasonSignupID == signup.SeasonSignupID);
        Assert.That(updated!.UserID, Is.EqualTo(to.Id));
    }

    [Test]
    public async Task Signups_ForTheSameSeason_KeepTheTargets()
    {
        var (to, from) = await TwoUsers();
        var shared = Season.Seed(seasonName: "Shared").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        var onlySource = Season.Seed(seasonName: "Only source").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        Context.Seasons.AddRange(shared, onlySource);
        await Context.SaveChangesAsync();
        var targetSignup = SeasonSignup.Seed(shared.SeasonID, to.Id, timestamp: DateTime.UtcNow);
        Context.SeasonSignups.AddRange(targetSignup, SeasonSignup.Seed(shared.SeasonID, from.Id, timestamp: DateTime.UtcNow),
            SeasonSignup.Seed(onlySource.SeasonID, from.Id, timestamp: DateTime.UtcNow));
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id);

        Context.ChangeTracker.Clear();
        var signups = await Context.SeasonSignups.ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(signups.Select(x => x.UserID), Is.All.EqualTo(to.Id));
            Assert.That(signups.Select(x => x.SeasonID), Is.EquivalentTo(new[] { shared.SeasonID, onlySource.SeasonID }));
            Assert.That(signups.Single(x => x.SeasonID == shared.SeasonID).SeasonSignupID, Is.EqualTo(targetSignup.SeasonSignupID));
        }
    }

    [Test]
    public async Task Experiences_AreCombined_WithoutChangingTheKey()
    {
        var (to, from) = await TwoUsers();
        Context.Set<UserExperience>().AddRange(new UserExperience(1) { UserID = to.Id }, new UserExperience(1) { UserID = from.Id }, new UserExperience(2) { UserID = from.Id });
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id);

        Context.ChangeTracker.Clear();
        var experiences = await Context.Set<UserExperience>().Select(x => new { x.UserID, x.GameVersionID }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(experiences, Is.EquivalentTo(new[] { new { UserID = to.Id, GameVersionID = 1 }, new { UserID = to.Id, GameVersionID = 2 } }));
            Assert.That(await Context.Users.AnyAsync(x => x.Id == from.Id), Is.False);
        }
    }

    [Test]
    public async Task Merge_Should_DeleteMergeFromUser_When_DeleteMergeFromIsTrue()
    {
        var (to, from) = await TwoUsers();

        await _service.Merge(to.Id, from.Id, new MergeOptions { DeleteMergeFrom = true });

        Assert.That(await Context.Users.FindAsync(from.Id), Is.Null);
    }

    [Test]
    public async Task Merge_Should_NotDeleteMergeFromUser_When_DeleteMergeFromIsFalse()
    {
        var (to, from) = await TwoUsers();

        await _service.Merge(to.Id, from.Id, new MergeOptions { DeleteMergeFrom = false });

        Assert.That(await Context.Users.FindAsync(from.Id), Is.Not.Null);
    }

    [Test]
    public async Task Merge_Should_UseDefaultOptions_When_OptionsIsNull()
    {
        await AddDiscordUser(12345);
        var (to, from) = await TwoUsers(fromDiscord: 12345);

        await _service.Merge(to.Id, from.Id, null, CancellationToken.None);

        var updatedMergeTo = await Context.Users.FindAsync(to.Id);
        var deletedUser = await Context.Users.FindAsync(from.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(updatedMergeTo!.DiscordUserID, Is.EqualTo(12345));
            Assert.That(deletedUser, Is.Null);
        }
    }

    [Test]
    public async Task Merge_Should_DeleteUserRoles_When_Merging()
    {
        var role = new Role { Name = "TestRole" };
        Context.Roles.Add(role);
        await Context.SaveChangesAsync();
        var (to, from) = await TwoUsers();
        Context.UserRoles.Add(new UserRole { UserId = from.Id, RoleId = role.Id });
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id, null, CancellationToken.None);

        var deletedRole = await Context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == from.Id && ur.RoleId == role.Id);
        Assert.That(deletedRole, Is.Null);
    }

    // ---------- External logins ----------

    [Test]
    public async Task Merge_MovesAllExternalLogins_DeletesSource()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");
        await AddLogin(from.Id, "Google", "g-1");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferDiscord = true, DeleteMergeFrom = true });

        var moved = await LoginsOf(to.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved.Select(l => l.LoginProvider), Is.EqualTo(DiscordThenGoogle));
            Assert.That(await Context.Users.AsNoTracking().AnyAsync(u => u.Id == from.Id), Is.False);
        }
    }

    [Test]
    public async Task Merge_KeepsTargetsOwnLogins_AndAllowsSeveralForOneProvider()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(to.Id, "Discord", "222");
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var keys = (await LoginsOf(to.Id)).Select(l => l.ProviderKey).Order().ToList();
        Assert.That(keys, Is.EqualTo(BothLoginKeys));
    }

    [Test]
    public async Task Merge_TransferLoginsOff_KeepsLoginsOnSource()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferLogins = false, DeleteMergeFrom = false });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await LoginsOf(to.Id), Is.Empty);
            Assert.That((await LoginsOf(from.Id)).Single().ProviderKey, Is.EqualTo("111"));
        }
    }

    [Test]
    public async Task Merge_TransferLoginsOff_WithDelete_RemovesLoginsWithSource()
    {
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferLogins = false, DeleteMergeFrom = true });

        Assert.That(await Context.UserLogins.AsNoTracking().AnyAsync(), Is.False);
    }

    [Test]
    public async Task Merge_LinksDiscordFromLogin_WhenNeitherUserHasDiscordUserID()
    {
        await AddDiscordUser(111);
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var target = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.EqualTo(111));
    }

    [Test]
    public async Task Merge_KeepsTargetsDiscordUserID_OverTheSourcesDiscordLogin()
    {
        await AddDiscordUser(111);
        await AddDiscordUser(222);
        var (to, from) = await TwoUsers(toDiscord: 222);
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var target = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.EqualTo(222));
    }

    [TestCase("111", false, true, TestName = "Merge_DoesNotLinkDiscord_WhenDiscordUserUnknown")]
    [TestCase("not-a-number", true, true, TestName = "Merge_DoesNotLinkDiscord_WhenLoginKeyIsNotAnId")]
    [TestCase("111", true, false, TestName = "Merge_DoesNotLinkDiscord_WhenTransferDiscordOff")]
    public async Task Merge_DoesNotLinkDiscordFromLogin(string key, bool knownDiscordUser, bool transferDiscord)
    {
        if (knownDiscordUser)
            await AddDiscordUser(111);
        var (to, from) = await TwoUsers();
        await AddLogin(from.Id, "Discord", key);

        await _service.Merge(to.Id, from.Id, new MergeOptions { TransferDiscord = transferDiscord });

        var target = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.Null);
    }

    [Test]
    public async Task Merge_DoesNotLinkDiscord_WhenAThirdUserHasIt()
    {
        await AddDiscordUser(111);
        var (to, from) = await TwoUsers();
        Context.Users.Add(User.Seed(userName: "third", discordUserID: 111));
        await Context.SaveChangesAsync();
        await AddLogin(from.Id, "Discord", "111");

        await _service.Merge(to.Id, from.Id);

        var target = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == to.Id);
        Assert.That(target.DiscordUserID, Is.Null);
    }

    [Test]
    public async Task Merge_TransferDiscord_WithoutDelete_ClearsSourceLink()
    {
        await AddDiscordUser(111);
        var (to, from) = await TwoUsers(fromDiscord: 111);

        await _service.Merge(to.Id, from.Id, new MergeOptions { DeleteMergeFrom = false });

        var users = await Context.Users.AsNoTracking().Where(u => u.Id == to.Id || u.Id == from.Id).ToDictionaryAsync(u => u.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(users[to.Id].DiscordUserID, Is.EqualTo(111));
            Assert.That(users[from.Id].DiscordUserID, Is.Null);
        }
    }

    // ---------- Matchmaking ----------

    [Test]
    public async Task Matchmaking_BothInAMatchBeingPlayed_IsRefused()
    {
        var (to, from) = await TwoUsers();
        Context.MatchedMatches.Add(MatchedMatch.Start([to.Id], [from.Id]));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(to.Id, from.Id));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("still being played"));
            Assert.That(await _service.ConflictAsync(to.Id, from.Id), Is.EqualTo(ex.Message), "Suggestions are refused alike");
            Assert.That(await Context.Users.AnyAsync(x => x.Id == from.Id), Is.True);
        }
    }

    [Test]
    public async Task ConflictAsync_NoConflict_ReturnsNull()
    {
        var (to, from) = await TwoUsers();

        Assert.That(await _service.ConflictAsync(to.Id, from.Id), Is.Null);
    }

    [Test]
    public async Task Matchmaking_HistoryAndVotesMove_AndRecordsCombine()
    {
        var (to, from) = await TwoUsers();
        to.SeedLateLeagueRecord(wins: 3, losses: 1, mmr: 1100, winStreak: 2);
        from.SeedLateLeagueRecord(wins: 1, losses: 3, mmr: 900, lossStreak: 3);
        var other = User.Seed(userName: "other");
        Context.Users.Add(other);
        await Context.SaveChangesAsync();
        var match = MatchedMatch.Start(new MatchedTeam([new MatchedPlayer(from.Id)]), new MatchedTeam([new MatchedPlayer(other.Id)]));
        Context.MatchedMatches.Add(match);
        Context.QueuedPlayer.Add(QueuedPlayer.Seed(from.Id, DateTime.UtcNow));
        await Context.SaveChangesAsync();
        var fromPlayer = match.HomeTeam.Players.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(match.VoteToKick(from.Id, other.Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteToKick(other.Id, from.Id).Result, Is.EqualTo(KickVoteResult.Voted));
            Assert.That(match.VoteForWinner(from.Id, WinnerVote.Home), Is.EqualTo(WinnerVoteResult.Voted));
        }
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id);

        Context.ChangeTracker.Clear();
        var target = await Context.Users.SingleAsync(x => x.Id == to.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await Context.Set<MatchedPlayer>().SingleAsync(x => x.Id == fromPlayer.Id)).UserID, Is.EqualTo(to.Id), "History kept, as the target's");
            Assert.That(await Context.Set<MatchedKickVote>().CountAsync(), Is.EqualTo(2));
            Assert.That(await Context.Set<MatchedWinnerVote>().CountAsync(), Is.EqualTo(1));
            Assert.That(await Context.QueuedPlayer.AnyAsync(), Is.False);
            Assert.That((target.Wins, target.Losses), Is.EqualTo((4, 4)));
            Assert.That(target.MMR, Is.EqualTo(1000), "(1100 * 4 + 900 * 4) / 8");
            Assert.That((target.WinStreak, target.LossStreak), Is.EqualTo((2, 0)), "The target played: its streaks stay");
        }
    }

    [Test]
    public async Task Matchmaking_TargetWithoutGames_TakesTheSourcesRecord()
    {
        var (to, from) = await TwoUsers();
        from.SeedLateLeagueRecord(wins: 2, mmr: 1200, winStreak: 2);
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id);

        Context.ChangeTracker.Clear();
        var target = await Context.Users.SingleAsync(x => x.Id == to.Id);
        Assert.That((target.Wins, target.Losses, target.MMR, target.WinStreak), Is.EqualTo((2, 0, 1200, 2)));
    }

    [Test]
    public async Task Reschedules_TheSourceRequestedOrApproved_MoveToTheTarget()
    {
        var (to, from) = await TwoUsers();
        var season = Season.Seed(seasonName: "Reschedule Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        // Two matches: a match has at most one pending reschedule (IX_MatchReschedules_SeasonMatchID, filtered on Status = 0)
        var busyMatch = SeasonMatch.Seed(season.SeasonID, null, null);
        var lateMatch = SeasonMatch.Seed(season.SeasonID, null, null);
        Context.SeasonMatches.AddRange(busyMatch, lateMatch);
        await Context.SaveChangesAsync();
        var late = MatchReschedule.Request(lateMatch.SeasonMatchID, null, null, "late", to.Id, DateTime.UtcNow);
        Context.MatchReschedules.AddRange(MatchReschedule.Request(busyMatch.SeasonMatchID, null, null, "busy", from.Id, DateTime.UtcNow), late);
        await Context.SaveChangesAsync();
        var entry = Context.Entry(late);
        entry.Property(x => x.Status).CurrentValue = RescheduleStatus.Approved;
        entry.Property(x => x.ApprovedByUserID).CurrentValue = from.Id;
        await Context.SaveChangesAsync();

        await _service.Merge(to.Id, from.Id);

        Context.ChangeTracker.Clear();
        var reschedules = await Context.MatchReschedules.OrderBy(x => x.Reason).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reschedules.Select(x => x.RequestedByUserID), Is.All.EqualTo(to.Id));
            Assert.That(reschedules.Single(x => x.Reason == "late").ApprovedByUserID, Is.EqualTo(to.Id));
        }
    }

    // ---------- Historical stats and championships ----------

    private static Season HistoricalSeason(string name, string sourceId) => Season.Seed(seasonName: name, historicalSourceID: sourceId, gameVersionID: 1)
        .WithDates(signupsOpen: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), signupsClose: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            draftStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc), seasonStart: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            seasonEnd: new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    private static PlayerSeasonStat Stat(User user, Season season, int kills, bool playoffs = false, int stint = 1)
        => PlayerSeasonStat.FromSheet(season.SeasonID, user.Id, 1, new StatSheetTotals { Gamertag = user.DisplayName!, Kills = kills }, stintIndex: stint, isPlayoffs: playoffs);

    private static PlayerChampionship Title(User user, Season season, string tier, bool isCaptain = false) => PlayerChampionship.Seed(
        userID: user.Id, seasonID: season.SeasonID, tier: tier, isCaptain: isCaptain, gamertagUsed: user.DisplayName!,
        team: Team.Seed(seasonID: season.SeasonID, teamName: $"{tier} champs {user.Id}"));

    [Test]
    public async Task Merge_MovesStatsAndChampionships_ThenDeletesThePlaceholder()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.Add(Stat(dummy, season, kills: 10));
        Context.Set<PlayerChampionship>().Add(Title(dummy, season, "Pro"));
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Users.AnyAsync(u => u.Id == dummy.Id), Is.False);
            Assert.That(await Context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(real.Id));
            Assert.That(await Context.Set<PlayerChampionship>().Select(s => s.UserID).SingleAsync(), Is.EqualTo(real.Id));
        }
    }

    [Test]
    public async Task Merge_MovesThePlaceholdersRecordedGames_AndCombinesBothLines()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        var game = RecordedGame.Seed("mcc/2", seasonID: season.SeasonID);
        game.SeedPlayer(new RecordedPlayerLine(dummy.Id, null, "Old Tag", 3, 0, 0));
        game.SeedPlayer(new RecordedPlayerLine(real.Id, null, "Real", 5, 0, 0));
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        Assert.That(await Context.Set<RecordedGameParticipant>().Select(p => new { p.UserID, p.Kills }).ToListAsync(),
            Is.EqualTo(new[] { new { UserID = real.Id, Kills = 5 } }), "One person, one line: the stats count them once");
    }

    [Test]
    public async Task Merge_MovesThePlaceholdersLateLeagueStats()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        Context.AddRange(real, dummy);
        await Context.SaveChangesAsync();
        var late = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        Context.MatchmakingSeasons.Add(late);
        await Context.SaveChangesAsync();
        Context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Old Tag", Kills = 4, GamesPlayed = 1 }, gameVersionID: 1, matchmakingSeason: late, user: dummy));
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        Assert.That(await Context.MatchmakingPlayerStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(real.Id));
    }

    [Test]
    public async Task Merge_BothWithLateLeagueStatsForOneSeason_IsRefused_NamingIt()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        Context.AddRange(real, dummy);
        await Context.SaveChangesAsync();
        var late = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        Context.MatchmakingSeasons.Add(late);
        await Context.SaveChangesAsync();
        Context.MatchmakingPlayerStats.AddRange(
            MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Real", Kills = 1 }, gameVersionID: 1, matchmakingSeason: late, user: real),
            MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Old Tag", Kills = 2 }, gameVersionID: 1, matchmakingSeason: late, user: dummy));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(real.Id, dummy.Id));

        Assert.That(ex!.Message, Does.Contain("Late League Season 8 (Halo 3)"));
    }

    [Test]
    public async Task DeletingTheSource_WithoutMovingItsLateLeagueStats_IsRefused()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        Context.AddRange(real, dummy);
        await Context.SaveChangesAsync();
        var late = MatchmakingSeason.Seed("Late League Season 8 (Halo 3)", new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc));
        Context.MatchmakingSeasons.Add(late);
        await Context.SaveChangesAsync();
        Context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Old Tag", Kills = 4 }, gameVersionID: 1, matchmakingSeason: late, user: dummy));
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UserHasHistoricalStatsException>(() => _service.Merge(real.Id, dummy.Id, new MergeOptions { TransferSeasonStats = false }));
    }

    [Test]
    public async Task Merge_BothWithStatsForOneSeasonAndStint_IsRefused_AndNothingChanges()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.AddRange(Stat(real, season, kills: 1), Stat(dummy, season, kills: 2));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(real.Id, dummy.Id));

        Assert.That(ex!.Message, Does.Contain("Winter League 2019"));
        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Users.AnyAsync(u => u.Id == dummy.Id), Is.True);
            Assert.That(await Context.PlayerSeasonStats.CountAsync(s => s.UserID == dummy.Id), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Merge_BothWithPlayoffStatsForOneSeason_IsRefused_NamingThePlayoffs()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.AddRange(Stat(real, season, kills: 1, playoffs: true), Stat(dummy, season, kills: 2, playoffs: true));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(real.Id, dummy.Id));

        Assert.That(ex!.Message, Does.Contain("the playoffs of season Winter League 2019"));
    }

    [Test]
    public async Task Merge_DifferentStintsOfOneSeason_AreBothKept()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.AddRange(Stat(real, season, kills: 1), Stat(dummy, season, kills: 2, stint: 2));
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        Assert.That(await Context.PlayerSeasonStats.CountAsync(s => s.UserID == real.Id), Is.EqualTo(2));
    }

    [Test]
    public async Task DeletingTheSource_WithoutMovingItsStats_IsRefused_AndNothingChanges()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.Add(Stat(dummy, season, kills: 10));
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UserHasHistoricalStatsException>(() => _service.Merge(real.Id, dummy.Id, new MergeOptions { TransferSeasonStats = false }));

        Assert.That(ex!.UserId, Is.EqualTo(dummy.Id));
        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.Users.AnyAsync(u => u.Id == dummy.Id), Is.True);
            Assert.That(await Context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(dummy.Id));
        }
    }

    [Test]
    public async Task DeletingTheSource_WithoutMovingItsTitles_IsRefused()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.Set<PlayerChampionship>().Add(Title(dummy, season, "Pro"));
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UserHasHistoricalStatsException>(() => _service.Merge(real.Id, dummy.Id, new MergeOptions { TransferChampionships = false }));
    }

    [Test]
    public async Task KeepingTheSource_LeavesWhatIsNotMoved()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        Context.AddRange(real, dummy, season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.Add(Stat(dummy, season, kills: 10));
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id, new MergeOptions { TransferSeasonStats = false, DeleteMergeFrom = false });

        Context.ChangeTracker.Clear();
        Assert.That(await Context.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(dummy.Id));
    }

    [Test]
    public async Task Merge_ATitleBothHold_IsKeptOnce_AndKeepsTheCaptaincy()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var season = HistoricalSeason("Winter League 2019", "agla/winter-league-2019");
        var other = HistoricalSeason("Spring League 2019", "agla/spring-league-2019");
        Context.AddRange(real, dummy, season, other);
        await Context.SaveChangesAsync();
        var source = Title(dummy, season, "Pro", isCaptain: true);
        Context.Set<PlayerChampionship>().AddRange(Title(real, season, "Pro"), source, Title(dummy, other, "Pro"));
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        var titles = await Context.Set<PlayerChampionship>().Where(c => c.UserID == real.Id).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(titles, Has.Count.EqualTo(2));
            Assert.That(titles.Single(t => t.SeasonID == season.SeasonID).IsCaptain, Is.True);
        }
    }

    // ---------- Claims settled by the merge ----------

    [Test]
    public async Task Merge_SettlesThePlaceholdersPendingClaims()
    {
        var real = User.Seed(userName: "real", displayName: "Real");
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        Context.AddRange(real, dummy);
        Context.XboxUsers.AddRange(XboxUser.FromXbox(1, "Mine"), XboxUser.FromXbox(2, "Theirs"));
        await Context.SaveChangesAsync();
        real.TieToXbox(1);
        Context.GamertagClaims.AddRange(
            GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 1, "Old Tag", "OLD TAG", dummy.Id),
            GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 2, "Old Tag", "OLD TAG", dummy.Id));
        await Context.SaveChangesAsync();

        await _service.Merge(real.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        var claims = await Context.GamertagClaims.OrderBy(c => c.GamertagClaimID).Select(c => new { c.XboxUserID, c.Status }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(claims[0].Status, Is.EqualTo(GamertagClaimStatus.Approved), "The placeholder is the account's now");
            Assert.That(claims[1].Status, Is.EqualTo(GamertagClaimStatus.Rejected), "It can't be another account's any more");
        }
    }

    [Test]
    public async Task Merge_IntoAnotherPlaceholder_MovesItsPendingClaims()
    {
        var dummy = User.Seed(userName: "historical:Old Tag", displayName: "Old Tag", isDummyUser: true);
        var target = User.Seed(userName: "historical:Target", displayName: "Target", isDummyUser: true);
        Context.Users.AddRange(dummy, target);
        Context.XboxUsers.Add(XboxUser.FromXbox(3, "X"));
        await Context.SaveChangesAsync();
        Context.GamertagClaims.Add(GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 3, "Old Tag", "OLD TAG", dummy.Id));
        await Context.SaveChangesAsync();

        await _service.Merge(target.Id, dummy.Id);

        Context.ChangeTracker.Clear();
        var claim = await Context.GamertagClaims.SingleAsync();
        Assert.That((claim.Status, claim.PlaceholderUserID), Is.EqualTo((GamertagClaimStatus.Pending, (int?)target.Id)));
    }
}
