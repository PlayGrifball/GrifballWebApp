using DiscordInterface.Generated;
using DiscordInterfaces;
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
    private readonly IUserMergeService _merges;
    public DiscordSetGamertag(
        GrifballContext context,
        ILogger<DiscordSetGamertag> logger,
        UserManager<Database.Models.User> userManager,
        ISetGamertagService setGamertagService,
        IUserMergeService? merges = null)
    {
        _context = context;
        _logger = logger;
        _userManager = userManager;
        _setGamertagService = setGamertagService;
        _merges = merges ?? new UserMergeService(context);
    }

    /// <summary>The Set Gamertag modal (<see cref="ModalInteractions"/>): reads the gamertag text input, which arrives wrapped in a Label.</summary>
    public async Task SetGamertag(IDiscordModalInteractionContext context)
    {
        var gamertag = ((TextInput)((Label)context.Components[0]).Component).Value;
        await SetGamertag(context, gamertag);
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
            discordUser = Database.Models.DiscordUser.FromDiscord((long)Context.Interaction.User.Id, Context.Interaction.User.Username);
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
                acct.LinkDiscord(discordUser);
                await _context.SaveChangesAsync();
            }
        }

        // A placeholder the archive's players lists linked to this Discord account is not one to play as: the person gets
        // their own account below, and the placeholder is merged into it (audited), its Discord link with it
        Database.Models.User? placeholder = discordUser.User is { IsDummyUser: true } linked ? linked : null;

        // Now if still null then we truly need to create a new user account
        if (discordUser.User is null || placeholder is not null)
        {
            var newUser = Database.Models.User.Register(discordUser.DiscordUsername); // Need to avoid conflicts
            if (placeholder is null)
                newUser.LinkDiscord(discordUser);
            // Signed in on the site with this Discord account already: that account is theirs
            var signedIn = placeholder is null ? null : await _userManager.FindByLoginAsync("Discord", discordUser.DiscordUserID.ToString());
            if (signedIn is not null)
                newUser = signedIn;
            else
            {
                var result = await _userManager.CreateAsync(newUser);
                if (!result.Succeeded)
                {
                    await Context.ModifyTempResponse("Failed to create user account");
                    return;
                }
                // Every account plays, as with sign-up: without the role the website's signup form and profile are forbidden.
                await _userManager.AddToRoleAsync(newUser, "Player");

                // Setup extenal auth similar to result = await _userManager.AddLoginAsync(user, info); from IdentityController.cs
                await _userManager.AddLoginAsync(newUser, new UserLoginInfo("Discord", discordUser.DiscordUserID.ToString(), "Discord"));

                // Add claims
                await _userManager.AddClaimsAsync(newUser, new[]
                {
                    new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", discordUser.DiscordUserID.ToString()),
                    new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", discordUser.DiscordUsername),
                });
            }

            if (placeholder is not null)
            {
                try
                {
                    await _merges.Merge(newUser.Id, placeholder.Id, MergeOptions.For(MergeTrigger.DiscordMatch, "Discord set gamertag", newUser.Id));
                }
                catch (MergeConflictException e)
                {
                    await Context.ModifyTempResponse("Your account was made, but your past records could not be joined to it: " + e.Message + " Contact a commissioner.");
                    return;
                }
            }

            // TODO: We need visibility on external auth in admin panel
        }

        var msg = await _setGamertagService.SetGamertag(placeholder is null ? discordUser.User!.Id : (await _context.Users.Where(u => u.DiscordUserID == discordUser.DiscordUserID).Select(u => u.Id).FirstAsync()), gamertag);

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
