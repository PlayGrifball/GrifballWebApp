using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// A player's commendations, from the medals (MedalEarned) of their Xbox account's games. There is no commendation
/// table yet: each medal is a commendation and its tiers are the counts in <see cref="TierThresholds"/>.
/// </summary>
public class ProfileCommendationsService
{
    /// <summary>The counts a medal's tiers are reached at.</summary>
    public static readonly IReadOnlyList<int> TierThresholds = [10, 50, 100, 250, 500, 1000];

    public const int DefaultTop = 12;
    public const int MaxTop = 100;

    private readonly GrifballContext _context;

    public ProfileCommendationsService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The user's <paramref name="top"/> most earned medals; null when there is no such user.</summary>
    public async Task<ProfileCommendationsDto?> GetAsync(int userID, int top = DefaultTop, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userID)
            .Select(u => new { u.XboxUserID })
            .FirstOrDefaultAsync(ct);
        if (user is null)
            return null;

        var result = new ProfileCommendationsDto { UserID = userID };
        if (user.XboxUserID is null)
            return result;

        top = Math.Clamp(top, 1, MaxTop);

        // A line per kind of medal (there are a few dozen), so the totals and the order are worked out here
        var earned = await _context.MedalEarned
            .AsNoTracking()
            .Where(m => m.XboxUserID == user.XboxUserID && m.Count > 0)
            .GroupBy(m => m.MedalID)
            .Select(g => new { MedalID = g.Key, Count = g.Sum(m => m.Count), Matches = g.Count() })
            .ToListAsync(ct);

        result.TotalMedals = earned.Sum(e => e.Count);
        result.DistinctMedals = earned.Count;

        var topEarned = earned
            .OrderByDescending(e => e.Count)
            .ThenByDescending(e => e.Matches)
            .ThenBy(e => e.MedalID)
            .Take(top)
            .ToList();
        var ids = topEarned.Select(e => e.MedalID).ToList();
        var medals = await _context.Medals
            .AsNoTracking()
            .Where(m => ids.Contains(m.MedalID))
            .Select(m => new
            {
                m.MedalID,
                m.MedalName,
                m.Description,
                DifficultyName = m.MedalDifficulty.MedalDifficultyName,
                TypeName = m.MedalType.MedalTypeName,
            })
            .ToDictionaryAsync(m => m.MedalID, ct);

        result.Commendations = topEarned
            .Select(e =>
            {
                medals.TryGetValue(e.MedalID, out var medal);
                var tier = TierOf(e.Count);
                return new CommendationDto
                {
                    MedalID = e.MedalID,
                    MedalName = medal?.MedalName ?? $"Medal {e.MedalID}",
                    Description = medal?.Description,
                    DifficultyName = medal?.DifficultyName,
                    TypeName = medal?.TypeName,
                    Count = e.Count,
                    Matches = e.Matches,
                    Tier = tier,
                    TierFloor = tier == 0 ? 0 : TierThresholds[tier - 1],
                    NextTierAt = tier < TierThresholds.Count ? TierThresholds[tier] : null,
                };
            })
            .ToList();
        return result;
    }

    /// <summary>How many tiers <paramref name="count"/> reaches.</summary>
    public static int TierOf(int count) => TierThresholds.Count(t => count >= t);
}
