using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.HaloInfinite;

/// <summary>Finds a player's Xbox account through the Halo client and saves it (Infrastructure's GetsertXboxUserService).</summary>
public interface IGetsertXboxUserService
{
    Task<(XboxUser?, string?)> GetsertXboxUserByGamertag(string gamertag, CancellationToken ct = default);
    Task<XboxUser> GetsertXboxUserByXuid(long xuid, CancellationToken ct = default);
    Task<XboxUser[]> GetsertXboxUsersByXuid(long[] xuid, CancellationToken ct = default);
}
