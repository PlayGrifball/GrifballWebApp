using GrifballWebApp.Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class RemovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<RemovePlayerFromTeamRequestDto>>(hubContext)
{
    public override async Task Handle(Notification<RemovePlayerFromTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.AllExcept(request.ConnectionId!).RemovePlayerFromTeam(request.Value);
    }
}

public class RemovePlayerDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options, IDbContextFactory<GrifballContext> context)
    : DiscordHandler<Notification<RemovePlayerFromTeamRequestDto>>(restClient, options, context)
{
    public override async Task HandleEvent(Notification<RemovePlayerFromTeamRequestDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{await GetUsername(request.Value.PersonID)} has returned to the player pool");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
