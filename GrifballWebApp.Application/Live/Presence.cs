using Tapper;

namespace GrifballWebApp.Application.Live;

/// <summary>
/// Who is on the site now, as the footer shows it: how many (members, each counted once however many tabs they have open,
/// and guests, a tab each), and the pages they are on, the busiest first, with the members on each by name. A page is its
/// route; a staff page, or the page of a season not announced yet, is the staff area alone (<see cref="StaffArea"/>).
/// </summary>
[TranspilationSource]
public sealed record PresenceSnapshot(int Online, int Members, int Guests, IReadOnlyList<PresencePage> Pages)
{
    /// <summary>The page every staff page is shown as.</summary>
    public const string StaffArea = "/staff";

    public static readonly PresenceSnapshot Empty = new(0, 0, 0, []);

    /// <summary>The snapshot of these tabs.</summary>
    public static PresenceSnapshot Of(IEnumerable<PresenceTab> tabs)
    {
        var all = tabs.ToList();
        var members = all.Where(t => t.UserID is not null).Select(t => t.UserID).Distinct().Count();
        var guests = all.Count(t => t.UserID is null);
        var pages = all
            .GroupBy(t => t.Path, StringComparer.Ordinal)
            .Select(page => new PresencePage(page.Key, page.Select(t => t.Subject).FirstOrDefault(s => s is not null), page.Count(t => t.UserID is null), [.. page
                .Where(t => t.UserID is not null)
                .DistinctBy(t => t.UserID)
                .Select(t => new PresenceMember(t.UserID!.Value, t.Name))
                .OrderBy(m => m.Name ?? "", StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.UserID)]))
            .OrderByDescending(p => p.Members.Count + p.Guests)
            .ThenBy(p => p.Path, StringComparer.Ordinal)
            .ToList();
        return new PresenceSnapshot(members + guests, members, guests, pages);
    }
}

/// <summary>
/// A page and who is on it: the members by name, and how many guests. Its subject is what it is about, by name (whose
/// profile, which season, team or match); null for a page about nothing in particular, or nothing the public may see.
/// </summary>
[TranspilationSource]
public sealed record PresencePage(string Path, string? Subject, int Guests, IReadOnlyList<PresenceMember> Members);

/// <summary>A member on a page: the name the site shows for them (never their login name; null when they have none).</summary>
[TranspilationSource]
public sealed record PresenceMember(int UserID, string? Name);

/// <summary>An open tab: its connection, its user (null: signed out) with their shown name, the page it shows, and what about.</summary>
public sealed record PresenceTab(string ConnectionID, int? UserID, string? Name, string Path, string? Subject = null);
