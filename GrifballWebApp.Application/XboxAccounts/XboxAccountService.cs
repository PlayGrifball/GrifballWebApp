using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.XboxAccounts;

/// <summary>One of an Xbox account's saved Halo games: its result for the account's team, and the account's own line.</summary>
public sealed class XboxAccountGameDto
{
    public required Guid MatchID { get; init; }
    public required DateTime? StartTime { get; init; }
    /// <summary>The score of the account's team.</summary>
    public required int Score { get; init; }
    /// <summary>The other team's score; null when the game had no other team.</summary>
    public required int? OtherScore { get; init; }
    public required Outcomes Outcome { get; init; }
    public required int Kills { get; init; }
    public required int Deaths { get; init; }
    public required int Assists { get; init; }
}

/// <summary>An Xbox account and a page of its saved Halo games, newest first: Halo's data only, nothing of the league.</summary>
public sealed class XboxAccountDto
{
    public required long XboxUserID { get; init; }
    public required string Gamertag { get; init; }
    /// <summary>The site user who holds the account (their profile); null when nobody does: no account was created for it.</summary>
    public required int? UserID { get; init; }
    /// <summary>How many games the site has saved for the account, every page.</summary>
    public required int GameCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required List<XboxAccountGameDto> Games { get; init; }
}

/// <summary>An account a gamertag search found: its XUID and gamertag, and the past gamertag it was found by (null when its current one was).</summary>
public sealed record XboxAccountChoice(long XboxUserID, string Gamertag, string? PastGamertag);

/// <summary>
/// An Xbox account's own page: its gamertag and the Halo games the site saved that it played in, whether or not it is a
/// league player. Its profile (league data) is the user's, when one holds the account.
/// </summary>
public class XboxAccountService
{
    public const int PageSize = 25;

    private readonly IApplicationDbContext _context;

    public XboxAccountService(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The account a gamertag (trimmed) is: one whose gamertag it is now, else one that had it before (its history). Gamertags aren't
    /// unique (a row keeps the one its account renamed away from until it's looked up again), so of several, the one that
    /// played last. Text compares case-insensitively on every database (the context's collation).
    /// </summary>
    public Task<(long XboxUserID, string Gamertag)?> FindByGamertagAsync(string gamertag, CancellationToken ct = default)
        => FindAsync(gamertag, pastToo: true, ct);

    /// <summary>
    /// As <see cref="FindByGamertagAsync"/>, by current gamertags alone: what a slash command's typed gamertag finds. A past
    /// gamertag is found only when picked from the option's list, labelled as one (<see cref="SearchAsync"/>).
    /// </summary>
    public Task<(long XboxUserID, string Gamertag)?> FindByCurrentGamertagAsync(string gamertag, CancellationToken ct = default)
        => FindAsync(gamertag, pastToo: false, ct);

    private async Task<(long XboxUserID, string Gamertag)?> FindAsync(string gamertag, bool pastToo, CancellationToken ct)
    {
        gamertag = gamertag.Trim();
        var normalized = XboxGamertagHistory.Normalize(gamertag);
        var current = _context.XboxUsers.Where(x => x.Gamertag == gamertag).Select(x => x.XboxUserID);
        var past = _context.XboxUsers.PastGamertags().Where(h => h.NormalizedGamertag == normalized).Select(h => h.XboxUserID);
        foreach (var xuids in pastToo ? new[] { current, past } : [current])
        {
            var found = await _context.XboxUsers.AsNoTracking()
                .Where(x => xuids.Contains(x.XboxUserID))
                .Select(x => new
                {
                    x.XboxUserID,
                    x.Gamertag,
                    LastPlayed = _context.Matches.Where(m => m.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == x.XboxUserID))).Max(m => m.StartTime),
                })
                .OrderByDescending(x => x.LastPlayed != null)
                .ThenByDescending(x => x.LastPlayed)
                .ThenByDescending(x => x.XboxUserID)
                .FirstOrDefaultAsync(ct);
            if (found is not null)
                return (found.XboxUserID, found.Gamertag);
        }
        return null;
    }

    /// <summary>
    /// The accounts a slash command's gamertag option lists for what was typed: those whose current gamertag holds it, then
    /// those with a past gamertag holding it (one per account, none already listed by its current gamertag), each group
    /// with the gamertags starting with it first. At most <paramref name="limit"/>; with nothing typed, no past gamertags.
    /// </summary>
    public async Task<List<XboxAccountChoice>> SearchAsync(string? query, int limit, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        var current = await _context.XboxUsers.AsNoTracking()
            .Where(x => x.Gamertag.Contains(q))
            .OrderBy(x => x.Gamertag.StartsWith(q) ? 0 : 1).ThenBy(x => x.Gamertag).ThenBy(x => x.XboxUserID)
            .Take(limit)
            .Select(x => new XboxAccountChoice(x.XboxUserID, x.Gamertag, null))
            .ToListAsync(ct);
        if (current.Count >= limit || q.Length == 0)
            return current;

        var pastRows = await _context.XboxUsers.PastGamertags()
            .Where(h => h.Gamertag.Contains(q) && !h.XboxUser.Gamertag.Contains(q))
            .Select(h => new { h.XboxUserID, Current = h.XboxUser.Gamertag, h.Gamertag, h.NormalizedGamertag, Rank = h.Gamertag.StartsWith(q) ? 0 : 1 })
            .ToListAsync(ct);
        var past = pastRows
            // A past gamertag that is the current one (in another case or spacing) is no alias
            .Where(h => h.NormalizedGamertag != XboxGamertagHistory.Normalize(h.Current))
            .GroupBy(h => h.XboxUserID)
            .Select(g => g.OrderBy(h => h.Rank).ThenBy(h => h.Gamertag, StringComparer.OrdinalIgnoreCase).First())
            .OrderBy(h => h.Rank).ThenBy(h => h.Gamertag, StringComparer.OrdinalIgnoreCase).ThenBy(h => h.XboxUserID)
            .Take(limit - current.Count)
            .Select(h => new XboxAccountChoice(h.XboxUserID, h.Current, h.Gamertag));
        return [.. current, .. past];
    }

    /// <summary>Whether the site has the account.</summary>
    public Task<bool> Exists(long xboxUserID, CancellationToken ct = default)
        => _context.XboxUsers.AnyAsync(x => x.XboxUserID == xboxUserID, ct);

    /// <summary>The account and a page of its games (from 1); null when the site has no such account.</summary>
    public async Task<XboxAccountDto?> GetAsync(long xboxUserID, int page = 1, CancellationToken ct = default)
    {
        // From the query string: clamped so the offset can't overflow (a page past the end is empty)
        page = Math.Clamp(page, 1, int.MaxValue / PageSize);
        var account = await _context.XboxUsers.AsNoTracking()
            .Where(x => x.XboxUserID == xboxUserID)
            .Select(x => new
            {
                x.Gamertag,
                UserID = _context.Users.Where(u => u.XboxUserID == x.XboxUserID).Select(u => (int?)u.Id).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (account is null)
            return null;

        var games = _context.Matches.AsNoTracking()
            .Where(m => m.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == xboxUserID)));
        var count = await games.CountAsync(ct);
        var rows = await games
            // Undated games last: Postgres would put NULLs first
            .OrderBy(m => m.StartTime == null)
            .ThenByDescending(m => m.StartTime)
            .ThenBy(m => m.MatchID)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(m => new
            {
                m.MatchID,
                m.StartTime,
                Team = m.MatchTeams.Where(t => t.MatchParticipants.Any(p => p.XboxUserID == xboxUserID)).Select(t => new { t.Score, t.Outcome }).First(),
                OtherScore = m.MatchTeams.Where(t => !t.MatchParticipants.Any(p => p.XboxUserID == xboxUserID)).Select(t => (int?)t.Score).Max(),
                Line = m.MatchTeams.SelectMany(t => t.MatchParticipants).Where(p => p.XboxUserID == xboxUserID).Select(p => new { p.Kills, p.Deaths, p.Assists }).First(),
            })
            .ToListAsync(ct);

        return new XboxAccountDto
        {
            XboxUserID = xboxUserID,
            Gamertag = account.Gamertag,
            UserID = account.UserID,
            GameCount = count,
            Page = page,
            PageSize = PageSize,
            Games = rows.Select(r => new XboxAccountGameDto
            {
                MatchID = r.MatchID,
                StartTime = r.StartTime,
                Score = r.Team.Score,
                OtherScore = r.OtherScore,
                Outcome = r.Team.Outcome,
                Kills = r.Line.Kills,
                Deaths = r.Line.Deaths,
                Assists = r.Line.Assists,
            }).ToList(),
        };
    }
}
