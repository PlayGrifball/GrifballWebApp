using GrifballWebApp.Database;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NetCord.Services;
using System.Security.Claims;

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
        var claimValue = await _context.UserClaims
            .Where(x => x.UserId == userID && x.ClaimType == ClaimTypes.NameIdentifier)
            .Select(x => x.ClaimValue)
            .FirstOrDefaultAsync();

        if (claimValue is not null)
        {
            if (ulong.TryParse(claimValue, out var discordUserID))
            {
                return new UserId(discordUserID).ToString();
            }
        }

        var name = await _context.Users
            .Where(x => x.Id == userID)
            .Select(x => x.DisplayName)
            .FirstOrDefaultAsync();
        return name ?? userID.ToString();
    }
}
