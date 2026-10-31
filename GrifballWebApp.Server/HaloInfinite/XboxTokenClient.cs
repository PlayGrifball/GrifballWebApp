using Microsoft.Extensions.Options;
using Surprenant.Grunt.Authentication;
using Surprenant.Grunt.Models;
using System.Diagnostics.CodeAnalysis;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>The OAuth calls of the Halo Infinite sign-in, with the app's <see cref="ClientConfiguration"/>.</summary>
public interface IXboxTokenClient
{
    /// <summary>Exchanges a one-time authorization code for a token; null if refused.</summary>
    Task<OAuthToken?> RequestTokenAsync(string code);

    /// <summary>A new token for <paramref name="refreshToken"/>; null if refused.</summary>
    Task<OAuthToken?> RefreshTokenAsync(string refreshToken);
}

/// <summary>Grunt's <see cref="XboxAuthenticationClient"/>, which calls Microsoft's OAuth endpoint.</summary>
/// <remarks>Not covered: its only work is Microsoft's live endpoint, which Grunt calls with its own HttpClient.</remarks>
[ExcludeFromCodeCoverage]
public sealed class XboxTokenClient(IOptionsMonitor<ClientConfiguration> options) : IXboxTokenClient
{
    public Task<OAuthToken?> RequestTokenAsync(string code)
    {
        var config = options.CurrentValue;
        return new XboxAuthenticationClient().RequestOAuthToken(config.ClientId, code, config.RedirectUrl, config.ClientSecret);
    }

    public Task<OAuthToken?> RefreshTokenAsync(string refreshToken)
    {
        var config = options.CurrentValue;
        return new XboxAuthenticationClient().RefreshOAuthToken(config.ClientId, refreshToken, config.RedirectUrl, config.ClientSecret);
    }
}
