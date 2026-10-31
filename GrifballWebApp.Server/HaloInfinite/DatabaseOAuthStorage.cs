using Surprenant.Grunt.Core.Storage;
using Surprenant.Grunt.Models;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>Whether this process owns the Halo Infinite sign-in: exchanges the authorization code and refreshes the token.</summary>
/// <param name="OwnsRefresh">
/// True in the roles that run the background work (<see cref="HaloTokenRefreshService"/>); false in the API role, which
/// only reads the token the owner keeps fresh.
/// </param>
public sealed record HaloTokenOwnership(bool OwnsRefresh);

/// <summary>
/// Grunt's <see cref="IOAuthStorage"/> on <see cref="IHaloTokenStore"/>. A process that doesn't own the sign-in
/// (<see cref="HaloTokenOwnership"/>) never gets the refresh token, so it can't refresh the token itself: an expired token
/// reads as none, and Grunt then asks <see cref="DatabaseAccountAuthorization"/> for a code, which refuses. The owner
/// refreshes it well before then.
/// </summary>
public sealed class DatabaseOAuthStorage(IHaloTokenStore store, HaloTokenOwnership ownership, TimeProvider time, ILogger<DatabaseOAuthStorage> logger) : IOAuthStorage
{
    public async Task<OAuthToken?> GetToken()
    {
        var stored = await store.GetTokenAsync();
        if (stored is null || ownership.OwnsRefresh)
            return stored?.Token;

        if (stored.ExpiresAt is { } expiresAt && expiresAt <= time.GetUtcNow().UtcDateTime)
        {
            logger.LogWarning("The stored Halo Infinite token expired at {ExpiresAt} and has not been refreshed yet (the worker refreshes it)", expiresAt);
            return null;
        }

        return new OAuthToken
        {
            TokenType = stored.Token.TokenType,
            ExpiresIn = stored.Token.ExpiresIn,
            Scope = stored.Token.Scope,
            AccessToken = stored.Token.AccessToken,
            RefreshToken = string.Empty,
            UserId = stored.Token.UserId,
        };
    }

    public Task SetToken(OAuthToken token) => store.SetTokenAsync(token);
}

/// <summary>
/// Grunt's <see cref="IAccountAuthorization"/> on <see cref="IHaloTokenStore"/>: the admin's code (Admin/SetCode) is stored
/// for whichever process owns the sign-in, and only that one takes it.
/// </summary>
public sealed class DatabaseAccountAuthorization(IHaloTokenStore store, HaloTokenOwnership ownership, ILogger<DatabaseAccountAuthorization> logger) : IAccountAuthorization
{
    public async Task<string> GetCodeAsync()
    {
        if (!ownership.OwnsRefresh)
            throw new InvalidOperationException("No valid Halo Infinite token: the worker signs in and refreshes it. Contact admin if this lasts");

        if (await store.TakeCodeAsync() is { } code)
            return code;

        // As Grunt's file-based one says (Admin/CheckStatus gives the sign-in link).
        logger.LogError("Admin must provide AccountAuthorization (Admin, Check status, gives the sign-in link)");
        throw new Exception("Missing AccountAuthorization. Contact admin");
    }

    public Task SetCodeAsync(string code) => store.SetCodeAsync(code);
}
