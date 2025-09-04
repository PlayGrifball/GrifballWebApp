using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using System.Security.Claims;

namespace GrifballWebApp.Server.Matchmaking;

public class DiscordSetGamertag
{
    private readonly GrifballContext _context;
    private readonly ILogger<DiscordSetGamertag> _logger;
    private readonly UserManager<Database.Models.User> _userManager;
    private readonly ISetGamertagService _setGamertagService;
    public DiscordSetGamertag(
        GrifballContext context,
        ILogger<DiscordSetGamertag> logger,
        UserManager<Database.Models.User> userManager,
        ISetGamertagService setGamertagService)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
        _setGamertagService = setGamertagService;
    }
    public async Task SetGamertag(IDiscordInteractionContext Context, string gamertag)
    {
        await Context.Interaction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        // Gesert the discord user
        var discordUser = await _context.DiscordUsers
            .Include(x => x.User)
            .Where(x => x.DiscordUserID == (long)Context.Interaction.User.Id)
            .FirstOrDefaultAsync();

        if (discordUser == null)
        {
            discordUser = new DiscordUser()
            {
                DiscordUserID = (long)Context.Interaction.User.Id,
                DiscordUsername = Context.Interaction.User.Username
            };
            _context.DiscordUsers.Add(discordUser);
            await _context.SaveChangesAsync();
        }

        // Create user account for discord user if they do not have one

        // First try to find a user setup already with a UserLogin for this account
        if (discordUser.User is null)
        {
            var acct = await _userManager.FindByLoginAsync("Discord", discordUser.DiscordUserID.ToString());
            if (acct is not null)
            {
                discordUser.User = acct;
                await _context.SaveChangesAsync();
            }
        }

        // Now if still null then we truly need to create a new user account
        if (discordUser.User is null)
        {
            discordUser.User = new()
            {
                UserName = discordUser.DiscordUsername, // Need to avoid conflicts
                //Email = "" // Do we have this?
            };
            var result = await _userManager.CreateAsync(discordUser.User);
            if (!result.Succeeded)
            {
                await Context.ModifyTempResponse("Failed to create user account");
                return;
            }
            // Setup extenal auth similar to result = await _userManager.AddLoginAsync(user, info); from IdentityController.cs
            await _userManager.AddLoginAsync(discordUser.User, new UserLoginInfo("Discord", discordUser.DiscordUserID.ToString(), "Discord"));

            // Add claims
            await _userManager.AddClaimsAsync(discordUser.User, new[]
            {
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", discordUser.DiscordUserID.ToString()),
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", discordUser.DiscordUsername),
            });

            // TODO: We need visibility on external auth in admin panel
        }

        var msg = await _setGamertagService.SetGamertag(discordUser.User.Id, gamertag);

        if (msg is not null)
        {
            await Context.ModifyTempResponse(msg);
        }
        else
        {
            await Context.ModifyTempResponse("Gamertag has been set");
        }
    }
}
