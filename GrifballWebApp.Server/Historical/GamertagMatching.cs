using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// How a gamertag is matched to an account or a placeholder, shared by the historical import (PlayerResolver, roster
/// suggestions), setting a gamertag (SetGamertagService) and claims (GamertagClaimService), so the three never disagree.
/// </summary>
public class GamertagMatching
{
    private readonly GrifballContext _context;

    public GamertagMatching(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>The placeholder (historical:&lt;gamertag&gt;) of a gamertag; with <paramref name="untiedOnly"/>, only
    /// one not tied to an Xbox account yet.</summary>
    public Task<User?> PlaceholderAsync(string gamertag, bool untiedOnly, CancellationToken ct)
    {
        var name = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.IsDummyUser && u.NormalizedUserName == name && (!untiedOnly || u.XboxUserID == null), ct);
    }

    /// <summary>
    /// Whether an account may have had a gamertag at <paramref name="from"/>: it hadn't let go of it before then. Only
    /// LastSeen bounds the period: FirstSeen is when the app first saw the account with it (a rename noticed, an approved
    /// claim), not when the account took it, and archive seasons are usually older.
    /// </summary>
    public static bool Covers(XboxGamertagHistory history, DateTime? from) => history.LastSeen is null || from is null || history.LastSeen >= from;

    /// <summary>
    /// Whether a placeholder's rows can be the account's under that past gamertag: none of their seasons began after the
    /// account let go of it, and no other account's history has the gamertag at a time that can include them.
    /// </summary>
    public async Task<bool> FitsTheHistoryAsync(int placeholderId, XboxGamertagHistory past, CancellationToken ct)
    {
        var starts = await _context.PlayerSeasonStats.Where(s => s.UserID == placeholderId).Select(s => s.Season.SeasonStart)
            .Concat(_context.PlayerChampionships.Where(c => c.UserID == placeholderId).Select(c => c.Season.SeasonStart))
            .ToListAsync(ct);
        if (past.LastSeen is not null && starts.Any(s => s > past.LastSeen))
            return false;
        DateTime? earliest = starts.Count == 0 ? null : starts.Min();
        var others = await _context.XboxGamertagHistory
            .Where(h => h.XboxUserID != past.XboxUserID && h.NormalizedGamertag == past.NormalizedGamertag)
            .ToListAsync(ct);
        return !others.Any(h => Covers(h, earliest));
    }

    /// <summary>
    /// Adds a pending claim for a sysadmin to decide, unless the same one (kind, account, gamertag) is pending or approved
    /// already. A rejected one stops it too unless <paramref name="evenIfRejected"/>: a user setting their gamertag again
    /// may claim again (a rejection doesn't block the account forever), the import doesn't repeat a suggestion a
    /// sysadmin turned down. Returns whether it was added. The caller saves.
    /// </summary>
    public async Task<bool> ClaimAsync(GamertagClaimKind kind, long xboxUserId, string gamertag, int placeholderId, int? seasonId, int? teamId, bool evenIfRejected, CancellationToken ct)
    {
        var normalized = HistoricalKeys.Normalize(gamertag);
        bool Same(GamertagClaim c) => c.Kind == kind && c.XboxUserID == xboxUserId && c.NormalizedGamertag == normalized && (!evenIfRejected || c.Status != GamertagClaimStatus.Rejected);
        if (_context.GamertagClaims.Local.Any(Same)
            || await _context.GamertagClaims.AnyAsync(c => c.Kind == kind && c.XboxUserID == xboxUserId && c.NormalizedGamertag == normalized
                && (!evenIfRejected || c.Status != GamertagClaimStatus.Rejected), ct))
            return false;
        _context.GamertagClaims.Add(new GamertagClaim
        {
            Kind = kind,
            XboxUserID = xboxUserId,
            Gamertag = HistoricalKeys.CleanGamertag(gamertag),
            NormalizedGamertag = normalized,
            PlaceholderUserID = placeholderId,
            SeasonID = seasonId,
            TeamID = teamId,
        });
        return true;
    }
}
