using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Teams;

public class StringMenuInteractions : ComponentInteractionModule<StringMenuInteractionContext>
{
    private readonly GrifballContext _context;
    private readonly TeamService _teamService;

    public StringMenuInteractions(GrifballContext context, TeamService teamService)
    {
        _context = context;
        _teamService = teamService;
    }

    private async Task<Database.Models.User?> UserGuard()
    {
        var user = await _context.Users
                    .Include(x => x.XboxUser)
                    .Where(x => x.DiscordUserID == (long)Context.User.Id)
                    .FirstOrDefaultAsync();
        if (user is null || user.XboxUser is null)
        {
            await Context.TempResponse("You must set your gamertag first");
            return null;
        }

        return user;
    }

    [ComponentInteraction(DiscordStringMenuContants.DraftPick)]
    public async Task DraftPick(int seasonId, int captainId, int _)
    {
        var user = await UserGuard();
        if (user is null) return;

        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage());

        var value = Context.SelectedValues.Select(int.Parse).First();

        var dto = new AddPlayerToTeamRequestDto
        {
            SeasonID = seasonId,
            CaptainID = captainId,
            PersonID = value,
        };
        try
        {
            await _teamService.AddPlayerToTeam(dto, user.Id);
            var id = Context.User.Id;
            var username = new UserId(id).ToString();
            await Context.Interaction.ModifyResponseAsync(x => x.WithContent($"{username}, " + "your pick was made successfully"));
        }
        catch (TeamServiceException ex)
        {
            var id = Context.User.Id;
            var username = new UserId(id).ToString();
            await Context.Interaction.ModifyResponseAsync(x => x.WithContent($"Hey {username}, " + ex.Message));
        }
        catch (Exception)
        {
            var id = Context.User.Id;
            var username = new UserId(id).ToString();
            await Context.Interaction.ModifyResponseAsync(x => x.WithContent($"Hey {username}, something bad happened. Please try again and contact sysadmin if problem persists"));
        }
    }
}
