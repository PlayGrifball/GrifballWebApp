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

public class DiscordSetGamertag : IDiscordGamertagSetter
{
    /// <summary>The reply when the site account to link is linked to another Discord account already: nothing is made or saved.</summary>
    public const string LinkedToOtherDiscord = "Your site account is linked to a different Discord account. Unlink it on the site, or ask a commissioner.";

    /// <summary>What a sysadmin is told for the same, about someone else's account.</summary>
    public const string TheirsLinkedToOtherDiscord = "That user's site account is linked to a different Discord account. Unlink it on the site first.";

    /// <summary>What a sysadmin is told when the Discord user has no account yet and no Discord username was given to make one with.</summary>
    public const string NeedsDiscordUsername = "No site account is linked to that Discord user yet, and making one needs their Discord username: give it too.";

    private readonly GrifballContext _context;
    private readonly UserManager<Domain.Players.User> _userManager;
    private readonly ISetGamertagService _setGamertagService;
    private readonly IUserMergeService _merges;
    public DiscordSetGamertag(
        GrifballContext context,
        UserManager<Domain.Players.User> userManager,
        ISetGamertagService setGamertagService,
        IUserMergeService merges)
    {
        _context = context;
        _userManager = userManager;
        _setGamertagService = setGamertagService;
        _merges = merges;
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

        var msg = await SetFor(Context.Interaction.User.Id, Context.Interaction.User.Username, gamertag, own: true);

        await Context.ModifyTempResponse(msg ?? "Gamertag has been set");
    }

    /// <inheritdoc/>
    public Task<string?> SetGamertagForDiscordUser(ulong discordUserId, string? discordUsername, string gamertag, CancellationToken ct = default)
        => SetFor(discordUserId, discordUsername, gamertag, own: false);

    /// <summary>
    /// The whole flow for a Discord user, as /setgamertag does it for the one who types it and a sysadmin does it for another: their
    /// account, the one signed in on the site with their Discord, or a new one (a placeholder linked to them is merged into it), then the
    /// player's rules for the gamertag. The refusal, null when set. <paramref name="own"/> says whose account the words are about.
    /// </summary>
    private async Task<string?> SetFor(ulong discordUserId, string? discordUsername, string gamertag, bool own)
    {
        var discordUser = await GetsertDiscordUser(discordUserId, discordUsername);
        if (discordUser is null)
            return NeedsDiscordUsername;

        // Create user account for discord user if they do not have one

        // First try to find a user setup already with a UserLogin for this account
        if (discordUser.User is null && await LinkSignedInAccount(discordUser, own) is { } linkRefusal)
            return linkRefusal;

        // A placeholder the archive's players lists linked to this Discord account is not one to play as: the person gets
        // their own account below, and the placeholder is merged into it (audited), its Discord link with it
        Domain.Players.User? placeholder = discordUser.User is { IsDummyUser: true } linked ? linked : null;

        // Now if still null then we truly need to create a new user account
        if ((discordUser.User is null || placeholder is not null) && await MakeAccount(discordUser, placeholder, own) is { } accountRefusal)
            return accountRefusal;

        return await _setGamertagService.SetGamertag(placeholder is null ? discordUser.User!.Id : (await _context.Users.Where(u => u.DiscordUserID == discordUser.DiscordUserID).Select(u => u.Id).FirstAsync()), gamertag);
    }

    /// <summary>The Discord user, added when the app hasn't seen them. Null when it hasn't and there is no username to add them with.</summary>
    private async Task<Domain.Players.DiscordUser?> GetsertDiscordUser(ulong discordUserId, string? discordUsername)
    {
        var discordUser = await _context.DiscordUsers
            .Include(x => x.User)
            .Where(x => x.DiscordUserID == (long)discordUserId)
            .FirstOrDefaultAsync();
        if (discordUser is not null)
            return discordUser;

        // Signed in on the site with this Discord account but never seen by the bot: their account names them
        var username = string.IsNullOrWhiteSpace(discordUsername)
            ? (await _userManager.FindByLoginAsync("Discord", discordUserId.ToString()))?.UserName
            : discordUsername;
        if (string.IsNullOrWhiteSpace(username))
            return null;

        discordUser = Domain.Players.DiscordUser.FromDiscord((long)discordUserId, username);
        _context.DiscordUsers.Add(discordUser);
        await _context.SaveChangesAsync();
        return discordUser;
    }

    /// <summary>The account signed in on the site with this Discord account, linked to it. The refusal when it is linked to another.</summary>
    private async Task<string?> LinkSignedInAccount(Domain.Players.DiscordUser discordUser, bool own)
    {
        var acct = await _userManager.FindByLoginAsync("Discord", discordUser.DiscordUserID.ToString());
        if (acct is not null)
        {
            if (LinkOrRefuse(acct, discordUser, own) is { } refusal)
                return refusal;
            await _context.SaveChangesAsync();
        }
        return null;
    }

    /// <summary>
    /// The Discord user's own account: the one signed in on the site with it, or a new one; a placeholder they were linked
    /// to is merged into it. The refusal when it can't be made or joined.
    /// </summary>
    private async Task<string?> MakeAccount(Domain.Players.DiscordUser discordUser, Domain.Players.User? placeholder, bool own)
    {
        var newUser = Domain.Players.User.Register(discordUser.DiscordUsername); // Need to avoid conflicts
        if (placeholder is null && LinkOrRefuse(newUser, discordUser, own) is { } refusal)
            return refusal;
        // Signed in on the site with this Discord account already: that account is theirs
        var signedIn = placeholder is null ? null : await _userManager.FindByLoginAsync("Discord", discordUser.DiscordUserID.ToString());
        if (signedIn is not null)
            newUser = signedIn;
        else if (await CreateAccount(newUser, discordUser) is { } failure)
            return failure;

        if (placeholder is not null)
        {
            try
            {
                await _merges.Merge(newUser.Id, placeholder.Id, MergeOptions.For(MergeTrigger.DiscordMatch, own ? "Discord set gamertag" : "Discord set gamertag by a sysadmin", newUser.Id));
            }
            catch (MergeConflictException e)
            {
                return (own ? "Your account was made, but your past records could not be joined to it: " : "Their account was made, but their past records could not be joined to it: ")
                    + e.Message + (own ? " Contact a commissioner." : " Merge them on the site.");
            }
        }

        // TODO: We need visibility on external auth in admin panel
        return null;
    }

    /// <summary>A new account for the Discord user, a Player, signed in with their Discord. The refusal when it can't be made.</summary>
    private async Task<string?> CreateAccount(Domain.Players.User newUser, Domain.Players.DiscordUser discordUser)
    {
        var result = await _userManager.CreateAsync(newUser);
        if (!result.Succeeded)
            return "Failed to create user account";
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
        return null;
    }

    /// <summary>Links <paramref name="user"/> to the Discord account (<see cref="Domain.Players.User.LinkDiscord(Domain.Players.DiscordUser)"/>); when it is
    /// linked to another already, the refusal (<see cref="LinkedToOtherDiscord"/>).</summary>
    private static string? LinkOrRefuse(Domain.Players.User user, Domain.Players.DiscordUser discordUser, bool own)
        => user.LinkDiscord(discordUser) is DiscordLinking.LinkedToOther ? (own ? LinkedToOtherDiscord : TheirsLinkedToOtherDiscord) : null;
}
