using GrifballWebApp.Application.Teams;
using GrifballWebApp.Application.Teams.Handlers;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext, IServiceScopeFactory scopes) : SignalRHandler<Notification<CaptainAddedDto>>(hubContext, scopes)
{
    public override async Task Handle(Notification<CaptainAddedDto> request, CancellationToken cancellationToken)
    {
        await (await Others(request.ConnectionId, request.Value.SeasonID)).AddCaptain(request.Value);
    }
}
