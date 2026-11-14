using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using GrifballWebApp.Application.Seasons;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, SeasonVisibility visibility) : SignalRHandler<Notification<CaptainAddedDto>>(hubContext, visibility)
{
    public override async Task Handle(Notification<CaptainAddedDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).AddCaptain(request.Value);
    }
}
