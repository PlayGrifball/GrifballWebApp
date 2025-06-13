using GrifballWebApp.Database;
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
    public SetGamertagService(GrifballContext context, IGetsertXboxUserService getsertXboxUserService)
    {
        _context = context;
        _getsertXboxUserService = getsertXboxUserService;
    }

    public async Task<string?> SetGamertag(int userID, string gamertag, CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);

        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == userID)
            .FirstOrDefaultAsync(ct);

        if (user is null)
            return "User does not exist";

        if (user.XboxUser is not null)
            return "Gamertag is already set for this user. Contact sysadmin if it needs to be changed";

        var (xboxUser, result) = await _getsertXboxUserService.GetsertXboxUserByGamertag(gamertag, ct);

        if (xboxUser is null)
        {
            return "Failed to set gamertag, reason: " + result;
        }

        if (xboxUser.User is null) // Simple just take the gamertag
        {
            user.XboxUser = xboxUser;
        }
        else // Must check if this is a dummy account
        {
            var existingUser = xboxUser.User;

            if (existingUser.IsDummyUser is false)
            {
                return "That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect";
            }

            // TODO: We may want to more this remaining logic to a AccountMergerService so it can be reused by admins instead of only when taking gamertag from dummy account

            // Now we either must transfer everything to the dummy account and delete this account
            // or we transfer from the dummy account and delete the dummy. I think I'll do the latter

            // We'll grab most of the things that the user owns, but we'll leave roles just because muh security
            // TODO: review that we are transfer any new tables that user 'owns' now that did not when this service was written
            var existingUserID = existingUser.Id;

            if (existingUser.DiscordUserID is not null)
            {
                user.DiscordUserID = existingUser.DiscordUserID;
            }

            var teamPlayers = await _context.TeamPlayers.Where(tp => tp.UserID == existingUserID).ToListAsync(ct);
            teamPlayers.ForEach(tp => tp.UserID = userID);

            var experience = await _context.UserExperiences.Where(x => x.UserID == existingUserID).ToListAsync(ct);
            experience.ForEach(x => x.UserID = userID);

            var signups = await _context.SeasonSignups.Where(x => x.UserID == existingUserID).ToListAsync(ct);
            signups.ForEach(x => x.UserID = userID);

            // Delete just in case it doesnt cascade
            var rolesToDelete = await _context.UserRoles.Where(x => x.UserId == existingUserID).ToArrayAsync(ct);
            _context.UserRoles.RemoveRange(rolesToDelete);
            //ExecuteDeleteAsync does not work in with in memory test
            //await _context.UserRoles.Where(x => x.UserId == existingUserID).ExecuteDeleteAsync(ct);

            await _context.SaveChangesAsync(ct);

            _context.Users.Remove(existingUser);
            await _context.SaveChangesAsync(ct);

            user.XboxUser = xboxUser;
            await _context.SaveChangesAsync(ct);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return null; // Success, no error message
    }
}
