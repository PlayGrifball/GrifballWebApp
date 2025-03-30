using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class AddCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<CaptainAddedDto>>(hubContext)
{
    public override async Task Handle(Notification<CaptainAddedDto> request, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.AllExcept(request.ConnectionId!).AddCaptain(request.Value);
    }
}

public class AddCaptainDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options) : DiscordHandler<Notification<CaptainAddedDto>>(restClient, options)
{
    public override async Task HandleEvent(Notification<CaptainAddedDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{request.Value.PersonID} is now {request.Value.OrderNumber} in the draft");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
