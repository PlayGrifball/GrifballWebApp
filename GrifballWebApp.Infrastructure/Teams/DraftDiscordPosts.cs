using GrifballWebApp.Application.DomainEvents;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Teams;

/// <summary>
/// The draft channel's messages, for the draft's domain events: queued on <paramref name="outbox"/> before the change
/// commits, so they are sent once it has and never for one rolled back, worded as <see cref="DraftOutbox"/> words them and
/// with the on-deck pick menus as the change left the draft. The channel is public, so a season not announced yet posts
/// nothing there: staff run its draft unseen (#435).
/// </summary>
public sealed class DraftDiscordPosts(GrifballContext context, IOutbox outbox, TeamService teams, TimeProvider? time = null)
    : IBeforeCommitHandler<PlayerDrafted>, IBeforeCommitHandler<PlayerUndrafted>, IBeforeCommitHandler<PickMoved>, IBeforeCommitHandler<CaptainsLockChanged>
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task Handle(PlayerDrafted domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
    {
        var seasonID = domainEvent.Team.SeasonID;
        if (await IsPublic(seasonID, cancellationToken))
            await outbox.EnqueuePlayerAddedAsync(context, teams, new AddPlayerToTeamRequestDto
            {
                SeasonID = seasonID,
                CaptainID = domainEvent.CaptainUserID ?? 0,
                PersonID = domainEvent.UserID,
            }, cancellationToken);
    }

    public async Task Handle(PlayerUndrafted domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
    {
        var seasonID = domainEvent.Team.SeasonID;
        if (await IsPublic(seasonID, cancellationToken))
            await outbox.EnqueuePlayerRemovedAsync(context, teams, new RemovePlayerFromTeamRequestDto
            {
                SeasonID = seasonID,
                CaptainID = domainEvent.CaptainUserID ?? 0,
                PersonID = domainEvent.UserID,
            }, cancellationToken);
    }

    public async Task Handle(PickMoved domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
    {
        var seasonID = domainEvent.From.SeasonID;
        if (await IsPublic(seasonID, cancellationToken))
            await outbox.EnqueuePlayerMovedAsync(context, teams, new MovePlayerToTeamRequestDto
            {
                SeasonID = seasonID,
                PreviousCaptainID = domainEvent.FromCaptainUserID ?? 0,
                NewCaptainID = domainEvent.ToCaptainUserID ?? 0,
                PersonID = domainEvent.UserID,
                RoundNumber = domainEvent.Round,
            }, cancellationToken);
    }

    public async Task Handle(CaptainsLockChanged domainEvent, IReadOnlyList<IDomainEvent> commit, CancellationToken cancellationToken)
    {
        var season = domainEvent.Season;
        if (await IsPublic(season.SeasonID, cancellationToken))
            await outbox.EnqueueLockChangedAsync(context, teams, season.SeasonID, season.CaptainsLocked, cancellationToken);
    }

    private Task<bool> IsPublic(int seasonId, CancellationToken ct)
        => context.Seasons.Where(Season.PublicAsOf(_time.GetUtcNow().UtcDateTime)).AnyAsync(s => s.SeasonID == seasonId, ct);
}
