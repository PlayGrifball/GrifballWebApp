using System.Security.Claims;
using System.Text.Encodings.Web;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// Signs a request in by a personal access token (<see cref="AccessTokenService"/>) in its Authorization header, as the user
/// who made the token, with their roles as they are now. Both the MCP endpoint (<see cref="GrifMcp"/>) and, since a token may
/// have <see cref="AccessTokenScope.Api"/> or <see cref="AccessTokenScope.Both"/>, the REST API's default scheme
/// (<see cref="BrowserAuth.SelectScheme"/>) ask for this scheme; a browser's cookie never does. A token whose scope doesn't
/// include the surface it was sent to fails with a reason instead of signing in (<see cref="GrifMcp.IsMcpRequest"/>).
/// </summary>
public class AccessTokenAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AccessToken";

    /// <summary>The token's own ID, on a principal it signed in: never its secret (<see cref="IsAccessToken"/>, the request's logs).</summary>
    public const string TokenIdClaimType = "grif:access_token_id";
    /// <summary>The token's own name, on a principal it signed in.</summary>
    public const string TokenNameClaimType = "grif:access_token_name";
    /// <summary>Whether the token is read-only, on a principal it signed in: "true" or "false".</summary>
    public const string ReadOnlyClaimType = "grif:access_token_readonly";

    private readonly AccessTokenService _tokens;
    private readonly SignInManager<User> _signIn;

    public AccessTokenAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        AccessTokenService tokens, SignInManager<User> signIn)
        : base(options, logger, encoder)
    {
        _tokens = tokens;
        _signIn = signIn;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!BrowserAuth.HasBearerToken(Request))
            return AuthenticateResult.NoResult();
        var token = Request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();

        var authenticated = await _tokens.Authenticate(token, Context.RequestAborted);
        if (authenticated is null)
            return AuthenticateResult.Fail("The access token is unknown, revoked or expired.");
        if (await _signIn.UserManager.IsLockedOutAsync(authenticated.User))
            return AuthenticateResult.Fail("The account is locked out.");

        var onMcp = GrifMcp.IsMcpRequest(Context);
        if (onMcp && !authenticated.Scope.AllowsMcp())
            return AuthenticateResult.Fail("The access token is for the REST API only: it does not open the MCP server.");
        if (!onMcp && !authenticated.Scope.AllowsApi())
            return AuthenticateResult.Fail("The access token is for the MCP server only: it does not open the REST API.");

        var principal = await _signIn.CreateUserPrincipalAsync(authenticated.User);
        ((ClaimsIdentity)principal.Identity!).AddClaims(
        [
            new Claim(TokenIdClaimType, authenticated.ID),
            new Claim(TokenNameClaimType, authenticated.Name),
            new Claim(ReadOnlyClaimType, authenticated.ReadOnly ? "true" : "false"),
        ]);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    /// <summary>
    /// A 401 with the reason <see cref="HandleAuthenticateAsync"/> failed for (unknown token, locked account, or the wrong
    /// surface for the token's scope), not just an empty status: the default base implementation writes no body.
    /// </summary>
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var authenticated = await HandleAuthenticateOnceSafeAsync();
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = authenticated?.Failure?.Message ?? "Not signed in.",
        }, options: null, contentType: "application/problem+json");
    }

    /// <summary>Whether <paramref name="user"/> was signed in by an access token (<see cref="TokenIdClaimType"/>), whatever its scope.</summary>
    public static bool IsAccessToken(ClaimsPrincipal user) => user.HasClaim(c => c.Type == TokenIdClaimType);

    /// <summary>Whether the access token that signed <paramref name="user"/> in is read-only; false for anyone else.</summary>
    public static bool IsReadOnly(ClaimsPrincipal user) => string.Equals(user.FindFirstValue(ReadOnlyClaimType), "true", StringComparison.Ordinal);

    /// <summary>GET and HEAD: what a read-only token may still do on the REST API (MCP's own tools say read-only differently).</summary>
    public static bool IsReadMethod(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

    /// <summary>
    /// Middleware: refuses a write (not <see cref="IsReadMethod"/>) from a read-only token's user with 403, saying why. Skips the
    /// MCP endpoint, whose own tool-call filter enforces read-only by tool, not by HTTP method (every MCP call is a POST).
    /// </summary>
    public static Task RefuseWritesFromReadOnlyTokens(HttpContext context, RequestDelegate next)
    {
        if (GrifMcp.IsMcpRequest(context) || IsReadMethod(context.Request.Method) || !IsReadOnly(context.User))
            return next(context);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "The access token is read-only.",
        }, options: null, contentType: "application/problem+json");
    }
}
