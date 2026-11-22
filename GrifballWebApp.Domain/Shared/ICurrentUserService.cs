using System.Security.Claims;

namespace GrifballWebApp.Domain.Shared;

/// <summary>The app user a change is made by, for the audit columns (AuditInterceptor). Implemented by the database
/// layer's CurrentUserService.</summary>
public interface ICurrentUserService
{
    void SetCurrentUserIdFromClaims(ClaimsPrincipal? user);
    void SetCurrentUserId(int userId);
    int? GetCurrentUserId();

    /// <summary>
    /// Whether the current request signed in with a personal access token (<see cref="AccessTokenClaimTypes.TokenId"/>),
    /// rather than the browser's own cookie: the one fact every merge path (MCP, historical import, a gamertag set onto
    /// a placeholder, the REST endpoints) checks before merging or undoing a merge, so a leaked token can never do either,
    /// however it reached the merge (#549). False outside a request (a job, the Discord bot) and whenever there is none.
    /// </summary>
    bool IsAccessToken { get; }
}

/// <summary>
/// The claim types a personal access token's sign-in adds to the principal (<c>GrifballWebApp.Server.Mcp.AccessTokenAuthentication</c>),
/// named here so the database and Application layers can tell a token-authenticated request apart (<see cref="ICurrentUserService.IsAccessToken"/>)
/// without referencing the Server project, which owns the scheme itself.
/// </summary>
public static class AccessTokenClaimTypes
{
    /// <summary>The token's own ID, never its secret.</summary>
    public const string TokenId = "grif:access_token_id";

    /// <summary>
    /// Whether <paramref name="user"/> was signed in by a personal access token, whatever its scope: the one check,
    /// shared by <c>AccessTokenAuthentication.IsAccessToken</c> (an alias kept there for the code that already names
    /// it that way) and <see cref="ICurrentUserService.IsAccessToken"/>'s implementation, instead of each testing the
    /// claim itself.
    /// </summary>
    public static bool IsAccessToken(ClaimsPrincipal user) => user.HasClaim(c => c.Type == TokenId);
}
