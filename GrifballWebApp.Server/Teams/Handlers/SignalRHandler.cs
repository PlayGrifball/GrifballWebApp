using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Seasons;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Teams.Handlers;

public abstract class SignalRHandler<T>(IHubContext<TeamsHub, ITeamsHubClient> hubContext, IServiceScopeFactory scopes) : INotificationHandler<T>
    where T : INotification
{
    protected readonly IHubContext<TeamsHub, ITeamsHubClient> _hubContext = hubContext;

    /// <summary>
    /// Everyone except the connection that made the change (it already shows it). Without a connection id (no live SignalR
    /// connection, e.g. a Discord action), everyone. A change to a season not announced yet goes to staff's connections alone
    /// (<see cref="TeamsHub.StaffGroup"/>): the public's draft pages mustn't learn of it (#435). The season is looked up in a
    /// scope of the handler's own: TeamService publishes without waiting, so the caller's may be gone by now.
    /// </summary>
    protected async Task<ITeamsHubClient> Others(string? connectionId, int seasonID)
    {
        var clients = _hubContext.Clients;
        await using var scope = scopes.CreateAsyncScope();
        if (!await scope.ServiceProvider.GetRequiredService<SeasonVisibility>().CanSeeSeason(seasonID, SeasonViewer.Public))
            return string.IsNullOrEmpty(connectionId) ? clients.Group(TeamsHub.StaffGroup) : clients.GroupExcept(TeamsHub.StaffGroup, connectionId);
        return string.IsNullOrEmpty(connectionId) ? clients.All : clients.AllExcept(connectionId);
    }

    public abstract Task Handle(T notification, CancellationToken cancellationToken);
}
