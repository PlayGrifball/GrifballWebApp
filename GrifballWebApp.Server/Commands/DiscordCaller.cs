using DiscordInterface.Generated;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Discord;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Rest;

namespace GrifballWebApp.Server.Commands;

/// <summary>The app user who ran a command: the one linked to their Discord account, with their site roles.</summary>
public sealed record CallerUser(int UserID, string Gamertag, IReadOnlyList<string> Roles)
{
    /// <summary>Whether the user has one of <paramref name="roles"/>; with none given, anyone does.</summary>
    public bool IsInAnyRole(IReadOnlyCollection<string> roles) => roles.Count == 0 || roles.Any(Roles.Contains);
}

/// <summary>
/// Who may run a command that isn't open to everyone. The bot has no rules of its own: the caller is the app user linked
/// to their Discord account, held to the site's roles (the controllers' <c>[Authorize(Roles = ...)]</c>) and then to the
/// shared service's rules. A Discord user with no account or gamertag gets the Set Gamertag button, the queue's flow.
/// Commands open to everyone on the site (<c>[AllowAnonymous]</c>) don't come here: they need no account.
/// </summary>
public class DiscordCaller
{
    public const string NoGamertag = "You must set your gamertag first: use the button below, or /setgamertag.";

    /// <summary>The roles the site's controllers name.</summary>
    public static class Roles
    {
        public const string Player = "Player";
        public const string Commissioner = "Commissioner";
        public const string Sysadmin = "Sysadmin";

        public static readonly string[] Anyone = [];
        public static readonly string[] Staff = [Commissioner, Sysadmin];
        public static readonly string[] CommissionerOnly = [Commissioner];
        public static readonly string[] SysadminOnly = [Sysadmin];
        public static readonly string[] PlayerOrSysadmin = [Player, Sysadmin];
    }

    private readonly GrifballContext _context;

    public DiscordCaller(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The user linked to this Discord account, with their gamertag and roles; null when none is, or they have no gamertag.</summary>
    public async Task<CallerUser?> Find(ulong discordUserId, CancellationToken ct = default)
        => await Linked(discordUserId, ct) is { Gamertag: { } gamertag } user ? new CallerUser(user.Id, gamertag, user.RoleNames) : null;

    /// <summary>
    /// Who the agents' tools run as for this Discord user (the bot's chat): their linked site account with its roles, gamertag or
    /// not, or <see cref="ToolCaller.Anonymous"/> when none is linked.
    /// </summary>
    public async Task<ToolCaller> AsToolCaller(ulong discordUserId, CancellationToken ct = default)
        => await Linked(discordUserId, ct) is { } user ? new ToolCaller(user.Id, user.RoleNames) : ToolCaller.Anonymous;

    /// <summary>The site account linked to a Discord user: its ID, gamertag (if it has one) and roles.</summary>
    private sealed record LinkedUser(int Id, string? Gamertag, List<string> RoleNames);

    /// <summary>The site account linked to this Discord user, if any (<see cref="Find"/> and <see cref="AsToolCaller"/> both read it).</summary>
    private Task<LinkedUser?> Linked(ulong discordUserId, CancellationToken ct)
        => _context.Users
            .AsNoTracking()
            .Where(x => x.DiscordUserID == (long)discordUserId)
            .Select(x => new LinkedUser(x.Id, x.XboxUser != null ? x.XboxUser.Gamertag : null, x.UserRoles.Select(r => r.Role.Name!).ToList()))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// The caller, when they have an account with a gamertag and one of <paramref name="roles"/> (none: any account will do).
    /// Otherwise null, and they were told why in the reply. Call it after deferring: the lookup must not hold up Discord's
    /// 3 seconds, so the reply is deferred ephemerally first (only the caller sees why).
    /// </summary>
    public async Task<CallerUser?> Require(IDiscordInteractionContext context, IReadOnlyCollection<string> roles)
    {
        var user = await Find(context.Interaction.User.Id);
        if (user is null)
        {
            await context.Interaction.ModifyResponseAsync(x => x.WithContent(NoGamertag)
                .WithComponents([new ActionRowProperties([new ButtonProperties(DiscordButtonContants.SetGamertag, "Set Gamertag", ButtonStyle.Primary)])]));
            return null;
        }

        if (!user.IsInAnyRole(roles))
        {
            await context.Reply(MissingRole(roles));
            return null;
        }

        return user;
    }

    /// <summary>The reply to a caller without the role: the site would answer 403.</summary>
    public static string MissingRole(IReadOnlyCollection<string> roles) => $"Only a {string.Join(" or ", roles.Select(r => r.ToLowerInvariant()))} can do that.";
}
