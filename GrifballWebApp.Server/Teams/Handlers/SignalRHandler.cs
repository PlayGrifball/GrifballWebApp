using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Teams.Handlers;

public abstract class SignalRHandler<T>(IHubContext<TeamsHub, ITeamsHubClient> hubContext, SeasonVisibility visibility) : INotificationHandler<T>
    where T : INotification
{
    protected readonly IHubContext<TeamsHub, ITeamsHubClient> _hubContext = hubContext;

    /// <summary>
    /// Everyone except the connection that made the change (it already shows it). Without a connection id (no live SignalR
    /// connection, e.g. a Discord action), everyone. A change to a season not announced yet goes to staff's connections alone
    /// (<see cref="TeamsHub.StaffGroup"/>): the public's draft pages mustn't learn of it (#435).
    /// </summary>
    protected async Task<ITeamsHubClient> Others(string? connectionId, int seasonID)
    {
        var clients = _hubContext.Clients;
        if (!await visibility.CanSeeSeason(seasonID, SeasonViewer.Public))
            return string.IsNullOrEmpty(connectionId) ? clients.Group(TeamsHub.StaffGroup) : clients.GroupExcept(TeamsHub.StaffGroup, connectionId);
        return string.IsNullOrEmpty(connectionId) ? clients.All : clients.AllExcept(connectionId);
    }

    public abstract Task Handle(T notification, CancellationToken cancellationToken);
}
