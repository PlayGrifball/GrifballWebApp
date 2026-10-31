using GrifballWebApp.Database;
using GrifballWebApp.Server.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NetCord.Services;

namespace GrifballWebApp.Server.Teams.Handlers;

public abstract class DiscordHandler<T>(RestClient restClient, IOptions<DiscordOptions> options, IDbContextFactory<GrifballContext> contextFactory)
    : INotificationHandler<T> where T : INotification
{
    protected readonly RestClient _restClient = restClient;
    protected readonly IOptions<DiscordOptions> _options = options;
    protected readonly IDbContextFactory<GrifballContext> _contextFactory = contextFactory;
    protected ulong DraftChannelID => _options.Value.DraftChannel;

    public async Task Handle(T request, CancellationToken cancellationToken)
    {
        if (_options.Value.DisableGlobally)
            return;
        await HandleEvent(request, cancellationToken);
    }

    public abstract Task HandleEvent(T request, CancellationToken cancellationToken);

    protected async Task<string> GetUsername(int userID)
    {
        using var _context = await _contextFactory.CreateDbContextAsync();
        return await DiscordMention.ForUser(_context, userID);
    }
}
