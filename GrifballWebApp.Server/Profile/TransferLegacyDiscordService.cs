using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Profile;

public class TransferLegacyDiscordService
{
    private readonly GrifballContext _context;
    public TransferLegacyDiscordService(GrifballContext context)
    {
        _context = context;
    }

    public async Task TransferAllAsync()
    {
        var users = await _context.UserLogins
            .Select(x => new
            {
                LoginProvider = x.LoginProvider,
                DiscordId = x.ProviderKey,
                User = x.User,
                
            })
            .Where(x => x.LoginProvider == "Discord")
            .Where(x => x.User.DiscordUserID == null)
            .ToArrayAsync();

        foreach (var user in users)
        {
            var existing = await _context.DiscordUsers
                .FirstOrDefaultAsync(x => x.DiscordUserID == long.Parse(user.DiscordId));

            if (existing is null)
            {
                var claim = _context.UserClaims
                    .Where(x => x.UserId == user.User.Id && x.ClaimType == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")
                    .Select(x => x.ClaimValue)
                    .FirstOrDefault();

                if (claim is null)
                    continue;

                var discordUser = new DiscordUser
                {
                    DiscordUserID = long.Parse(user.DiscordId),
                    DiscordUsername = claim,
                };
                _context.DiscordUsers.Add(discordUser);
            }

            user.User.DiscordUserID = long.Parse(user.DiscordId);
            await _context.SaveChangesAsync();
        }
    }
}
