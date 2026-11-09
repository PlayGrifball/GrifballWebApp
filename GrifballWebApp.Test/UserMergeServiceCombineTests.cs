using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// Merges where both users have data of the same kind: experiences (keyed on the user), signups and team memberships
/// (unique per user), matchmaking history and reschedule requests (which would block or be lost with the source).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeServiceCombineTests
{
    private GrifballContext _context;
    private UserMergeService _service;
    private User _to;
    private User _from;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new UserMergeService(_context);
        _to = await _context.AddUser("target", "Target");
        _from = await _context.AddUser("source", "Source");
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private GrifballContext Fresh() => _context.NewContextLike();

    [Test]
    public async Task Experiences_AreCombined_WithoutChangingTheKey()
    {
        _context.Set<UserExperience>().AddRange(
            new UserExperience(1) { UserID = _to.Id },
            new UserExperience(1) { UserID = _from.Id },
            new UserExperience(2) { UserID = _from.Id });
        await _context.SaveChangesAsync();

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var experiences = await fresh.Set<UserExperience>().Select(x => new { x.UserID, x.GameVersionID }).ToListAsync();
        Assert.That(experiences, Is.EquivalentTo(new[] { new { UserID = _to.Id, GameVersionID = 1 }, new { UserID = _to.Id, GameVersionID = 2 } }));
        Assert.That(await fresh.Users.AnyAsync(x => x.Id == _from.Id), Is.False);
    }

    [Test]
    public async Task Signups_ForTheSameSeason_KeepTheTargets()
    {
        var shared = await _context.AddSeason("Shared");
        var onlySource = await _context.AddSeason("Only source");
        var targetSignup = await _context.AddSignup(shared, _to, "Target's team");
        await _context.AddSignup(shared, _from, "Source's team");
        await _context.AddSignup(onlySource, _from);

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var signups = await fresh.SeasonSignups.ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(signups.Select(x => x.UserID), Is.All.EqualTo(_to.Id));
            Assert.That(signups.Select(x => x.SeasonID), Is.EquivalentTo(new[] { shared.SeasonID, onlySource.SeasonID }));
            Assert.That(signups.Single(x => x.SeasonID == shared.SeasonID).SeasonSignupID, Is.EqualTo(targetSignup.SeasonSignupID));
        });
    }

    [Test]
    public async Task TeamPlayers_OnTheSameTeam_KeepTheTargetsRow_AndTheCaptaincy()
    {
        var season = await _context.AddSeason();
        var team = await _context.AddTeam(season, "Team", _from, 1, _to);
        var targetRow = await _context.Set<TeamPlayer>().SingleAsync(x => x.UserID == _to.Id);
        var otherSeason = await _context.AddSeason("Other");
        var otherTeam = await _context.AddTeam(otherSeason, "Other team", await _context.AddUser("cap"), 1, _from);

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var dbTeam = await fresh.Teams.SingleAsync(x => x.TeamID == team.TeamID);
        var players = await fresh.Set<TeamPlayer>().Where(x => x.TeamID == team.TeamID).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(players.Select(x => x.UserID), Is.EqualTo(new[] { _to.Id }));
            Assert.That(dbTeam.CaptainID, Is.EqualTo(targetRow.TeamPlayerID), "The source captained the team: the target now does");
            Assert.That(players.Single().DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(await fresh.Set<TeamPlayer>().AnyAsync(x => x.TeamID == otherTeam.TeamID && x.UserID == _to.Id), Is.True);
        });
    }

    [Test]
    public async Task TeamPlayers_OnDifferentTeamsInOneSeason_IsAConflict_AndNothingChanges()
    {
        var season = await _context.AddSeason("Clash");
        await _context.AddTeam(season, "A", _to, 1);
        await _context.AddTeam(season, "B", _from, 2);

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(_to.Id, _from.Id));

        Assert.That(ex!.Message, Does.Contain("Clash"));
        await using var fresh = Fresh();
        Assert.That(await fresh.Users.AnyAsync(x => x.Id == _from.Id), Is.True);
    }

    [Test]
    public async Task Matchmaking_BothInAMatchBeingPlayed_IsRefused()
    {
        _context.MatchedMatches.Add(MatchedMatch.Start([_to.Id], [_from.Id]));
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<MergeConflictException>(() => _service.Merge(_to.Id, _from.Id));

        await using var fresh = Fresh();
        Assert.Multiple(async () =>
        {
            Assert.That(ex!.Message, Does.Contain("still being played"));
            Assert.That(await _service.ConflictAsync(_to.Id, _from.Id), Is.EqualTo(ex.Message), "Suggestions are refused alike");
            Assert.That(await fresh.Users.AnyAsync(x => x.Id == _from.Id), Is.True);
            Assert.That(await fresh.Set<MatchedPlayer>().Select(p => p.UserID).ToListAsync(), Is.EquivalentTo(new[] { _to.Id, _from.Id }));
        });
    }

    [Test]
    public async Task Matchmaking_BothPutInAMatchOnceTheMergeBegan_FailsTheMerge_AndNothingIsSaved()
    {
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(TestDatabase.Provider, _context.Database.GetConnectionString()!).Options;
        await using (var racing = new MatchStartedAsTheMergeBegins(options, _to.Id, _from.Id))
        {
            // The caller's own change, left unsaved (as DiscordAccountLinkService's claim): the merge writes it first, inside its
            // transaction, so only a rollback keeps it out
            (await racing.Users.SingleAsync(x => x.Id == _to.Id)).Rename("Saved by the failed merge");
            // Not a MergeConflictException: callers take that to mean nothing changed, and carry on
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new UserMergeService(racing).Merge(_to.Id, _from.Id));
            Assert.That(ex!.Message, Does.Contain("while they were being merged"));
        }

        await using var fresh = Fresh();
        Assert.Multiple(async () =>
        {
            Assert.That(await fresh.Users.AnyAsync(x => x.Id == _from.Id), Is.True);
            Assert.That((await fresh.Users.SingleAsync(x => x.Id == _to.Id)).DisplayName, Is.EqualTo(_to.DisplayName),
                "The change written inside the transaction went with it: rolled back when the context was disposed");
            Assert.That(await fresh.UserMerges.AnyAsync(), Is.False, "No record either");
            Assert.That(await fresh.Set<MatchedPlayer>().Select(p => p.UserID).ToListAsync(), Is.EquivalentTo(new[] { _to.Id, _from.Id }));
        });
    }

    /// <summary>The queue starts a match of both users, in a context of its own, after the merge's check and before its
    /// transaction.</summary>
    private sealed class MatchStartedAsTheMergeBegins(DbContextOptions<GrifballContext> options, int home, int away) : GrifballContext(options)
    {
        public override async Task StartTransactionAsync(CancellationToken cancellationToken = default)
        {
            await using (var queue = new GrifballContext(options))
            {
                queue.MatchedMatches.Add(MatchedMatch.Start([home], [away]));
                await queue.SaveChangesAsync(cancellationToken);
            }
            await base.StartTransactionAsync(cancellationToken);
        }
    }

    [Test]
    public async Task Matchmaking_HistoryAndVotesMove_AndRecordsCombine()
    {
        _to.SeedLateLeagueRecord(wins: 3, losses: 1, mmr: 1100, winStreak: 2);
        _from.SeedLateLeagueRecord(wins: 1, losses: 3, mmr: 900, lossStreak: 3);
        var other = await _context.AddUser("other");
        var match = MatchedMatch.Start(
            new MatchedTeam([new MatchedPlayer(_from.Id)]),
            new MatchedTeam([new MatchedPlayer(other.Id)]));
        _context.MatchedMatches.Add(match);
        _context.QueuedPlayer.Add(QueuedPlayer.Seed(_from.Id, DateTime.UtcNow));
        await _context.SaveChangesAsync();
        var fromPlayer = match.HomeTeam.Players.Single();
        var otherPlayer = match.AwayTeam.Players.Single();
        Assert.That(match.VoteToKick(_from.Id, other.Id).Result, Is.EqualTo(KickVoteResult.Voted));
        Assert.That(match.VoteToKick(other.Id, _from.Id).Result, Is.EqualTo(KickVoteResult.Voted));
        Assert.That(match.VoteForWinner(_from.Id, WinnerVote.Home), Is.EqualTo(WinnerVoteResult.Voted));
        await _context.SaveChangesAsync();

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var target = await fresh.Users.SingleAsync(x => x.Id == _to.Id);
        Assert.Multiple(async () =>
        {
            Assert.That((await fresh.Set<MatchedPlayer>().SingleAsync(x => x.Id == fromPlayer.Id)).UserID, Is.EqualTo(_to.Id), "History kept, as the target's");
            Assert.That(await fresh.Set<MatchedKickVote>().CountAsync(), Is.EqualTo(2));
            Assert.That(await fresh.Set<MatchedWinnerVote>().CountAsync(), Is.EqualTo(1));
            Assert.That(await fresh.QueuedPlayer.AnyAsync(), Is.False);
            Assert.That((target.Wins, target.Losses), Is.EqualTo((4, 4)));
            Assert.That(target.MMR, Is.EqualTo(1000), "(1100 * 4 + 900 * 4) / 8");
            Assert.That((target.WinStreak, target.LossStreak), Is.EqualTo((2, 0)), "The target played: its streaks stay");
        });
    }

    [Test]
    public async Task Matchmaking_TargetWithoutGames_TakesTheSourcesRecord()
    {
        _from.SeedLateLeagueRecord(wins: 2, mmr: 1200, winStreak: 2);
        await _context.SaveChangesAsync();

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var target = await fresh.Users.SingleAsync(x => x.Id == _to.Id);
        Assert.That((target.Wins, target.Losses, target.MMR, target.WinStreak), Is.EqualTo((2, 0, 1200, 2)));
    }

    [Test]
    public async Task Reschedules_TheSourceRequestedOrApproved_MoveToTheTarget()
    {
        var season = await _context.AddSeason();
        var seasonMatch = SeasonMatch.Seed(season.SeasonID, null, null);
        _context.SeasonMatches.Add(seasonMatch);
        await _context.SaveChangesAsync();
        var late = MatchReschedule.Request(seasonMatch.SeasonMatchID, null, null, "late", _to.Id, DateTime.UtcNow);
        _context.MatchReschedules.AddRange(MatchReschedule.Request(seasonMatch.SeasonMatchID, null, null, "busy", _from.Id, DateTime.UtcNow), late);
        RescheduleTestData.Decided(_context, late, RescheduleStatus.Approved, _from.Id);
        await _context.SaveChangesAsync();

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var reschedules = await fresh.MatchReschedules.OrderBy(x => x.Reason).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(reschedules.Select(x => x.RequestedByUserID), Is.All.EqualTo(_to.Id));
            Assert.That(reschedules.Single(x => x.Reason == "late").ApprovedByUserID, Is.EqualTo(_to.Id));
        });
    }
}
