namespace GrifballWebApp.Database.Models;

/// <summary>
/// A secret of the Halo Infinite sign-in (Grunt): the account's OAuth token, or the one-time authorization code an
/// admin pasted in (<see cref="OAuthTokenName"/>, <see cref="AuthorizationCodeName"/>). <see cref="ProtectedValue"/> is
/// encrypted with ASP.NET Data Protection, whose keys are in <see cref="GrifballContext.DataProtectionKeys"/>, so every
/// backend process can read it and the database alone gives nothing away.
/// </summary>
public class HaloInfiniteCredential
{
    /// <summary>The account's OAuth token (Grunt's tokens.json): access and refresh token.</summary>
    public const string OAuthTokenName = "OAuthToken";

    /// <summary>The one-time authorization code (Grunt's AccountAuthorization.txt), until it is exchanged for a token.</summary>
    public const string AuthorizationCodeName = "AuthorizationCode";

    public string Name { get; set; } = null!;

    public string ProtectedValue { get; set; } = null!;

    /// <summary>When the OAuth access token expires, if known (a token imported from tokens.json is not).</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
