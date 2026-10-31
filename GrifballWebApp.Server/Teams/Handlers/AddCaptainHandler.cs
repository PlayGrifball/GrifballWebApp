using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<CaptainAddedDto>>(hubContext)
{
    public override async Task Handle(Notification<CaptainAddedDto> request, CancellationToken cancellationToken)
    {
        await Others(request.ConnectionId).AddCaptain(request.Value);
    }
}
