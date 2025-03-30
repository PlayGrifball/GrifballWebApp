using MediatR;
using Microsoft.Extensions.Options;
using NetCord.Rest;

namespace GrifballWebApp.Server.Teams.Handlers;

public abstract class DiscordHandler<T>(RestClient restClient, IOptions<DiscordOptions> options) : INotificationHandler<T> where T : INotification
{
    protected readonly RestClient _restClient = restClient;
    protected readonly IOptions<DiscordOptions> _options = options;
    protected ulong DraftChannelID => _options.Value.DraftChannel;
    public async Task Handle(T request, CancellationToken cancellationToken)
    {
        if (_options.Value.DisableGlobally)
            return;
        await HandleEvent(request, cancellationToken);
    }
    public abstract Task HandleEvent(T request, CancellationToken cancellationToken);
}
