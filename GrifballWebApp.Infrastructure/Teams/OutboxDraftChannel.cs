using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Teams;

/// <summary>
/// The draft channel's messages (<see cref="IDraftChannel"/>), queued on <paramref name="outbox"/> as <see cref="DraftOutbox"/>
/// words them. The channel is public, so a season not announced yet posts nothing there: staff run its draft unseen (#435).
/// </summary>
public sealed class OutboxDraftChannel(GrifballContext context, IOutbox outbox, TimeProvider? time = null) : IDraftChannel
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    public async Task PlayerAddedAsync(TeamService teams, AddPlayerToTeamRequestDto dto, CancellationToken ct)
    {
        if (await IsPublic(dto.SeasonID, ct))
            await outbox.EnqueuePlayerAddedAsync(context, teams, dto, ct);
    }

    public async Task PlayerRemovedAsync(TeamService teams, RemovePlayerFromTeamRequestDto dto, CancellationToken ct)
    {
        if (await IsPublic(dto.SeasonID, ct))
            await outbox.EnqueuePlayerRemovedAsync(context, teams, dto, ct);
    }

    public async Task PlayerMovedAsync(TeamService teams, MovePlayerToTeamRequestDto dto, CancellationToken ct)
    {
        if (await IsPublic(dto.SeasonID, ct))
            await outbox.EnqueuePlayerMovedAsync(context, teams, dto, ct);
    }

    public async Task LockChangedAsync(TeamService teams, int seasonId, bool locked, CancellationToken ct)
    {
        if (await IsPublic(seasonId, ct))
            await outbox.EnqueueLockChangedAsync(context, teams, seasonId, locked, ct);
    }

    private Task<bool> IsPublic(int seasonId, CancellationToken ct)
        => context.Seasons.Where(Season.PublicAsOf(_time.GetUtcNow().UtcDateTime)).AnyAsync(s => s.SeasonID == seasonId, ct);
}
