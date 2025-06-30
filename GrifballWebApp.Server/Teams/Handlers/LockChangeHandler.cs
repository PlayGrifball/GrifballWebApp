using GrifballWebApp.Database;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public class LockChangeHandler(IHubContext<TeamsHub, ITeamsHubClient> hubContext) : SignalRHandler<LockChanged>(hubContext)
{
    public override async Task Handle(LockChanged request, CancellationToken cancellationToken)
    {
        if (request.Value)
        {
            await _hubContext.Clients.AllExcept(request.connectionID!).LockCaptains(request.seasonID);
        }
        else
        {
            await _hubContext.Clients.AllExcept(request.connectionID!).UnlockCaptains(request.seasonID);
        }
    }
}

public class LockChangeDiscordHandler(RestClient restClient, IOptions<DiscordOptions> options, IDbContextFactory<GrifballContext> contextFactory, IServiceScopeFactory factory)
    : DiscordHandler<LockChanged>(restClient, options, contextFactory)
{
    public override async Task HandleEvent(LockChanged request, CancellationToken cancellationToken)
    {
        using var scope = factory.CreateScope();
        var onDeck = scope.ServiceProvider.GetRequiredService<DiscordOnDeckMessages>();

        if (request.Value)
        {
            var msg = new MessageProperties()
                .WithContent($"Captains are now locked");
            await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);

            await onDeck.SendMessageAsync(request.seasonID, cancellationToken);
        }
        else
        {
            var msg = new MessageProperties()
                 .WithContent($"Captains are now unlocked. Draft is paused until adjustments are complete");
            await _restClient.SendMessageAsync(DraftChannelID, msg, cancellationToken: cancellationToken);
        }
    }
}

public record LockChanged(bool Value, int seasonID, string? connectionID) : INotification;