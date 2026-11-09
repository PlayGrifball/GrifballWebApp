using System.Globalization;
using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Search;

/// <summary>
/// The quick search behind the Ctrl+K navigator: players by their current gamertag, Discord or display name and their
/// past gamertags (<see cref="Domain.Players.XboxGamertagHistory"/>), teams by name (each season's team on its own,
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
    private readonly TimeProvider _time;

    public QuickSearchService(GrifballContext context, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = context;
    }

    /// <summary>The hits for <paramref name="query"/>; none of any kind when it is shorter than <see cref="MinLength"/>.</summary>
    public async Task<QuickSearchDto> SearchAsync(string? query, CancellationToken ct = default)
    {
        if (Clean(query) is not { } q)
            return new QuickSearchDto { Players = [], Teams = [], Seasons = [] };

        var now = _time.GetUtcNow().UtcDateTime;
        return new QuickSearchDto
        {
            Players = await SearchPlayers(q, ct),
            Teams = await SearchTeams(q, now, ct),
            Seasons = await SearchSeasons(q, now, ct),
        };
    }

    /// <summary>The player hits alone, by the same rules: the leaderboards' player search.</summary>
    public async Task<QuickSearchPlayerDto[]> SearchPlayersAsync(string? query, CancellationToken ct = default)
        => Clean(query) is { } q ? await SearchPlayers(q, ct) : [];

    /// <summary>The query trimmed and cut to <see cref="MaxLength"/>; null when shorter than <see cref="MinLength"/>.</summary>
    private static string? Clean(string? query)
    {
        var q = (query ?? "").Trim();
        if (q.Length > MaxLength)
            q = q[..MaxLength].TrimEnd();
        return q.Length < MinLength ? null : q;
    }

    /// <summary>
    /// Players whose current name (gamertag, Discord name or display name) or a past gamertag holds the query: those
    /// with the name matched starting with it first, then by that name. A player whose current name holds the query is
    /// listed by it, never by a past gamertag. A past gamertag that is their current one (normalized as the profile
    /// does, <see cref="HistoricalKeys.Normalize"/>) is not an alias.
    /// </summary>
    private async Task<QuickSearchPlayerDto[]> SearchPlayers(string q, CancellationToken ct)
    {
        // Ranked in the database by the same keys as the merged list below, so the few taken are the first of it
        var current = await _context.Users.AsNoTracking()
            .Where(NameHolds(q))
            .Select(u => new
            {
                u.Id,
                Name = u.XboxUser!.Gamertag ?? u.DiscordUser!.DiscordUsername ?? u.DisplayName ?? "",
                HasXbox = u.XboxUserID != null,
                u.IsDummyUser,
                // The name the query matched: the first starting with it, else the first holding it
                Matched = u.XboxUser != null && u.XboxUser.Gamertag.StartsWith(q) ? u.XboxUser.Gamertag
                    : u.DiscordUser != null && u.DiscordUser.DiscordUsername.StartsWith(q) ? u.DiscordUser.DiscordUsername
                    : u.DisplayName != null && u.DisplayName.StartsWith(q) ? u.DisplayName
                    : u.XboxUser != null && u.XboxUser.Gamertag.Contains(q) ? u.XboxUser.Gamertag
                    : u.DiscordUser != null && u.DiscordUser.DiscordUsername.Contains(q) ? u.DiscordUser.DiscordUsername
                    : u.DisplayName ?? "",
                Rank = (u.XboxUser != null && u.XboxUser.Gamertag.StartsWith(q))
                    || (u.DiscordUser != null && u.DiscordUser.DiscordUsername.StartsWith(q))
                    || (u.DisplayName != null && u.DisplayName.StartsWith(q)) ? 0 : 1,
            })
            .OrderBy(u => u.Rank).ThenBy(u => u.Matched).ThenBy(u => u.Id)
            .Take(Limit)
            .ToListAsync(ct);

        // From the past gamertags holding the query (not every player's history), of players whose current names do
        // not; one row per player below, so a renamer's many aliases take no room
        var pastRows = await (
                from h in _context.PastGamertags().Where(h => h.Gamertag.Contains(q))
                join u in _context.Users.AsNoTracking().Where(NameLacks(q)) on (long?)h.XboxUserID equals u.XboxUserID
                select new
                {
                    u.Id,
                    Name = u.XboxUser!.Gamertag ?? u.DiscordUser!.DiscordUsername ?? u.DisplayName ?? "",
                    Current = u.XboxUser!.Gamertag,
                    h.Gamertag,
                    h.NormalizedGamertag,
                    Rank = h.Gamertag.StartsWith(q) ? 0 : 1,
                })
            .ToListAsync(ct);
        var past = pastRows
            .Where(h => h.NormalizedGamertag != HistoricalKeys.Normalize(h.Current))
            .GroupBy(h => h.Id)
            .Select(g => g.OrderBy(h => h.Rank).ThenBy(h => h.Gamertag, StringComparer.OrdinalIgnoreCase).First());

        var players = current
            .Select(u => (u.Rank, Dto: new QuickSearchPlayerDto
            {
                UserID = u.Id,
                Name = u.Name,
                PastGamertag = null,
                MatchedName = string.Equals(u.Matched, u.Name, StringComparison.OrdinalIgnoreCase) ? null : u.Matched,
                Kind = u.HasXbox ? QuickSearchPlayerKind.Xbox : u.IsDummyUser ? QuickSearchPlayerKind.Archive : null,
            }))
            // A past gamertag is found through the player's Xbox account's history: they have one
            .Concat(past.Select(h => (h.Rank, Dto: new QuickSearchPlayerDto { UserID = h.Id, Name = h.Name, PastGamertag = h.Gamertag, Kind = QuickSearchPlayerKind.Xbox })))
            .OrderBy(p => p.Rank)
            .ThenBy(p => p.Dto.PastGamertag ?? p.Dto.MatchedName ?? p.Dto.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Dto.UserID)
            .Take(Limit)
            .Select(p => p.Dto)
            .ToArray();

        // The archive players' years, from the seasons they played or won a title in: one query for those listed
        var archive = players.Where(p => p.Kind == QuickSearchPlayerKind.Archive).Select(p => p.UserID).ToList();
        if (archive.Count > 0)
        {
            var starts = await _context.PlayerSeasonStats.AsNoTracking().Where(s => archive.Contains(s.UserID)).Select(s => new { s.UserID, s.Season.SeasonStart })
                .Concat(_context.Championships().Where(c => archive.Contains(c.UserID)).Select(c => new { c.UserID, c.Season.SeasonStart }))
                .ToListAsync(ct);
            var years = starts.GroupBy(s => s.UserID).ToDictionary(g => g.Key, g => YearSpan(g.Min(s => s.SeasonStart).Year, g.Max(s => s.SeasonStart).Year));
            foreach (var p in players)
                p.Years = years.GetValueOrDefault(p.UserID);
        }
        return players;
    }

    /// <summary>"2009", or "2009–2011" across years.</summary>
    public static string YearSpan(int first, int last) => first == last ? first.ToString(CultureInfo.InvariantCulture) : $"{first}–{last}";

    /// <summary>Whether one of the player's current names (gamertag, Discord name or display name) holds the query.</summary>
    private static Expression<Func<User, bool>> NameHolds(string q) =>
        u => (u.XboxUser != null && u.XboxUser.Gamertag.Contains(q))
            || (u.DiscordUser != null && u.DiscordUser.DiscordUsername.Contains(q))
            || (u.DisplayName != null && u.DisplayName.Contains(q));

    /// <summary>The players <see cref="NameHolds"/> leaves out: the past-gamertag search's, so no player is listed twice.</summary>
    private static Expression<Func<User, bool>> NameLacks(string q)
    {
        var holds = NameHolds(q);
        return Expression.Lambda<Func<User, bool>>(Expression.Not(holds.Body), holds.Parameters);
    }

    /// <summary>Teams whose name holds the query, of seasons that are public: names starting with it first, then the
    /// latest season's first.</summary>
    private Task<QuickSearchTeamDto[]> SearchTeams(string q, DateTime now, CancellationToken ct)
    {
        var publicSeasons = _context.Seasons.Where(Season.PublicAsOf(now));
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
    /// first, then the latest first. A season is public once it starts or is announced (see Season.PublicAsOf).
    /// </summary>
    private Task<QuickSearchSeasonDto[]> SearchSeasons(string q, DateTime now, CancellationToken ct)
    {
        var seasons = _context.Seasons.AsNoTracking().Where(Season.PublicAsOf(now));

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
            // YearSpan runs in the final projection, on the rows read
            .Select(s => new QuickSearchSeasonDto { SeasonID = s.SeasonID, SeasonName = s.SeasonName, Start = s.SeasonStart, End = s.SeasonEnd, Years = YearSpan(s.SeasonStart.Year, s.SeasonEnd.Year) })
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
}
