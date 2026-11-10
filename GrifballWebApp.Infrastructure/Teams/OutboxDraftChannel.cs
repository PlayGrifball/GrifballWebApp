using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Teams;

namespace GrifballWebApp.Infrastructure.Teams;

/// <summary>The draft channel's messages (<see cref="IDraftChannel"/>), queued on <paramref name="outbox"/> as <see cref="DraftOutbox"/> words them.</summary>
public sealed class OutboxDraftChannel(GrifballContext context, IOutbox outbox) : IDraftChannel
{
    public Task PlayerAddedAsync(TeamService teams, AddPlayerToTeamRequestDto dto, CancellationToken ct)
        => outbox.EnqueuePlayerAddedAsync(context, teams, dto, ct);

    public Task PlayerRemovedAsync(TeamService teams, RemovePlayerFromTeamRequestDto dto, CancellationToken ct)
        => outbox.EnqueuePlayerRemovedAsync(context, teams, dto, ct);

    public Task PlayerMovedAsync(TeamService teams, MovePlayerToTeamRequestDto dto, CancellationToken ct)
        => outbox.EnqueuePlayerMovedAsync(context, teams, dto, ct);

    public Task LockChangedAsync(TeamService teams, int seasonId, bool locked, CancellationToken ct)
        => outbox.EnqueueLockChangedAsync(context, teams, seasonId, locked, ct);
}
