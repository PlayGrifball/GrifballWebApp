namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>How the app reaches Halo Infinite (<c>HaloInfinite:Mode</c>).</summary>
public enum HaloInfiniteMode
{
    /// <summary>
    /// The default: every Halo call goes through the Halo gateway (Grunt's <c>Grunt.Gateway.Host</c>), which holds the one
    /// signed-in Halo account for every environment. The app needs only the gateway's address and its own API key.
    /// </summary>
    Gateway,

    /// <summary>
    /// The app signs in to Halo itself with its own Microsoft app (<c>ClientConfiguration:ClientId</c>, <c>ClientSecret</c>,
    /// <c>RedirectUrl</c>) and keeps the tokens in its working directory, set up from the Infinite Client page.
    /// </summary>
    Direct,
}

/// <summary>
/// The Halo Infinite settings read at startup: <c>HaloInfinite:Mode</c> (<see cref="HaloInfiniteMode.Gateway"/> unless set)
/// and, for the gateway, <c>HaloGateway:BaseUrl</c>, <c>HaloGateway:ApiKey</c> (sent as <c>X-Api-Key</c>) and
/// <c>HaloGateway:AdminUrl</c> (where the gateway's Halo account is signed in, shown on the Infinite Client page).
/// </summary>
public sealed class HaloInfiniteSettings
{
    public const string ModeKey = "HaloInfinite:Mode";
    public const string GatewaySection = "HaloGateway";
    public const string BaseUrlKey = GatewaySection + ":BaseUrl";
    public const string ApiKeyKey = GatewaySection + ":ApiKey";
    public const string AdminUrlKey = GatewaySection + ":AdminUrl";

    /// <summary>The gateway's Service in the cluster it runs in.</summary>
    public const string DefaultGatewayBaseUrl = "http://halo-gateway.halo-gateway.svc:8080/";

    /// <summary>The gateway's admin page, where its Halo account is signed in.</summary>
    public const string DefaultGatewayAdminUrl = "https://grunt.grifball.xyz/admin";

    public HaloInfiniteSettings(HaloInfiniteMode mode, Uri gatewayBaseUrl, string? gatewayApiKey, string gatewayAdminUrl)
    {
        Mode = mode;
        GatewayBaseUrl = gatewayBaseUrl;
        GatewayApiKey = gatewayApiKey;
        GatewayAdminUrl = gatewayAdminUrl;
    }

    public HaloInfiniteMode Mode { get; }

    /// <summary>Always ends with '/', so relative routes keep any path it has.</summary>
    public Uri GatewayBaseUrl { get; }

    /// <summary>Secret: never logged or returned by the API.</summary>
    public string? GatewayApiKey { get; }

    public string GatewayAdminUrl { get; }

    public bool IsGateway => Mode == HaloInfiniteMode.Gateway;

    /// <summary>Not the key: settings objects end up in logs.</summary>
    public override string ToString() => IsGateway
        ? $"Halo Infinite through the gateway at {GatewayBaseUrl} (API key {(string.IsNullOrEmpty(GatewayApiKey) ? "not set" : "set")})"
        : "Halo Infinite direct (the app's own sign-in)";

    /// <summary>
    /// Reads the settings. A blank mode is <see cref="HaloInfiniteMode.Gateway"/>; anything other than Gateway or Direct
    /// (any case), or a gateway URL that isn't an absolute http(s) URL, stops startup with the setting named.
    /// </summary>
    public static HaloInfiniteSettings FromConfiguration(IConfiguration configuration)
    {
        var modeText = configuration[ModeKey]?.Trim();
        var mode = HaloInfiniteMode.Gateway;
        if (!string.IsNullOrEmpty(modeText)
            && (!Enum.TryParse(modeText, ignoreCase: true, out mode) || !Enum.IsDefined(mode)))
        {
            throw new InvalidOperationException($"{ModeKey} must be Gateway or Direct, not '{modeText}'");
        }

        var baseText = configuration[BaseUrlKey]?.Trim();
        if (string.IsNullOrEmpty(baseText))
            baseText = DefaultGatewayBaseUrl;
        if (!Uri.TryCreate(baseText, UriKind.Absolute, out var baseUrl) || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException($"{BaseUrlKey} must be an absolute http or https URL, not '{baseText}'");
        if (!baseUrl.AbsolutePath.EndsWith('/'))
            baseUrl = new Uri(baseUrl.GetLeftPart(UriPartial.Path) + "/");

        var apiKey = configuration[ApiKeyKey]?.Trim();
        var adminUrl = configuration[AdminUrlKey]?.Trim();

        return new HaloInfiniteSettings(mode, baseUrl, string.IsNullOrEmpty(apiKey) ? null : apiKey,
            string.IsNullOrEmpty(adminUrl) ? DefaultGatewayAdminUrl : adminUrl);
    }
}
