using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
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
        _context.UserExperiences.AddRange(
            new UserExperience { UserID = _to.Id, GameVersionID = 1 },
            new UserExperience { UserID = _from.Id, GameVersionID = 1 },
            new UserExperience { UserID = _from.Id, GameVersionID = 2 });
        await _context.SaveChangesAsync();

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var experiences = await fresh.UserExperiences.Select(x => new { x.UserID, x.GameVersionID }).ToListAsync();
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
        var targetRow = await _context.TeamPlayers.SingleAsync(x => x.UserID == _to.Id);
        var otherSeason = await _context.AddSeason("Other");
        var otherTeam = await _context.AddTeam(otherSeason, "Other team", await _context.AddUser("cap"), 1, _from);

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var dbTeam = await fresh.Teams.SingleAsync(x => x.TeamID == team.TeamID);
        var players = await fresh.TeamPlayers.Where(x => x.TeamID == team.TeamID).ToListAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(players.Select(x => x.UserID), Is.EqualTo(new[] { _to.Id }));
            Assert.That(dbTeam.CaptainID, Is.EqualTo(targetRow.TeamPlayerID), "The source captained the team: the target now does");
            Assert.That(players.Single().DraftCaptainOrder, Is.EqualTo(1));
            Assert.That(await fresh.TeamPlayers.AnyAsync(x => x.TeamID == otherTeam.TeamID && x.UserID == _to.Id), Is.True);
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
    public async Task Matchmaking_HistoryAndVotesMove_AndRecordsCombine()
    {
        _to.Wins = 3; _to.Losses = 1; _to.MMR = 1100; _to.WinStreak = 2;
        _from.Wins = 1; _from.Losses = 3; _from.MMR = 900; _from.LossStreak = 3;
        var other = await _context.AddUser("other");
        var match = new MatchedMatch
        {
            HomeTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = _from.Id }] },
            AwayTeam = new MatchedTeam { Players = [new MatchedPlayer { UserID = other.Id }] },
        };
        _context.MatchedMatches.Add(match);
        _context.QueuedPlayer.Add(new QueuedPlayer { UserID = _from.Id });
        await _context.SaveChangesAsync();
        var fromPlayer = match.HomeTeam.Players.Single();
        var otherPlayer = match.AwayTeam.Players.Single();
        _context.MatchedKickVotes.Add(new MatchedKickVote { MatchId = match.Id, VoterMatchedPlayerId = fromPlayer.Id, KickMatchedPlayerId = otherPlayer.Id });
        _context.MatchedKickVotes.Add(new MatchedKickVote { MatchId = match.Id, VoterMatchedPlayerId = otherPlayer.Id, KickMatchedPlayerId = fromPlayer.Id });
        _context.MatchedWinnerVotes.Add(new MatchedWinnerVote { MatchId = match.Id, MatchedPlayerId = fromPlayer.Id, WinnerVote = WinnerVote.Home });
        await _context.SaveChangesAsync();

        await _service.Merge(_to.Id, _from.Id);

        await using var fresh = Fresh();
        var target = await fresh.Users.SingleAsync(x => x.Id == _to.Id);
        Assert.Multiple(async () =>
        {
            Assert.That((await fresh.MatchedPlayers.SingleAsync(x => x.Id == fromPlayer.Id)).UserID, Is.EqualTo(_to.Id), "History kept, as the target's");
            Assert.That(await fresh.MatchedKickVotes.CountAsync(), Is.EqualTo(2));
            Assert.That(await fresh.MatchedWinnerVotes.CountAsync(), Is.EqualTo(1));
            Assert.That(await fresh.QueuedPlayer.AnyAsync(), Is.False);
            Assert.That((target.Wins, target.Losses), Is.EqualTo((4, 4)));
            Assert.That(target.MMR, Is.EqualTo(1000), "(1100 * 4 + 900 * 4) / 8");
            Assert.That((target.WinStreak, target.LossStreak), Is.EqualTo((2, 0)), "The target played: its streaks stay");
        });
    }

    [Test]
    public async Task Matchmaking_TargetWithoutGames_TakesTheSourcesRecord()
    {
        _from.Wins = 2; _from.MMR = 1200; _from.WinStreak = 2;
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
        var seasonMatch = new SeasonMatch { SeasonID = season.SeasonID };
        _context.SeasonMatches.Add(seasonMatch);
        await _context.SaveChangesAsync();
        _context.MatchReschedules.AddRange(
            new MatchReschedule { SeasonMatchID = seasonMatch.SeasonMatchID, Reason = "busy", RequestedByUserID = _from.Id },
            new MatchReschedule { SeasonMatchID = seasonMatch.SeasonMatchID, Reason = "late", RequestedByUserID = _to.Id, ApprovedByUserID = _from.Id });
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
