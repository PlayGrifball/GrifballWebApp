using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

public interface ISetGamertagService
{
    Task<string?> SetGamertag(int userID, string gamertag, CancellationToken ct = default);
}

public class SetGamertagService : ISetGamertagService
{
    private readonly GrifballContext _context;
    private readonly IGetsertXboxUserService _getsertXboxUserService;
    private readonly IUserMergeService _userMergeService;
    public SetGamertagService(GrifballContext context, IGetsertXboxUserService getsertXboxUserService, IUserMergeService userMergeService)
    {
        _context = context;
        _getsertXboxUserService = getsertXboxUserService;
        _userMergeService = userMergeService;
    }

    public async Task<string?> SetGamertag(int userID, string gamertag, CancellationToken ct = default)
    {
        await _context.StartTransactionAsync(ct);

        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == userID)
            .FirstOrDefaultAsync(ct);

        if (user is null)
        {
            await _context.RollbackTransactionAsync(ct);
            return "User does not exist";
        }

        if (user.XboxUser is not null)
        {
            await _context.RollbackTransactionAsync(ct);
            return "Gamertag is already set for this user. Contact sysadmin if it needs to be changed";
        }

        var (xboxUser, result) = await _getsertXboxUserService.GetsertXboxUserByGamertag(gamertag, ct);

        if (xboxUser is null)
        {
            await _context.RollbackTransactionAsync(ct);
            return "Failed to set gamertag, reason: " + result;
        }

        if (xboxUser.User is null) // Simple just take the gamertag
        {
            user.XboxUser = xboxUser;

            // Placeholders (historical:<gamertag>) of gamertags this Xbox account is known to have had are merged when the
            // seasons of their rows fit the account's history (it hadn't let go of the gamertag yet) and no other account's
            // history claims the gamertag for that time. Any other placeholder only shares a name with the account
            // (someone else may have used it years ago): a sysadmin decides, through a pending claim. All the merges
            // are in this one transaction: one that can't be made undoes them all.
            var pastGamertags = await _context.XboxGamertagHistory.Where(h => h.XboxUserID == xboxUser.XboxUserID).ToListAsync(ct);
            foreach (var past in pastGamertags)
            {
                var placeholder = await Placeholder(past.Gamertag, ct);
                if (placeholder is null || placeholder.Id == userID)
                    continue;
                if (!await FitsTheHistory(placeholder.Id, past, ct))
                {
                    await Claim(xboxUser.XboxUserID, past.Gamertag, placeholder.Id, ct);
                    continue;
                }
                try
                {
                    await _userMergeService.Merge(userID, placeholder.Id, null, ct);
                }
                catch (MergeConflictException e)
                {
                    await _context.RollbackTransactionAsync(ct);
                    return "That gamertag's placeholder account could not be merged into yours: " + e.Message + " Contact a commissioner.";
                }
            }

            var namesake = await Placeholder(xboxUser.Gamertag, ct);
            if (namesake is not null && namesake.Id != userID)
                await Claim(xboxUser.XboxUserID, xboxUser.Gamertag, namesake.Id, ct);
        }
        else // Must check if this is a dummy account
        {
            var existingUser = xboxUser.User;

            if (existingUser.IsDummyUser is false)
            {
                await _context.RollbackTransactionAsync(ct);
                return "That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect";
            }

            try
            {
                await _userMergeService.Merge(userID, existingUser.Id, null, ct);
            }
            catch (MergeConflictException e)
            {
                await _context.RollbackTransactionAsync(ct);
                return "That gamertag's placeholder account could not be merged into yours: " + e.Message + " Contact a commissioner.";
            }
        }

        await _context.SaveChangesAsync(ct);
        await _context.CommitTransactionAsync(ct);

        return null; // Success, no error message
    }


    /// <summary>
    /// Whether the placeholder's historical rows can be the account's under that past gamertag: none of their seasons
    /// began after the account let go of it (its LastSeen; FirstSeen is only when the app first saw it, so it bounds
    /// nothing), and no other account's history has the gamertag at a time that can include them.
    /// </summary>
    private async Task<bool> FitsTheHistory(int placeholderId, XboxGamertagHistory past, CancellationToken ct)
    {
        var starts = await _context.PlayerSeasonStats.Where(s => s.UserID == placeholderId).Select(s => s.Season.SeasonStart)
            .Concat(_context.PlayerChampionships.Where(c => c.UserID == placeholderId).Select(c => c.Season.SeasonStart))
            .ToListAsync(ct);
        if (past.LastSeen is not null && starts.Any(s => s > past.LastSeen))
            return false;
        DateTime? earliest = starts.Count == 0 ? null : starts.Min();
        var reused = await _context.XboxGamertagHistory.AnyAsync(h => h.XboxUserID != past.XboxUserID
            && h.NormalizedGamertag == past.NormalizedGamertag
            && (h.LastSeen == null || earliest == null || h.LastSeen >= earliest), ct);
        return !reused;
    }

    /// <summary>A pending claim (once per account and gamertag) for a sysadmin to decide.</summary>
    private async Task Claim(long xboxUserId, string gamertag, int placeholderId, CancellationToken ct)
    {
        var normalized = HistoricalKeys.Normalize(gamertag);
        var claimed = _context.GamertagClaims.Local.Any(c => c.Kind == GamertagClaimKind.SetGamertag && c.XboxUserID == xboxUserId && c.NormalizedGamertag == normalized)
            || await _context.GamertagClaims.AnyAsync(c => c.Kind == GamertagClaimKind.SetGamertag && c.XboxUserID == xboxUserId && c.NormalizedGamertag == normalized, ct);
        if (claimed)
            return;
        _context.GamertagClaims.Add(new GamertagClaim
        {
            Kind = GamertagClaimKind.SetGamertag,
            XboxUserID = xboxUserId,
            Gamertag = HistoricalKeys.CleanGamertag(gamertag),
            NormalizedGamertag = normalized,
            PlaceholderUserID = placeholderId,
        });
    }

    /// <summary>The placeholder of a gamertag not yet tied to an Xbox account.</summary>
    private Task<User?> Placeholder(string gamertag, CancellationToken ct)
    {
        var name = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.IsDummyUser && u.XboxUserID == null && u.NormalizedUserName == name, ct);
    }
}
