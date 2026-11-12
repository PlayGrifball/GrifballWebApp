using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Preview;

/// <summary>
/// Preview mode only: a gamertag under <see cref="Prefix"/> is a smoke player's (grifballwebapp.client/smoke), made up
/// here instead of looked up on Halo, so the smoke suite's new players can set a gamertag and sign up for a season.
/// Its XUID is negative (Xbox's never are), from the gamertag, so the same name always gets the same account. Every other
/// gamertag, and every XUID, goes to <see cref="GetsertXboxUserService"/> as in any environment.
/// </summary>
public sealed class PreviewSmokeXboxUsers(GetsertXboxUserService halo, GrifballContext context) : IGetsertXboxUserService
{
    /// <summary>What a smoke player's gamertag starts with.</summary>
    public const string Prefix = "smoke-";

    public async Task<(XboxUser?, string?)> GetsertXboxUserByGamertag(string gamertag, CancellationToken ct = default)
    {
        if (!gamertag.StartsWith(Prefix, StringComparison.Ordinal))
            return await halo.GetsertXboxUserByGamertag(gamertag, ct);

        var known = await context.XboxUsers
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Gamertag == gamertag, ct);
        if (known is not null)
            return (known, null);

        var made = XboxUser.FromXbox(Xuid(gamertag), gamertag);
        context.XboxUsers.Add(made);
        await context.SaveChangesAsync(ct);
        return (made, null);
    }

    public Task<XboxUser> GetsertXboxUserByXuid(long xuid, CancellationToken ct = default)
        => halo.GetsertXboxUserByXuid(xuid, ct);

    public Task<XboxUser[]> GetsertXboxUsersByXuid(long[] xuid, CancellationToken ct = default)
        => halo.GetsertXboxUsersByXuid(xuid, ct);

    /// <summary>A negative XUID from the gamertag (64-bit FNV-1a): stable across runs and pods.</summary>
    internal static long Xuid(string gamertag)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in gamertag)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return -1 - (long)(hash & long.MaxValue);
    }
}
