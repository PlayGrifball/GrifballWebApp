namespace GrifballWebApp.Application.Identity;

/// <summary>
/// The site's roles, as the controllers' <c>[Authorize(Roles = ...)]</c> name them, and the role check and refusal the Discord
/// commands and the agents' tools share.
/// </summary>
public static class SiteRoles
{
    public const string Player = "Player";
    public const string Commissioner = "Commissioner";
    public const string Sysadmin = "Sysadmin";

    /// <summary>Who may pull games from Halo for a page (a season match's): any player, as staff may.</summary>
    public const string Pullers = Player + "," + Commissioner + "," + Sysadmin;

    /// <summary>The league's staff: who run seasons and see one not announced yet (#435).</summary>
    public static readonly string[] Staff = [Commissioner, Sysadmin];

    /// <summary>Whether <paramref name="held"/> has one of <paramref name="roles"/>; with none named, anyone does.</summary>
    public static bool IsInAnyRole(IReadOnlyCollection<string> held, IReadOnlyCollection<string> roles) => roles.Count == 0 || roles.Any(held.Contains);

    /// <summary>The refusal for a caller without the role: the site would answer 403.</summary>
    public static string MissingRole(IReadOnlyCollection<string> roles) => $"Only a {string.Join(" or ", roles.Select(r => r.ToLowerInvariant()))} can do that.";
}
