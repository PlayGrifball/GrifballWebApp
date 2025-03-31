using GrifballWebApp.Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class MovePlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<MovePlayerToTeamRequestDto>>(hubContext)
{
    public override async Task Handle(Notification<MovePlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.AllExcept(request.ConnectionId!).MovePlayerToTeam(request.Value);
    }
}

public class MovePlayerDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options, IDbContextFactory<GrifballContext> contextFactory)
    : DiscordHandler<Notification<MovePlayerToTeamRequestDto>>(restClient, options, contextFactory)
{
    public override async Task HandleEvent(Notification<MovePlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{await GetUsername(request.Value.PersonID)} has been moved from {await GetUsername(request.Value.PreviousCaptainID)}'s team to {await GetUsername(request.Value.NewCaptainID)}'s team");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
