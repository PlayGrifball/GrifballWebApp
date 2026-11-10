using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Profile;

public interface ISetGamertagService
{
    Task<string?> SetGamertag(int userID, string gamertag, CancellationToken ct = default);
}

public class SetGamertagService : ISetGamertagService
{
    private readonly IApplicationDbContext _context;
    private readonly IGetsertXboxUserService _getsertXboxUserService;
    private readonly IUserMergeService _userMergeService;
    private readonly GamertagMatching _matching;
    public SetGamertagService(IApplicationDbContext context, IGetsertXboxUserService getsertXboxUserService, IUserMergeService userMergeService, GamertagMatching matching)
    {
        _context = context;
        _getsertXboxUserService = getsertXboxUserService;
        _userMergeService = userMergeService;
        _matching = matching;
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

        // Before the lookup, which may call Halo: a user with a gamertag is refused whatever they asked for
        if (user.XboxUserID is not null)
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

        // Nobody holds it: simply take the gamertag. Refused when someone else holds it, or the user was given one during
        // the lookup (a race)
        var tie = user.TieToXbox(xboxUser);
        if (tie is XboxTie.AlreadyTied or XboxTie.TiedToOther)
        {
            await _context.RollbackTransactionAsync(ct);
            return "Gamertag is already set for this user. Contact sysadmin if it needs to be changed";
        }

        if (tie is XboxTie.HeldByOther) // Must check if this is a dummy account
        {
            var existingUser = xboxUser.User!;

            if (existingUser.IsDummyUser is false)
            {
                await _context.RollbackTransactionAsync(ct);
                return "That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect";
            }

            try
            {
                // The placeholder holding the XUID (the import tied it to the account) is merged, the XUID with it
                await _userMergeService.Merge(userID, existingUser.Id, MergeOptions.For(MergeTrigger.SetGamertag, $"set gamertag {xboxUser.Gamertag}", userID), ct);
            }
            catch (MergeConflictException e)
            {
                await _context.RollbackTransactionAsync(ct);
                return "That gamertag's placeholder account could not be merged into yours: " + e.Message + " Contact a commissioner.";
            }
        }

        // The user owns the XUID now, either way: the account's past gamertags' placeholders follow
        var error = await ReconcilePastGamertags(userID, xboxUser, ct);
        if (error is not null)
        {
            await _context.RollbackTransactionAsync(ct);
            return error;
        }

        await _context.SaveChangesAsync(ct);
        await _context.CommitTransactionAsync(ct);

        return null; // Success, no error message
    }


    /// <summary>
    /// Placeholders (historical:&lt;gamertag&gt;) of gamertags the user's Xbox account is known to have had are merged
    /// when the seasons of their rows fit the account's history (it hadn't let go of the gamertag yet) and no other
    /// account's history claims the gamertag for that time. Any other placeholder only shares a name with the account
    /// (someone else may have used it years ago): a sysadmin decides, through a pending claim. Runs once the user owns the
    /// XUID, whether it was free or a placeholder of the import held it. All in the caller's transaction: one merge that
    /// can't be made undoes them all. Returns an error for the user, or null.
    /// </summary>
    private async Task<string?> ReconcilePastGamertags(int userID, XboxUser xboxUser, CancellationToken ct)
    {
        var pastGamertags = await _context.XboxUsers.PastGamertags().Where(h => h.XboxUserID == xboxUser.XboxUserID).ToListAsync(ct);
        foreach (var past in pastGamertags)
        {
            var placeholder = await _matching.PlaceholderAsync(past.Gamertag, untiedOnly: true, ct);
            if (placeholder is null || placeholder.Id == userID)
                continue;
            if (!await _matching.FitsTheHistoryAsync(placeholder.Id, past, ct))
            {
                await _matching.ClaimAsync(GamertagClaimKind.SetGamertag, xboxUser.XboxUserID, past.Gamertag, placeholder.Id, null, null, evenIfRejected: true, ct);
                continue;
            }
            try
            {
                await _userMergeService.Merge(userID, placeholder.Id, MergeOptions.For(MergeTrigger.SetGamertag, $"past gamertag {past.Gamertag}", userID), ct);
            }
            catch (MergeConflictException e)
            {
                return "That gamertag's placeholder account could not be merged into yours: " + e.Message + " Contact a commissioner.";
            }
        }

        var namesake = await _matching.PlaceholderAsync(xboxUser.Gamertag, untiedOnly: true, ct);
        if (namesake is not null && namesake.Id != userID)
            await _matching.ClaimAsync(GamertagClaimKind.SetGamertag, xboxUser.XboxUserID, xboxUser.Gamertag, namesake.Id, null, null, evenIfRejected: true, ct);
        return null;
    }
}
