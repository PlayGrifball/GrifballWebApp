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
    private readonly IUserMergeService _userMergeService;
    public SetGamertagService(GrifballContext context, IGetsertXboxUserService getsertXboxUserService, IUserMergeService userMergeService)
    {
        _context = context;
        _getsertXboxUserService = getsertXboxUserService;
        _userMergeService = userMergeService;
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

            await _userMergeService.Merge(userID, existingUser.Id, ct);
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return null; // Success, no error message
    }
}
