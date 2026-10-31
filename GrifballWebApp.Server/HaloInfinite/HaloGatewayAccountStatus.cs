namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>
/// What the Halo gateway says about its Halo account. <see cref="Status"/> is the status word its <c>/health</c> answers
/// (its <c>halo_auth</c> check): <c>Healthy</c> while the account is signed in, <c>Degraded</c> once an admin has to sign
/// in again at the gateway's admin page. <see cref="Reachable"/> false: no answer at all.
/// </summary>
public sealed record HaloGatewayAccount(bool Reachable, string? Status, string? Error)
{
    public bool Healthy => Reachable && string.Equals(Status, "Healthy", StringComparison.OrdinalIgnoreCase);

    public static HaloGatewayAccount Unreachable(string error) => new(false, null, error);
}

/// <summary>Asks the Halo gateway about its Halo account.</summary>
public interface IHaloGatewayAccountStatus
{
    /// <summary>Never throws for a gateway that is down or answers oddly; only when <paramref name="cancellationToken"/> is cancelled.</summary>
    Task<HaloGatewayAccount> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the gateway's <c>/health</c>, which needs no API key and answers only a status word: Healthy, Degraded (still
/// HTTP 200) or Unhealthy (503). The HttpClient is <see cref="HaloInfiniteServiceExtensions.GatewayStatusClientName"/>,
/// whose base address is <c>HaloGateway:BaseUrl</c>.
/// </summary>
public sealed class HaloGatewayAccountStatus(IHttpClientFactory httpClientFactory, ILogger<HaloGatewayAccountStatus> logger) : IHaloGatewayAccountStatus
{
    private const int MaxStatusLength = 32;

    public async Task<HaloGatewayAccount> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(HaloInfiniteServiceExtensions.GatewayStatusClientName);
            using var response = await client.GetAsync("health", cancellationToken);
            var code = (int)response.StatusCode;
            var text = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (text.Length > MaxStatusLength)
                text = text[..MaxStatusLength];

            // 200 (Healthy or Degraded) and 503 (Unhealthy) both carry the status word.
            return code is 200 or 503 && text.Length > 0
                ? new HaloGatewayAccount(true, text, null)
                : new HaloGatewayAccount(true, null, $"gateway /health answered {code}");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Halo gateway health request failed");
            return HaloGatewayAccount.Unreachable(ex.GetType().Name);
        }
    }
}
