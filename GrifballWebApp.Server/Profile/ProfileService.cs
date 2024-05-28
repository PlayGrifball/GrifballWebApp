using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Server.Profile;

public class ProfileService
{
    private readonly GrifballContext _context;
    private readonly HaloInfiniteClientFactory _infiniteClientFactory;
    public ProfileService(GrifballContext context, HaloInfiniteClientFactory clientFactory)
    {
        _context = context;
        _infiniteClientFactory = clientFactory;
    }

    public async Task SetGamertag(int userID, string gamertag, CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);

        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == userID)
            .FirstOrDefaultAsync(ct);

        if (user is null)
            throw new Exception("User does not exist");

        if (user.XboxUser is not null)
            throw new Exception("Gamertag is already set for this user. Contact sysadmin if it needs to be changed");

        var xboxUser = await _context.XboxUsers
            .Include(x => x.User)
            .Where(x => x.Gamertag == gamertag)
            .FirstOrDefaultAsync(ct);

        if (xboxUser is null)
        {
            var client = await _infiniteClientFactory.CreateAsync();

            var response = await client.UserByGamertag(gamertag);

            if (response is null || response.Result is null || string.IsNullOrEmpty(response.Result.gamertag))
            {
                throw new Exception("Did not find that gamertag");
            }

            var parsed = long.TryParse(response.Result.xuid, out var xboxUserID);

            if (parsed is false)
                throw new Exception("Failed to parse xuid. Contact sysadmin");

            xboxUser = new XboxUser()
            {
                XboxUserID = xboxUserID,
                Gamertag = response.Result.gamertag,
            };
            user.XboxUser = xboxUser;
        }
        else
        {
            if (xboxUser.User is null) // Simple just take the gamertag
            {
                user.XboxUser = xboxUser;
            }
            else // Must check if this is a dummy account
            {
                var existingUser = xboxUser.User;

                if (existingUser.IsDummyUser is false)
                {
                    throw new Exception("That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect");
                }

                // Now we either must transfer everything to the dummy account and delete this account
                // or we transfer from the dummy account and delete the dummy. I think I'll do the latter

                // We'll grab most of the things that the user owns, but we'll leave roles just because muh security
                var existingUserID = existingUser.Id;
                var teamPlayers = await _context.TeamPlayers.Where(tp => tp.UserID == existingUserID).ToListAsync(ct);
                teamPlayers.ForEach(tp => tp.UserID = userID);

                var experience = await _context.UserExperiences.Where(x => x.UserID == existingUserID).ToListAsync(ct);
                experience.ForEach(x => x.UserID = userID);

                var signups = await _context.SeasonSignups.Where(x => x.UserID == existingUserID).ToListAsync(ct);
                signups.ForEach(x => x.UserID = userID);

                // Delete just in case it doesnt cascade
                await _context.UserRoles.Where(x => x.UserId == existingUserID).ExecuteDeleteAsync(ct);

                await _context.SaveChangesAsync(ct);

                _context.Users.Remove(existingUser);
                await _context.SaveChangesAsync(ct);

                user.XboxUser = xboxUser;
                await _context.SaveChangesAsync(ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
