using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public abstract class SignalRHandler<T>(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : INotificationHandler<T> where T : INotification
{
    protected readonly IHubContext<TeamsHub, ITeamsHubClient> _hubContext = hubContext;

    public abstract Task Handle(T request, CancellationToken cancellationToken);
}
