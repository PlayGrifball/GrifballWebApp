using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class ResortCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<CaptainPlacementDto>>(hubContext)
{
    public override async Task Handle(Notification<CaptainPlacementDto> request, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.AllExcept(request.ConnectionId!).ResortCaptain(request.Value);
    }
}

public class ResortCaptainDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options) : DiscordHandler<Notification<CaptainPlacementDto>>(restClient, options)
{
    public override async Task HandleEvent(Notification<CaptainPlacementDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{request.Value.PersonID} is now {request.Value.OrderNumber} in the draft");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}

