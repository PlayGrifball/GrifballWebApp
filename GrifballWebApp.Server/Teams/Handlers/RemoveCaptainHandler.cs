using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class RemoveCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<RemoveCaptainDto>>(hubContext)
{
    public override async Task Handle(Notification<RemoveCaptainDto> request, CancellationToken cancellationToken)
    {
        await Others(request.ConnectionId).RemoveCaptain(request.Value);
    }
}
