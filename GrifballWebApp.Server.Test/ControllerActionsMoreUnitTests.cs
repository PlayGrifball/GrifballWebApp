using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Reschedule;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Historical.Vods;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.MatchPlanner;
using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// Controllers' actions done, past their guards: VODs linked and dismissed, matches made and timed, the queue left,
/// reschedules requested and decided, a season organized, a duplicate merged and a claim rejected. Each over its real
/// Application service on a temp-file SQLite GrifballContext (the outbox and the user merge substituted).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ControllerActionsMoreUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime Published = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly string[] TheVod = ["Grifs vs Rivals"];

    private async Task<(Season Season, Team Home, Team Away, SeasonMatch Match)> AMatch()
    {
        var season = await AddSeason("Winter");
        var home = await AddTeam(season, "Grifs");
        var away = await AddTeam(season, "Rivals");
        return (season, home, away, await AddSeasonMatch(season, home, away));
    }

    private VodController Vods() => new(new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!));

    private async Task<Vod> AVodToReview()
    {
        var vod = Vod.Seed("abc123", "Grifs vs Rivals", Published, VodMatchStatus.NeedsReview);
        Db.Vods.Add(vod);
        await Db.SaveChangesAsync();
        return vod;
    }

    [Test]
    public async Task Vod_LinkedToAMatch_ShowsForItsSeasonTeamsAndPlayers()
    {
        var (season, home, away, match) = await AMatch();
        var player = await AddUser(5, "Grifter1");
        Db.Set<TeamPlayer>().Add(TeamPlayer.Seed(teamID: home.TeamID, userID: player.Id));
        await Db.SaveChangesAsync();
        var vod = await AVodToReview();
        var controller = Vods();

        var linked = await controller.Link(vod.VodID, new LinkVodRequest { SeasonMatchID = match.SeasonMatchID }, CancellationToken.None);

        Db.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(linked, Is.InstanceOf<NoContentResult>());
            Assert.That((await controller.ForSeason(season.SeasonID, CancellationToken.None)).Select(v => v.Title), Is.EqualTo(TheVod));
            Assert.That((await controller.ForTeam(away.TeamID, CancellationToken.None)).Select(v => v.Title), Is.EqualTo(TheVod));
            Assert.That((await controller.ForPlayer(player.Id, CancellationToken.None)).Select(v => v.Title), Is.EqualTo(TheVod));
        }
    }

    [Test]
    public async Task Vod_LinkedToNothing_IsABadRequestSayingWhatToPick()
    {
        var vod = await AVodToReview();

        var result = await Vods().Link(vod.VodID, new LinkVodRequest(), CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "Pick a season or a season match.");
    }

    [Test]
    public async Task Vod_ThatIsNotThere_IsNotFound_ToLinkOrDismiss()
    {
        var controller = Vods();

        var link = await controller.Link(999, new LinkVodRequest { SeasonID = 1 }, CancellationToken.None);
        var dismiss = await controller.Dismiss(999, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            link.AssertProblem(StatusCodes.Status404NotFound, "There is no such VOD");
            dismiss.AssertProblem(StatusCodes.Status404NotFound, "There is no such VOD");
        }
    }

    [Test]
    public async Task Vod_Dismissed_LeavesTheReview()
    {
        var vod = await AVodToReview();
        var controller = Vods();

        var result = await controller.Dismiss(vod.VodID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(await controller.Review(CancellationToken.None), Is.Empty);
        }
    }

    [Test]
    public async Task MatchPlanner_MakesTheMatches_ThenTimesOne()
    {
        var season = await AddSeason("Winter");
        await AddTeam(season, "Grifs");
        await AddTeam(season, "Rivals");
        var controller = new MatchPlannerController(new MatchPlannerService(Db));
        var when = new DateTime(2026, 11, 20, 20, 0, 0, DateTimeKind.Utc);

        var made = await controller.CreateSeasonMatches(season.SeasonID, homeMatchesPerTeam: 1, bestOf: 3, CancellationToken.None);
        var first = await Db.SeasonMatches.Where(m => m.SeasonID == season.SeasonID).OrderBy(m => m.SeasonMatchID).Select(m => m.SeasonMatchID).FirstAsync();
        var timed = await controller.UpdateMatchTime(first, new UpdateMatchTimeDto { SeasonMatchID = 0, Time = when }, CancellationToken.None);

        Db.ChangeTracker.Clear();
        var matches = await Db.SeasonMatches.Where(m => m.SeasonID == season.SeasonID).OrderBy(m => m.SeasonMatchID).Select(m => new { m.BestOf, m.ScheduledTime }).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.InstanceOf<NoContentResult>());
            Assert.That(timed, Is.InstanceOf<NoContentResult>());
            Assert.That(matches.Select(m => m.BestOf), Is.All.EqualTo(3));
            Assert.That(matches.Select(m => m.ScheduledTime), Is.EqualTo(new DateTime?[] { when, null }));
        }
    }

    private static QueueController Queue(IQueueRepository queue, string? userID = "5")
        => new QueueController(new QueueMembership(queue, null!, Substitute.For<IQueueNotifier>()), queue, new ConfigurationBuilder().Build()).WithUser(userID);

    [Test]
    public async Task Queue_Leave_LeavesIt_AndIsThePlayersStatusReadAgain()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.RemovePlayerToQueue(5, Arg.Any<CancellationToken>()).Returns(true);
        queue.PlayerState(5, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(5, HasGamertag: false, InQueue: false, InMatch: false, QueueCount: 2));

        var result = await Queue(queue).Leave(CancellationToken.None);

        Assert.That((result.Value!.InQueue, result.Value.HasGamertag, result.Value.Count), Is.EqualTo((false, false, 2)));
    }

    [Test]
    public async Task Queue_Leave_NotInIt_IsAConflictSayingSo()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.RemovePlayerToQueue(5, Arg.Any<CancellationToken>()).Returns(false);

        var result = await Queue(queue).Leave(CancellationToken.None);

        result.AssertProblem(StatusCodes.Status409Conflict, QueueMessages.For(Domain.LateLeague.QueueLeaveResult.NotInQueue));
    }

    [Test]
    public async Task Queue_Join_AlreadyIn_IsAConflictSayingSo()
    {
        var queue = Substitute.For<IQueueRepository>();
        queue.PlayerState(5, Arg.Any<CancellationToken>()).Returns(new QueuePlayerState(5, true, InQueue: true, InMatch: false, QueueCount: 3));

        var result = await Queue(queue).Join(CancellationToken.None);

        result.AssertProblem(StatusCodes.Status409Conflict, QueueMessages.For(Domain.LateLeague.QueueJoinResult.AlreadyInQueue));
    }

    [Test]
    public async Task Reschedule_RequestedThenDecided_ByACommissioner()
    {
        var (_, _, _, match) = await AMatch();
        var commissioner = await AddUser(5, "Commish", "Commissioner");
        var controller = new RescheduleController(new RescheduleService(Db, Substitute.For<IOutbox>())).WithUser(commissioner.Id.ToString(), "Commish", "Commissioner");

        var requested = await controller.RequestReschedule(match.SeasonMatchID, new RescheduleRequestDto { Reason = "Away all week" }, CancellationToken.None);
        var rescheduleID = ((RescheduleRequestedDto)((CreatedAtRouteResult)requested).Value!).RescheduleID;
        var decided = await controller.ProcessReschedule(rescheduleID, new ProcessRescheduleDto { Approved = false, CommissionerNotes = "No" }, CancellationToken.None);

        Db.ChangeTracker.Clear();
        var request = await Db.MatchReschedules.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(((CreatedAtRouteResult)requested).RouteName, Is.EqualTo("GetSeasonMatchPage"));
            Assert.That(decided, Is.InstanceOf<NoContentResult>());
            Assert.That((request.MatchRescheduleID, request.Status, request.ApprovedByUserID), Is.EqualTo((rescheduleID, RescheduleStatus.Rejected, (int?)commissioner.Id)));
        }
    }

    [Test]
    public async Task Reschedule_OfAMatchThatIsNotThere_IsNotFound()
    {
        var commissioner = await AddUser(5, "Commish", "Commissioner");
        var controller = new RescheduleController(new RescheduleService(Db, Substitute.For<IOutbox>())).WithUser(commissioner.Id.ToString());

        var requested = await controller.RequestReschedule(999, new RescheduleRequestDto { Reason = "Away" }, CancellationToken.None);
        var decided = await controller.ProcessReschedule(999, new ProcessRescheduleDto { Approved = true }, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            requested.AssertProblem(StatusCodes.Status404NotFound, "Season match not found");
            decided.AssertProblem(StatusCodes.Status404NotFound, "Reschedule request not found");
        }
    }

    [Test]
    public async Task EventOrganizer_CreatesASeason_ThenEditsIt()
    {
        var controller = new EventOrganizerController(new EventOrganizerService(Db));
        var open = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
        var season = new SeasonDto
        {
            SeasonName = "Winter",
            GameVersionID = 1,
            SignupsOpen = open,
            SignupsClose = open.AddDays(7),
            DraftStart = open.AddDays(10),
            SeasonStart = open.AddDays(14),
            SeasonEnd = open.AddDays(60),
        };

        var created = await controller.UpsertSeason(season, CancellationToken.None);
        var seasonID = (int)((CreatedAtActionResult)created.Result!).Value!;
        season.SeasonID = seasonID;
        season.SeasonName = "Winter Cup";
        var edited = await controller.UpsertSeason(season, CancellationToken.None);

        Db.ChangeTracker.Clear();
        var saved = await Db.Seasons.SingleAsync(s => s.SeasonID == seasonID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(edited.Value, Is.EqualTo(seasonID));
            Assert.That((saved.SeasonName, saved.SignupsOpen), Is.EqualTo(("Winter Cup", open)));
        }
    }

    private GamertagClaimsController Claims(IUserMergeService merges) => new(new GamertagClaimService(Db, merges),
        new XuidLookupService(Db, null!, new GamertagMatching(Db), Options.Create(new XuidLookupOptions()), TimeProvider.System),
        new XuidLookupSignal(),
        new DuplicateSuggestionService(Db, merges, TimeProvider.System));

    [Test]
    public async Task MergeDuplicate_MergesThePlaceholderIntoThePlayer()
    {
        var merges = Substitute.For<IUserMergeService>();
        var player = await AddUser(5, "Grifter1");
        var placeholder = User.ImportPlaceholder("historical:Grifter1", "Grifter1", null);
        Db.Users.Add(placeholder);
        await Db.SaveChangesAsync();

        var result = await Claims(merges).MergeDuplicate(new DuplicatePairRequest { SourceUserID = placeholder.Id, TargetUserID = player.Id }, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        await merges.Received(1).Merge(player.Id, placeholder.Id, Arg.Any<MergeOptions?>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RejectClaim_RejectsIt_WithTheReason()
    {
        var claimant = await AddUser(5, "Claimant");
        var claim = GamertagClaim.ClaimedByUser(claimantUserID: claimant.Id, gamertag: "Grifter1", normalizedGamertag: "GRIFTER1");
        Db.Set<GamertagClaim>().Add(claim);
        await Db.SaveChangesAsync();

        var result = await Claims(Substitute.For<IUserMergeService>()).RejectClaim(claim.GamertagClaimID, new RejectGamertagClaimRequest { Reason = "Not yours" }, CancellationToken.None);

        Db.ChangeTracker.Clear();
        var rejected = await Db.Set<GamertagClaim>().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That((rejected.Status, rejected.RejectionReason), Is.EqualTo((GamertagClaimStatus.Rejected, (string?)"Not yours")));
        }
    }
}
