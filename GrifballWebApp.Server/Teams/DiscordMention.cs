using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using NetCord.Services;
using System.Security.Claims;

namespace GrifballWebApp.Server.Teams;

public static class DiscordMention
{
    /// <summary>
    /// How a user is named in a Discord message: a mention when their account is linked to Discord (the user's
    /// DiscordUserID, set by Discord sign-in, the bot and profile linking alike), else an older account's Discord
    /// NameIdentifier claim, else their display name, else their id.
    /// </summary>
    public static async Task<string> ForUser(GrifballContext context, int userID, CancellationToken ct = default)
    {
        var user = await context.Users
            .Where(x => x.Id == userID)
            .Select(x => new { x.DiscordUserID, x.DisplayName })
            .FirstOrDefaultAsync(ct);

        if (user?.DiscordUserID is long discordUserID)
            return new UserId((ulong)discordUserID).ToString();

        var claimValue = await context.UserClaims
            .Where(x => x.UserId == userID && x.ClaimType == ClaimTypes.NameIdentifier)
            .Select(x => x.ClaimValue)
            .FirstOrDefaultAsync(ct);
        if (ulong.TryParse(claimValue, out var claimDiscordUserID))
            return new UserId(claimDiscordUserID).ToString();

        return user?.DisplayName ?? userID.ToString();
    }
}
