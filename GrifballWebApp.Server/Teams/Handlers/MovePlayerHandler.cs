using Microsoft.AspNetCore.SignalR;
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

public class MovePlayerDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options) : DiscordHandler<Notification<MovePlayerToTeamRequestDto>>(restClient, options)
{
    public override async Task HandleEvent(Notification<MovePlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{request.Value.PersonID} is has been moved to {request.Value.NewCaptainID}'s team");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
