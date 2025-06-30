using GrifballWebApp.Database;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class RemoveCaptainHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<Notification<RemoveCaptainDto>>(hubContext)
{
    public override async Task Handle(Notification<RemoveCaptainDto> request, CancellationToken cancellationToken)
    {
        await _hubContext.Clients.AllExcept(request.ConnectionId!).RemoveCaptain(request.Value);
    }
}

public class RemoveCaptainDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options, IDbContextFactory<GrifballContext> context)
    : DiscordHandler<Notification<RemoveCaptainDto>>(restClient, options, context)
{
    public override async Task HandleEvent(Notification<RemoveCaptainDto> request, CancellationToken cancellationToken)
    {
        //var msg = new MessageProperties()
        //    .WithContent($"{await GetUsername(request.Value.PersonID)} is no longer a captain in the draft");
        //await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
    }
}
