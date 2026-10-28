using GrifballWebApp.Server.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public abstract class SignalRHandler<T>(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : INotificationHandler<T> where T : INotification
{
    protected readonly IHubContext<TeamsHub, ITeamsHubClient> _hubContext = hubContext;

    /// <summary>
    /// Everyone except the connection that made the change (it already shows it). Without a
    /// connection id (no live SignalR connection, e.g. a Discord action), everyone.
    /// </summary>
    protected ITeamsHubClient Others(string? connectionId) =>
        string.IsNullOrEmpty(connectionId) ? _hubContext.Clients.All : _hubContext.Clients.AllExcept(connectionId);

    public abstract Task Handle(T request, CancellationToken cancellationToken);
}
