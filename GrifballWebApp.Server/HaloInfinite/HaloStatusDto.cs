using GrifballWebApp.Server.Telemetry;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// What the Infinite Client page shows (<c>Admin/HaloStatus</c>). Direct mode: only <see cref="Mode"/>; the page then
/// asks <c>Admin/CheckStatus</c> as it always has. Gateway mode: the gateway's address and admin page, whether this
/// environment has an API key for it (never the key), and a fresh probe: whether the gateway answered at all
/// (<see cref="Reachable"/>), its Halo account's status word (<see cref="AccountStatus"/>, from its <c>/health</c>) and
/// what the test call through it answered (<see cref="ApiCode"/>).
/// </summary>
public sealed record HaloStatusDto
{
    public required HaloInfiniteMode Mode { get; init; }

    public string? GatewayUrl { get; init; }

    public string? GatewayAdminUrl { get; init; }

    public bool ApiKeyConfigured { get; init; }

    public bool? Reachable { get; init; }

    public string? AccountStatus { get; init; }

    public int? ApiCode { get; init; }

    public bool Healthy { get; init; }

    public string? Message { get; init; }

    public static HaloStatusDto Direct() => new() { Mode = HaloInfiniteMode.Direct };

    public static HaloStatusDto Gateway(HaloInfiniteSettings settings, HaloProbe probe) => new()
    {
        Mode = HaloInfiniteMode.Gateway,
        GatewayUrl = settings.GatewayBaseUrl.ToString(),
        GatewayAdminUrl = settings.GatewayAdminUrl,
        ApiKeyConfigured = !string.IsNullOrEmpty(settings.GatewayApiKey),
        Reachable = probe.Gateway?.Reachable,
        AccountStatus = probe.Gateway?.Status,
        ApiCode = probe.ApiCode,
        Healthy = probe.Passed,
        Message = probe.Description,
    };
}
