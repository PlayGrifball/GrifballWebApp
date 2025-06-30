using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NetCord.Services;
using System.Security.Claims;

namespace GrifballWebApp.Server.Teams;

public class DiscordOnDeckMessages
{
    private readonly TeamService _teamService;
    private readonly RestClient _restClient;
    private readonly GrifballContext _context;
    private readonly ulong _draftChannel;

    public DiscordOnDeckMessages(TeamService teamService, RestClient restClient, GrifballContext grifballContext, IOptions<DiscordOptions> options)
    {
        _teamService = teamService;
        _restClient = restClient;
        _context = grifballContext;
        _draftChannel = options.Value.DraftChannel;
    }

    public async Task SendMessageAsync(int seasonId, CancellationToken cancellationToken = default)
    {
        var onDeck = await _teamService.OnDeck(seasonId, cancellationToken);

        if (onDeck is null)
        {
            var msg = new MessageProperties()
                .WithContent("Failed to find team");
            await _restClient.SendMessageAsync(_draftChannel, msg, cancellationToken: cancellationToken);
            return;
        }

        var pool = (await _teamService.GetPlayerPool(seasonId, cancellationToken)).OrderBy(x => x.Name);

        if (pool.Any() is false)
        {
            var msg = new MessageProperties()
                .WithContent("Draft has been completed");
            await _restClient.SendMessageAsync(_draftChannel, msg, cancellationToken: cancellationToken);
            return;
        }

        // Split pool into groups of 25
        var poolGroups = pool
            .Select((player, index) => new { player, index })
            .GroupBy(x => x.index / 25)
            .Select(g => g.Select(x => x.player).ToList())
            .ToList();

        var poolMenus = poolGroups.Select((smallerPool, index) =>
        {
            var startName = smallerPool.First().Name;
            var startNameShort = startName.Substring(0, Math.Min(4, startName.Length));

            var lastName = smallerPool.Last().Name;
            var lastNameShort = lastName.Substring(0, Math.Min(4, lastName.Length));

            var poolMenu = new StringMenuProperties(DiscordStringMenuContants.DraftPickWithParams(seasonId, onDeck.Captain.UserID, index))
            {
                Placeholder = $"Make pick - {startNameShort} - {lastNameShort}",
                CustomId = DiscordStringMenuContants.DraftPickWithParams(seasonId, onDeck.Captain.UserID, index),
                Options = [.. smallerPool.Select(x => new StringMenuSelectOptionProperties(x.Name, x.PersonID.ToString()))],
            };
            return poolMenu;
        });

        // Split pool into groups of 5
        var menuGroups = poolMenus
            .Select((player, index) => new { player, index })
            .GroupBy(x => x.index / 5)
            .Select(g => g.Select(x => x.player).ToList())
            //.Select(x => new MessageProperties().WithComponents(x))
            .ToList();

        var first = menuGroups.First();
        var msg2 = new MessageProperties()
            .WithComponents(first)
            .WithContent($"{await GetUsername(onDeck.Captain.UserID)} is on deck");

        var extraMessages = menuGroups.Skip(1).Select(x => new MessageProperties().WithComponents(x)).ToList();
        await _restClient.SendMessageAsync(_draftChannel, msg2, cancellationToken: cancellationToken);
        foreach (var extraMessage in extraMessages)
        {
            await _restClient.SendMessageAsync(_draftChannel, extraMessage, cancellationToken: cancellationToken);
        }
    }

    // TODO: Refactor into class
    private async Task<string> GetUsername(int userID)
    {
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
