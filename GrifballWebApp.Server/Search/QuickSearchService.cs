using System.Globalization;
using GrifballWebApp.Database;
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
    /// whose name starts with it first, then by name. A player matched by both their name and a past gamertag is
    /// listed once, by their name.
    /// </summary>
    private async Task<QuickSearchPlayerDto[]> SearchPlayers(string q, CancellationToken ct)
    {
        var current = await _context.Users.AsNoTracking()
            .Where(u => (u.XboxUser != null && u.XboxUser.Gamertag.Contains(q))
                || (u.DiscordUser != null && u.DiscordUser.DiscordUsername.Contains(q))
                || (u.DisplayName != null && u.DisplayName.Contains(q)))
            .Select(u => new { u.Id, Name = u.XboxUser!.Gamertag ?? u.DiscordUser!.DiscordUsername ?? u.DisplayName ?? u.UserName ?? "" })
            .OrderBy(u => u.Name.StartsWith(q) ? 0 : 1).ThenBy(u => u.Name).ThenBy(u => u.Id)
            .Take(Limit)
            .ToListAsync(ct);

        var past = await _context.XboxGamertagHistory.AsNoTracking()
            .Where(h => h.XboxUser.User != null && h.Gamertag.Contains(q))
            .Select(h => new
            {
                h.XboxUser.User!.Id,
                PastGamertag = h.Gamertag,
                Name = h.XboxUser.Gamertag ?? h.XboxUser.User.DiscordUser!.DiscordUsername ?? h.XboxUser.User.DisplayName ?? h.XboxUser.User.UserName ?? "",
            })
            .OrderBy(h => h.PastGamertag.StartsWith(q) ? 0 : 1).ThenBy(h => h.PastGamertag).ThenBy(h => h.Id)
            .Take(Limit * 2)
            .ToListAsync(ct);

        var matchedByName = current.Select(u => u.Id).ToHashSet();
        return current.Select(u => new QuickSearchPlayerDto { UserID = u.Id, Name = u.Name, PastGamertag = null })
            .Concat(past.Where(h => !matchedByName.Contains(h.Id))
                .DistinctBy(h => h.Id)
                .Select(h => new QuickSearchPlayerDto { UserID = h.Id, Name = h.Name, PastGamertag = h.PastGamertag }))
            .OrderBy(p => StartsWith(p.PastGamertag ?? p.Name, q) ? 0 : 1)
            .ThenBy(p => p.PastGamertag ?? p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.UserID)
            .Take(Limit)
            .ToArray();
    }

    /// <summary>Teams whose name holds the query, of seasons that are public: names starting with it first, then the
    /// latest season's first.</summary>
    private Task<QuickSearchTeamDto[]> SearchTeams(string q, DateTime now, CancellationToken ct)
    {
        return _context.Teams.AsNoTracking()
            .Where(t => t.TeamName.Contains(q))
            .Where(t => t.Season.SeasonStart <= now || (t.Season.PublicAt != null && t.Season.PublicAt <= now))
            .OrderBy(t => t.TeamName.StartsWith(q) ? 0 : 1).ThenByDescending(t => t.Season.SeasonEnd).ThenBy(t => t.TeamName).ThenBy(t => t.TeamID)
            .Take(Limit)
            .Select(t => new QuickSearchTeamDto { TeamID = t.TeamID, TeamName = t.TeamName, SeasonID = t.SeasonID, SeasonName = t.Season.SeasonName })
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Public seasons whose name holds the query or, when it is a year, that ran in that year: names starting with it
    /// first, then the latest first. A season is public once it starts or is announced (its PublicAt has passed).
    /// </summary>
    private Task<QuickSearchSeasonDto[]> SearchSeasons(string q, DateTime now, CancellationToken ct)
    {
        var seasons = _context.Seasons.AsNoTracking()
            .Where(s => s.SeasonStart <= now || (s.PublicAt != null && s.PublicAt <= now));

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
