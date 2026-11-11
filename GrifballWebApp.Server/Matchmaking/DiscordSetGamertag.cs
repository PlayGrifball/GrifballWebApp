using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;
using System.Security.Claims;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.Matchmaking;

public class DiscordSetGamertag
{
    /// <summary>The reply when the site account to link is linked to another Discord account already: nothing is made or saved.</summary>
    public const string LinkedToOtherDiscord = "Your site account is linked to a different Discord account. Unlink it on the site, or ask a commissioner.";

    private readonly GrifballContext _context;
    private readonly UserManager<Domain.Players.User> _userManager;
    private readonly ISetGamertagService _setGamertagService;
    private readonly IUserMergeService _merges;
    public DiscordSetGamertag(
        GrifballContext context,
        UserManager<Domain.Players.User> userManager,
        ISetGamertagService setGamertagService,
        IUserMergeService? merges = null)
    {
        _context = context;
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

        var discordUser = await GetsertDiscordUser(Context);

        // Create user account for discord user if they do not have one

        // First try to find a user setup already with a UserLogin for this account
        if (discordUser.User is null && !await LinkSignedInAccount(Context, discordUser))
            return;

        // A placeholder the archive's players lists linked to this Discord account is not one to play as: the person gets
        // their own account below, and the placeholder is merged into it (audited), its Discord link with it
        Domain.Players.User? placeholder = discordUser.User is { IsDummyUser: true } linked ? linked : null;

        // Now if still null then we truly need to create a new user account
        if ((discordUser.User is null || placeholder is not null) && !await MakeAccount(Context, discordUser, placeholder))
            return;

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

    /// <summary>The interaction's Discord user, added when the app hasn't seen them.</summary>
    private async Task<Domain.Players.DiscordUser> GetsertDiscordUser(IDiscordInteractionContext Context)
    {
        var discordUser = await _context.DiscordUsers
            .Include(x => x.User)
            .Where(x => x.DiscordUserID == (long)Context.Interaction.User.Id)
            .FirstOrDefaultAsync();

        if (discordUser == null)
        {
            discordUser = Domain.Players.DiscordUser.FromDiscord((long)Context.Interaction.User.Id, Context.Interaction.User.Username);
            _context.DiscordUsers.Add(discordUser);
            await _context.SaveChangesAsync();
        }
        return discordUser;
    }

    /// <summary>The account signed in on the site with this Discord account, linked to it. False (replied to) when it is linked to another.</summary>
    private async Task<bool> LinkSignedInAccount(IDiscordInteractionContext Context, Domain.Players.DiscordUser discordUser)
    {
        var acct = await _userManager.FindByLoginAsync("Discord", discordUser.DiscordUserID.ToString());
        if (acct is not null)
        {
            if (!await LinkOrRefuse(Context, acct, discordUser))
                return false;
            await _context.SaveChangesAsync();
        }
        return true;
    }

    /// <summary>
    /// The Discord user's own account: the one signed in on the site with it, or a new one; a placeholder they were linked
    /// to is merged into it. False (replied to) when it can't be made or joined.
    /// </summary>
    private async Task<bool> MakeAccount(IDiscordInteractionContext Context, Domain.Players.DiscordUser discordUser, Domain.Players.User? placeholder)
    {
        var newUser = Domain.Players.User.Register(discordUser.DiscordUsername); // Need to avoid conflicts
        if (placeholder is null && !await LinkOrRefuse(Context, newUser, discordUser)) return false;
        // Signed in on the site with this Discord account already: that account is theirs
        var signedIn = placeholder is null ? null : await _userManager.FindByLoginAsync("Discord", discordUser.DiscordUserID.ToString());
        if (signedIn is not null)
            newUser = signedIn;
        else if (!await CreateAccount(Context, newUser, discordUser))
            return false;

        if (placeholder is not null)
        {
            try
            {
                await _merges.Merge(newUser.Id, placeholder.Id, MergeOptions.For(MergeTrigger.DiscordMatch, "Discord set gamertag", newUser.Id));
            }
            catch (MergeConflictException e)
            {
                await Context.ModifyTempResponse("Your account was made, but your past records could not be joined to it: " + e.Message + " Contact a commissioner.");
                return false;
            }
        }

        // TODO: We need visibility on external auth in admin panel
        return true;
    }

    /// <summary>A new account for the Discord user, a Player, signed in with their Discord. False (replied to) when it can't be made.</summary>
    private async Task<bool> CreateAccount(IDiscordInteractionContext Context, Domain.Players.User newUser, Domain.Players.DiscordUser discordUser)
    {
        var result = await _userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            await Context.ModifyTempResponse("Failed to create user account");
            return false;
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
        return true;
    }

    /// <summary>Links <paramref name="user"/> to the Discord account (<see cref="Domain.Players.User.LinkDiscord(Domain.Players.DiscordUser)"/>); when it is
    /// linked to another already, replies <see cref="LinkedToOtherDiscord"/> and returns false.</summary>
    private static async Task<bool> LinkOrRefuse(IDiscordInteractionContext context, Domain.Players.User user, Domain.Players.DiscordUser discordUser)
    {
        if (user.LinkDiscord(discordUser) is not DiscordLinking.LinkedToOther)
            return true;
        await context.ModifyTempResponse(LinkedToOtherDiscord);
        return false;
    }
}
