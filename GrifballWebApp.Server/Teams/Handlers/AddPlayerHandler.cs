using Microsoft.AspNetCore.SignalR;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddPlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<AddPlayerToTeamRequestDto>>(hubContext)
{
    public override async Task Handle(Notification<AddPlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await Others(request.ConnectionId).AddPlayerToTeam(request.Value);
    }
}
