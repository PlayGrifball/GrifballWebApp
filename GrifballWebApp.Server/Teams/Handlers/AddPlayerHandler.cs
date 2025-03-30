using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddPlayerHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<AddPlayerToTeamRequestDto>>(hubContext)
{
    public override async Task Handle(Notification<AddPlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.AllExcept(request.ConnectionId!).AddPlayerToTeam(request.Value);
    }
}

public class AddPlayerDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options) : DiscordHandler<Notification<AddPlayerToTeamRequestDto>>(restClient, options)
{
    public override async Task HandleEvent(Notification<AddPlayerToTeamRequestDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{request.Value.PersonID} is now on {request.Value.CaptainID}'s team");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
