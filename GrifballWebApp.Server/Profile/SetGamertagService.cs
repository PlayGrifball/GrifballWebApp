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

            // Placeholders (historical:<gamertag>) of the gamertags this Xbox account is known to have had are this
            // player's: merged. A placeholder of the gamertag itself only shares a name with the account (someone else
            // may have used it years ago), so a commissioner decides: it becomes a pending claim.
            var pastGamertags = await _context.XboxGamertagHistory.Where(h => h.XboxUserID == xboxUser.XboxUserID)
                .Select(h => h.Gamertag).ToListAsync(ct);
            foreach (var past in pastGamertags)
            {
                var placeholder = await Placeholder(past, ct);
                if (placeholder is null || placeholder.Id == userID)
                    continue;
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
            {
                var normalized = HistoricalKeys.Normalize(xboxUser.Gamertag);
                var claimed = await _context.GamertagClaims.AnyAsync(c => c.Kind == GamertagClaimKind.SetGamertag
                    && c.XboxUserID == xboxUser.XboxUserID && c.NormalizedGamertag == normalized, ct);
                if (!claimed)
                {
                    _context.GamertagClaims.Add(new GamertagClaim
                    {
                        Kind = GamertagClaimKind.SetGamertag,
                        XboxUserID = xboxUser.XboxUserID,
                        Gamertag = HistoricalKeys.CleanGamertag(xboxUser.Gamertag),
                        NormalizedGamertag = normalized,
                        PlaceholderUserID = namesake.Id,
                    });
                }
            }
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

    /// <summary>The placeholder of a gamertag not yet tied to an Xbox account.</summary>
    private Task<GrifballWebApp.Database.Models.User?> Placeholder(string gamertag, CancellationToken ct)
    {
        var name = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.IsDummyUser && u.XboxUserID == null && u.NormalizedUserName == name, ct);
    }
}
