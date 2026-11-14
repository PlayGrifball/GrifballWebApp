using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Halo;
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

    /// <summary>The account and a page of its games (from 1); null when the site has no such account.</summary>
    public async Task<XboxAccountDto?> GetAsync(long xboxUserID, int page = 1, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
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
