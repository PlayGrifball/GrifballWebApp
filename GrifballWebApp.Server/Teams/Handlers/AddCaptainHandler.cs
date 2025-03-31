using GrifballWebApp.Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
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

public class AddCaptainDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options, IDbContextFactory<GrifballContext> context)
    : DiscordHandler<Notification<CaptainAddedDto>>(restClient, options, context)
{
    public override async Task HandleEvent(Notification<CaptainAddedDto> request, CancellationToken cancellationToken)
    {
        var msg = new MessageProperties()
            .WithContent($"{await GetUsername(request.Value.PersonID)} has been added as captain {request.Value.OrderNumber} in the draft");
        await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
