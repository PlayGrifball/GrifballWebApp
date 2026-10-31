using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class ResortCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<CaptainPlacementDto>>(hubContext)
{
    public override async Task Handle(Notification<CaptainPlacementDto> request, CancellationToken cancellationToken)
    {
        await Others(request.ConnectionId).ResortCaptain(request.Value);
    }
}

