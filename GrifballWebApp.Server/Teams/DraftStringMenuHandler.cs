using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NetCord.Services;

namespace GrifballWebApp.Server.Teams;

/// <summary>
/// The draft pick string menu. <see cref="StringMenuInteractions"/> is the NetCord module; it only wraps its context and calls this,
/// so it takes our <see cref="IDiscordStringMenuInteractionContext"/> and tests can pass any implementation.
/// </summary>
public class DraftStringMenuHandler
{
    private readonly GrifballContext _context;
    private readonly TeamService _teamService;
    private readonly ILogger<DraftStringMenuHandler> _logger;

    public DraftStringMenuHandler(GrifballContext context, TeamService teamService, ILogger<DraftStringMenuHandler> logger)
    {
        _context = context;
        _teamService = teamService;
        _logger = logger;
    }

    private async Task<Database.Models.User?> UserGuard(IDiscordStringMenuInteractionContext discordContext)
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)discordContext.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await discordContext.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    public async Task DraftPick(IDiscordStringMenuInteractionContext discordContext, int seasonId, int captainId)
    {
        var user = await UserGuard(discordContext);
        if (user is null) return;

        await discordContext.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var value = discordContext.SelectedValues.Select(int.Parse).First();

        var dto = new AddPlayerToTeamRequestDto
        {
            SeasonID = seasonId,
            CaptainID = captainId,
            PersonID = value,
        };
        try
        {
            await _teamService.AddPlayerToTeam(dto, user.Id);
            var id = discordContext.User.Id;
            var username = new UserId(id).ToString();
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent($"{username}, " + "your pick was made successfully"));
        }
        catch (TeamServiceException ex)
        {
            var id = discordContext.User.Id;
            var username = new UserId(id).ToString();
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent($"Hey {username}, " + ex.Message));
        }
        catch (Exception ex)
        {
            var id = discordContext.User.Id;
            var username = new UserId(id).ToString();
            _logger.LogError(ex, "DraftPick failed for Discord user {DiscordUserId}, season {SeasonId}, captain {CaptainId}", id, seasonId, captainId);
            await discordContext.Interaction.ModifyResponseAsync(x => x.WithContent($"Hey {username}, something bad happened. Please try again and contact sysadmin if problem persists"));
        }
    }
}
