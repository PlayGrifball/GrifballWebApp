using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using T = GrifballWebApp.Application.Live.LiveTopics;

namespace GrifballWebApp.Application.Live;

/// <summary>
/// Every committed domain event, to the pages that show what it changed (<see cref="LiveChanges.Of"/>). A Halo game's
/// players' profiles hear of it where they are watched (the hub's delivery looks up whose its accounts are, once a
/// batch), not here on the save's path.
/// </summary>
public sealed class LiveUpdatesHandler(ILiveUpdates live) : IDomainEventHandler<IDomainEvent>
{
    public Task Handle(IDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        live.Publish(LiveChanges.Of(domainEvent));
        return Task.CompletedTask;
    }
}

/// <summary>Which topics a domain event changes: the pages that show what happened. Read once its save is committed.</summary>
public static class LiveChanges
{
    public static IReadOnlyList<LiveChange> Of(IDomainEvent domainEvent) => [.. Topics(domainEvent)];

    private static IEnumerable<LiveChange> Topics(IDomainEvent domainEvent) => domainEvent switch
    {
        // League: a season and what shows it
        SeasonCreated e => All(T.Seasons, T.Season(e.Season.SeasonID)),
        SeasonEdited e => All(T.Seasons, T.Season(e.Season.SeasonID), T.Teams(e.Season.SeasonID), T.Draft(e.Season.SeasonID)),
        SeasonScheduled e => All(T.Seasons, T.Season(e.Season.SeasonID), T.Teams(e.Season.SeasonID), T.Draft(e.Season.SeasonID), T.Signups(e.Season.SeasonID)),
        SeasonAnnounced e => All(T.Seasons, T.Season(e.Season.SeasonID), T.Teams(e.Season.SeasonID)),
        SeasonTimeslotsChanged e => All(T.Season(e.Season.SeasonID), T.Signups(e.Season.SeasonID)),
        CaptainsLockChanged e => All(T.Draft(e.Season.SeasonID)),
        SeasonCopiedFrom e => All(T.Season(e.Season.SeasonID), T.Teams(e.Season.SeasonID), T.Draft(e.Season.SeasonID), T.Signups(e.Season.SeasonID)),
        SeasonChampionsChanged e => All(T.Seasons, T.Season(e.Season.SeasonID), T.Teams(e.Season.SeasonID)),
        SeasonAwardsChanged e => All(T.Season(e.Season.SeasonID), T.Teams(e.Season.SeasonID)),

        // Its teams: the season, its teams page, the team's page, its bracket, its draft, and the profiles of who is (or was) on them
        TeamFormed e => OfTeam(e.Team, []),
        TeamDisbanded e => OfTeam(e.Team, e.UserIDs),
        TeamRenamed e => OfTeam(e.Team, []),
        TeamCaptainChanged e => OfTeam(e.Team, []),
        TeamRosterChanged e => OfTeam(e.Team, e.UserIDs),
        PlayerDrafted e => OfTeam(e.Team, [e.UserID]),
        PlayerUndrafted e => OfTeam(e.Team, [e.UserID]),
        PickMoved e => OfTeam(e.From, [e.UserID]).Concat(All(T.Team(e.To.TeamID))),
        TeamTimeslotsChanged e => All(T.Season(e.Team.SeasonID), T.Team(e.Team.TeamID)),
        TeamArchiveRecorded e => All(T.Season(e.Team.SeasonID), T.Teams(e.Team.SeasonID), T.Team(e.Team.TeamID)),

        // Its matches: the season, its bracket and teams page, the match's page, its teams' pages, and the home page's schedule and results
        SeasonMatchCreated e => OfMatch(e.Match),
        SeasonMatchCanceled e => OfMatch(e.Match),
        SeasonMatchScheduled e => OfMatch(e.Match),
        SeasonMatchTeamsChanged e => OfMatch(e.Match).Concat(e.ReplacedTeamID is int replaced ? All(T.Team(replaced)) : []),
        SeasonMatchResultChanged e => OfMatch(e.Match),
        SeasonMatchDecided e => OfMatch(e.Match),
        SeasonMatchReopened e => OfMatch(e.Match),
        SeriesGamesChanged e => OfMatch(e.Match),

        // Its signups: the staff's pages, the draft's pool, the player's own
        PlayerSignedUp e => OfSignup(e.Signup),
        SignupChanged e => OfSignup(e.Signup),
        SignupWithdrawn e => OfSignup(e.Signup),

        RescheduleRequested e => All(T.SeasonMatch(e.Reschedule.SeasonMatchID), T.StaffReschedules),
        RescheduleChanged e => All(T.SeasonMatch(e.Reschedule.SeasonMatchID), T.StaffReschedules),
        VodChanged e => OfVod(e.Vod),
        PlayerSeasonStatsChanged e => All(T.Season(e.Stat.SeasonID), T.Player(e.Stat.UserID)).Concat(e.Stat.TeamID is int statTeamID ? All(T.Team(statTeamID)) : []),
        SchedulingRulesChanged e => All(T.Season(e.Rules.SeasonID)),
        ScheduleRecommendationChanged e => All(T.Season(e.Recommendation.SeasonID)),

        // Late League: the queue's size to everyone, a player's own place to them alone
        PlayerQueued e => All(T.Queue).Append(new LiveChange(T.MyQueue, e.Spot.UserID)),
        PlayerLeftQueue e => All(T.Queue).Append(new LiveChange(T.MyQueue, e.Spot.UserID)),
        LateLeagueMatchStarted e => All(T.Queue, T.LateLeague).Concat(e.UserIDs.Select(u => new LiveChange(T.MyQueue, u))),
        LateLeagueMatchDecided e => All(T.Queue, T.LateLeague).Concat(e.UserIDs.Select(u => new LiveChange(T.MyQueue, u))),
        LateLeagueMatchCanceled e => All(T.Queue, T.LateLeague).Concat(e.UserIDs.Select(u => new LiveChange(T.MyQueue, u))),
        LateLeagueGameRecorded e => All(T.Player(e.User.Id)),
        LateLeaguePlayerKicked e => All(T.LateLeague).Append(new LiveChange(T.MyQueue, e.UserID)),
        LateLeagueMatchChanged => All(T.LateLeague),
        MatchmakingSeasonChanged => All(T.LateLeague),
        MatchmakingPlayerStatsChanged e => All(T.LateLeague, T.Player(e.Stat.UserID)),
        LadderRankChanged e => All(T.LateLeague, T.Player(e.Rank.UserID)),

        // Halo: the match's page, and its players' accounts (their profiles: LiveUpdatesHandler)
        HaloMatchChanged e => All(T.HaloMatch(e.Match.MatchID))
            .Concat(e.Match.Participants.Select(p => p.XboxUserID).Distinct().Select(x => new LiveChange(T.Xbox(x)))),

        // Players: their profiles and the staff's pages of users
        UserChanged e => All(T.Player(e.User.Id), T.StaffUsers),
        XboxUserChanged e => All(T.Xbox(e.XboxUser.XboxUserID), T.StaffUsers),
        DiscordUserChanged => All(T.StaffUsers),
        UserMergeChanged => All(T.StaffMerges, T.StaffUsers),
        DuplicateDismissed => All(T.StaffMerges),

        // Archive
        GamertagClaimChanged e => All(T.StaffClaims).Concat(e.Claim.ClaimantUserID is int claimant ? All(T.Player(claimant)) : []),
        HistoricalImportJobChanged => All(T.StaffImports),
        HistoricalSourceChanged => All(T.StaffImports),
        RecordedGameChanged e => e.Game.SeasonID is int seasonID
            ? All(T.Season(seasonID)).Concat(e.Game.Teams.Where(t => t.TeamID != null).Select(t => new LiveChange(T.Team(t.TeamID!.Value))))
            : All(T.LateLeague),

        _ => [],
    };

    private static IEnumerable<LiveChange> All(params string[] topics) => topics.Select(t => new LiveChange(t));

    private static IEnumerable<LiveChange> OfTeam(Team team, IEnumerable<int> userIDs)
        => All(T.Season(team.SeasonID), T.Teams(team.SeasonID), T.Team(team.TeamID), T.Bracket(team.SeasonID), T.Draft(team.SeasonID)).Concat(userIDs.Distinct().Select(u => new LiveChange(T.Player(u))));

    private static IEnumerable<LiveChange> OfMatch(SeasonMatch match)
        => All(T.Seasons, T.Season(match.SeasonID), T.Bracket(match.SeasonID), T.Teams(match.SeasonID), T.SeasonMatch(match.SeasonMatchID))
            .Concat(new[] { match.HomeTeamID, match.AwayTeamID }.OfType<int>().Select(t => new LiveChange(T.Team(t))));

    private static IEnumerable<LiveChange> OfSignup(SeasonSignup signup)
        => All(T.Signups(signup.SeasonID), T.Draft(signup.SeasonID), T.Season(signup.SeasonID), T.Player(signup.UserID));

    private static IEnumerable<LiveChange> OfVod(Vod vod)
        => All(T.StaffVods)
            .Concat(vod.SeasonID is int seasonID ? All(T.Season(seasonID)) : [])
            .Concat(vod.SeasonMatchID is int seasonMatchID ? All(T.SeasonMatch(seasonMatchID)) : []);
}
