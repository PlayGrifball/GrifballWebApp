using System.Globalization;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Search;

/// <summary>
/// The quick search behind the Ctrl+K navigator: players by their current gamertag, Discord or display name and their
/// past gamertags (<see cref="Database.Models.XboxGamertagHistory"/>), teams by name (each season's team on its own,
/// linking to that season's roster) and seasons by name or by a year they ran in. Text compares case-insensitively
/// on every database (the context's collation), so plain Contains/StartsWith are used.
/// </summary>
public class QuickSearchService
{
    /// <summary>The most hits of each kind returned.</summary>
    public const int Limit = 8;
    /// <summary>The fewest characters searched for (a year is searched for whatever its length).</summary>
    public const int MinLength = 2;
    /// <summary>The longest query searched for; a longer one is cut to this length.</summary>
    public const int MaxLength = 64;

    private readonly GrifballContext _context;

    public QuickSearchService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The hits for <paramref name="query"/>; none of any kind when it is shorter than <see cref="MinLength"/>.</summary>
    public async Task<QuickSearchDto> SearchAsync(string? query, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        if (q.Length > MaxLength)
            q = q[..MaxLength].TrimEnd();
        if (q.Length < MinLength)
            return new QuickSearchDto { Players = [], Teams = [], Seasons = [] };

        var now = DateTime.UtcNow;
        return new QuickSearchDto
        {
            Players = await SearchPlayers(q, ct),
            Teams = await SearchTeams(q, now, ct),
            Seasons = await SearchSeasons(q, now, ct),
        };
    }

    /// <summary>
    /// Players whose current name (gamertag, Discord name or display name) or a past gamertag holds the query: those
    /// with a name or past gamertag starting with it first, then by it. A player matched by both their name and a past
    /// gamertag is listed once, by their name. A past gamertag that is also their current one is not an alias.
    /// </summary>
    private async Task<QuickSearchPlayerDto[]> SearchPlayers(string q, CancellationToken ct)
    {
        var current = (await _context.Users.AsNoTracking()
            .Where(u => (u.XboxUser != null && u.XboxUser.Gamertag.Contains(q))
                || (u.DiscordUser != null && u.DiscordUser.DiscordUsername.Contains(q))
                || (u.DisplayName != null && u.DisplayName.Contains(q)))
            .OrderBy(u => (u.XboxUser != null && u.XboxUser.Gamertag.StartsWith(q))
                || (u.DiscordUser != null && u.DiscordUser.DiscordUsername.StartsWith(q))
                || (u.DisplayName != null && u.DisplayName.StartsWith(q)) ? 0 : 1)
            .ThenBy(u => u.XboxUser!.Gamertag ?? u.DiscordUser!.DiscordUsername ?? u.DisplayName ?? u.UserName ?? "")
            .ThenBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                Name = u.XboxUser!.Gamertag ?? u.DiscordUser!.DiscordUsername ?? u.DisplayName ?? u.UserName ?? "",
                Gamertag = u.XboxUser != null ? u.XboxUser.Gamertag : null,
                DiscordName = u.DiscordUser != null ? u.DiscordUser.DiscordUsername : null,
                u.DisplayName,
            })
            .Take(Limit)
            .ToListAsync(ct))
            .Select(u => (u.Id, u.Name, Matched: MatchedName(q, u.Gamertag, u.DiscordName, u.DisplayName) ?? u.Name))
            .ToList();

        // One row per player, so a renamer's many aliases or the players already found by name take no room
        var found = current.Select(u => u.Id).ToList();
        var past = await _context.Users.AsNoTracking()
            .Where(u => u.XboxUser != null && !found.Contains(u.Id))
            .Select(u => new
            {
                u.Id,
                Name = u.XboxUser!.Gamertag ?? u.DiscordUser!.DiscordUsername ?? u.DisplayName ?? u.UserName ?? "",
                Prefix = _context.XboxGamertagHistory.Any(h => h.XboxUserID == u.XboxUserID && h.Gamertag.StartsWith(q)
                    && h.NormalizedGamertag != u.XboxUser!.Gamertag.ToUpper()),
                PastGamertag = _context.XboxGamertagHistory
                    .Where(h => h.XboxUserID == u.XboxUserID && h.Gamertag.Contains(q)
                        && h.NormalizedGamertag != u.XboxUser!.Gamertag.ToUpper())
                    .OrderBy(h => h.Gamertag.StartsWith(q) ? 0 : 1).ThenBy(h => h.Gamertag)
                    .Select(h => h.Gamertag)
                    .FirstOrDefault(),
            })
            .Where(u => u.PastGamertag != null)
            .OrderBy(u => u.Prefix ? 0 : 1).ThenBy(u => u.PastGamertag).ThenBy(u => u.Id)
            .Take(Limit)
            .ToListAsync(ct);

        return current
            .Select(u => new QuickSearchPlayerDto
            {
                UserID = u.Id,
                Name = u.Name,
                PastGamertag = null,
                MatchedName = string.Equals(u.Matched, u.Name, StringComparison.OrdinalIgnoreCase) ? null : u.Matched,
            })
            .Concat(past.Select(h => new QuickSearchPlayerDto { UserID = h.Id, Name = h.Name, PastGamertag = h.PastGamertag }))
            .OrderBy(p => StartsWith(Matched(p), q) ? 0 : 1)
            .ThenBy(Matched, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.UserID)
            .Take(Limit)
            .ToArray();

        static string Matched(QuickSearchPlayerDto p) => p.PastGamertag ?? p.MatchedName ?? p.Name;
    }

    /// <summary>Of a player's names, the one the query matched: the first starting with it, else the first holding it;
    /// null when none does as compared here (the database's collation can be looser).</summary>
    public static string? MatchedName(string q, params string?[] names) =>
        names.FirstOrDefault(n => n is not null && StartsWith(n, q))
        ?? names.FirstOrDefault(n => n is not null && n.Contains(q, StringComparison.OrdinalIgnoreCase));

    /// <summary>Teams whose name holds the query, of seasons that are public: names starting with it first, then the
    /// latest season's first.</summary>
    private Task<QuickSearchTeamDto[]> SearchTeams(string q, DateTime now, CancellationToken ct)
    {
        var publicSeasons = _context.Seasons.Public(now);
        return _context.Teams.AsNoTracking()
            .Where(t => t.TeamName.Contains(q))
            .Where(t => publicSeasons.Any(s => s.SeasonID == t.SeasonID))
            .OrderBy(t => t.TeamName.StartsWith(q) ? 0 : 1).ThenByDescending(t => t.Season.SeasonEnd).ThenBy(t => t.TeamName).ThenBy(t => t.TeamID)
            .Take(Limit)
            .Select(t => new QuickSearchTeamDto { TeamID = t.TeamID, TeamName = t.TeamName, SeasonID = t.SeasonID, SeasonName = t.Season.SeasonName })
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Public seasons whose name holds the query or, when it is a year, that ran in that year: names starting with it
    /// first, then the latest first. A season is public once it starts or is announced (see SeasonQueries.Public).
    /// </summary>
    private Task<QuickSearchSeasonDto[]> SearchSeasons(string q, DateTime now, CancellationToken ct)
    {
        var seasons = _context.Seasons.AsNoTracking().Public(now);

        if (TryParseYear(q, out var year))
        {
            var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddYears(1);
            seasons = seasons.Where(s => s.SeasonName.Contains(q) || (s.SeasonStart < to && s.SeasonEnd >= from));
        }
        else
        {
            seasons = seasons.Where(s => s.SeasonName.Contains(q));
        }

        return seasons
            .OrderBy(s => s.SeasonName.StartsWith(q) ? 0 : 1).ThenByDescending(s => s.SeasonEnd).ThenByDescending(s => s.SeasonID)
            .Take(Limit)
            .Select(s => new QuickSearchSeasonDto { SeasonID = s.SeasonID, SeasonName = s.SeasonName, Start = s.SeasonStart, End = s.SeasonEnd })
            .ToArrayAsync(ct);
    }

    /// <summary>Whether the query is a four-digit year the league could have played in.</summary>
    public static bool TryParseYear(string q, out int year)
    {
        year = 0;
        return q.Length == 4
            && int.TryParse(q, NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && year is >= 2000 and <= 2999;
    }

    private static bool StartsWith(string text, string q) => text.StartsWith(q, StringComparison.OrdinalIgnoreCase);
}
