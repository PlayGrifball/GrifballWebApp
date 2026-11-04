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

    /// <summary>
    /// The user's <paramref name="top"/> most earned medals. A user who does not exist, or has no Xbox account, has none,
    /// so the profile shows an empty panel rather than a load error.
    /// </summary>
    public async Task<ProfileCommendationsDto> GetAsync(int userID, int top = DefaultTop, CancellationToken ct = default)
    {
        var result = new ProfileCommendationsDto { UserID = userID };
        var xboxUserID = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userID)
            .Select(u => u.XboxUserID)
            .FirstOrDefaultAsync(ct);
        if (xboxUserID is null)
            return result;

        top = Math.Clamp(top, 1, MaxTop);

        // Through MatchParticipants, whose XboxUserID index finds the player's games, so MedalEarned is sought on its
        // (MatchID, XboxUserID) index rather than scanned. A line per kind of medal (there are a few dozen), with the
        // medal's own fields in the key, so the totals and the order are worked out here in one round trip.
        var earned = await _context.MatchParticipants
            .AsNoTracking()
            .Where(p => p.XboxUserID == xboxUserID)
            .SelectMany(p => p.MedalEarned)
            .Where(m => m.Count > 0)
            .GroupBy(m => new
            {
                m.MedalID,
                m.Medal.MedalName,
                m.Medal.Description,
                DifficultyName = m.Medal.MedalDifficulty.MedalDifficultyName,
                TypeName = m.Medal.MedalType.MedalTypeName,
            })
            .Select(g => new { g.Key, Count = g.Sum(m => m.Count), Matches = g.Count() })
            .ToListAsync(ct);

        result.TotalMedals = earned.Sum(e => e.Count);
        result.DistinctMedals = earned.Count;
        result.Commendations = earned
            .OrderByDescending(e => e.Count)
            .ThenByDescending(e => e.Matches)
            .ThenBy(e => e.Key.MedalID)
            .Take(top)
            .Select(e =>
            {
                var tier = TierOf(e.Count);
                return new CommendationDto
                {
                    MedalID = e.Key.MedalID,
                    MedalName = e.Key.MedalName,
                    Description = e.Key.Description,
                    DifficultyName = e.Key.DifficultyName,
                    TypeName = e.Key.TypeName,
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
