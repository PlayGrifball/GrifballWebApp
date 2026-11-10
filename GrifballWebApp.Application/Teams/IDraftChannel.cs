namespace GrifballWebApp.Application.Teams;

/// <summary>
/// The draft channel's messages about a draft change, queued on the outbox in the change's transaction, with who is on
/// deck next (Infrastructure's OutboxDraftChannel: the Discord messages are its to word).
/// </summary>
public interface IDraftChannel
{
    Task PlayerAddedAsync(TeamService teams, AddPlayerToTeamRequestDto dto, CancellationToken ct);
    Task PlayerRemovedAsync(TeamService teams, RemovePlayerFromTeamRequestDto dto, CancellationToken ct);
    Task PlayerMovedAsync(TeamService teams, MovePlayerToTeamRequestDto dto, CancellationToken ct);
    Task LockChangedAsync(TeamService teams, int seasonId, bool locked, CancellationToken ct);
}
