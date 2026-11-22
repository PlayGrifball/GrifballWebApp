using System.Security.Claims;
using System.Text.Encodings.Web;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
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
/// include the surface it was sent to fails with a reason instead of signing in (<see cref="GrifMcp.IsMcpRequest"/>). Hubs
/// are never a surface a token opens (<see cref="RefuseHubConnections"/> refuses the connection outright): a hub is a
/// browser feature, where the read-only and scope rules below don't apply, and a connection would outlive revoking the token.
/// </summary>
public class AccessTokenAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AccessToken";

    /// <summary>
    /// The token's own ID, on a principal it signed in: never its secret (<see cref="IsAccessToken"/>, the request's logs).
    /// The same string as <see cref="AccessTokenClaimTypes.TokenId"/>, which is what <see cref="ICurrentUserService.IsAccessToken"/>
    /// reads: one constant, aliased here so existing code naming <see cref="AccessTokenAuthentication"/> for it still compiles.
    /// </summary>
    public const string TokenIdClaimType = AccessTokenClaimTypes.TokenId;
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
        // RefuseHubConnections already refuses the request outright; this is the belt the braces are for.
        if (BrowserAuth.IsHubRequest(Context))
            return AuthenticateResult.NoResult();
        if (BrowserAuth.BearerToken(Context) is not { } token)
            return AuthenticateResult.NoResult();

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
        // HandleAuthenticateOnceSafeAsync never returns null, so only Failure's own absence (NoResult, no token at all) needs the fallback.
        var authenticated = await HandleAuthenticateOnceSafeAsync();
        await WriteProblemAsync(Context, StatusCodes.Status401Unauthorized, authenticated.Failure?.Message ?? "Not signed in.");
    }

    /// <summary>Whether <paramref name="user"/> was signed in by an access token (<see cref="TokenIdClaimType"/>), whatever its scope.</summary>
    public static bool IsAccessToken(ClaimsPrincipal user) => user.HasClaim(c => c.Type == TokenIdClaimType);

    /// <summary>Whether the access token that signed <paramref name="user"/> in is read-only; false for anyone else.</summary>
    public static bool IsReadOnly(ClaimsPrincipal user) => string.Equals(user.FindFirstValue(ReadOnlyClaimType), "true", StringComparison.Ordinal);

    /// <summary>GET and HEAD: what a read-only token may still do on the REST API (MCP's own tools say read-only differently).</summary>
    public static bool IsReadMethod(string method) => HttpMethods.IsGet(method) || HttpMethods.IsHead(method);

    /// <summary>
    /// Middleware: refuses a <c>grif_</c> access token's attempt to open a hub (negotiate or the connection) with 401, whatever
    /// its scope or read-only flag. Hubs are the browser's own: a read-only check by HTTP method means nothing to a persistent
    /// connection's calls, and a connection a token opened would stay signed in after the token is revoked.
    /// </summary>
    public static Task RefuseHubConnections(HttpContext context, RequestDelegate next)
    {
        if (!BrowserAuth.IsHubRequest(context) || BrowserAuth.AccessToken(context) is null)
            return next(context);
        return WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "Access tokens don't open hubs: those stay the browser's own session.");
    }

    /// <summary>
    /// Middleware: refuses a write (not <see cref="IsReadMethod"/>) from a read-only token's user with 403, saying why. Skips the
    /// MCP endpoint, whose own tool-call filter enforces read-only by tool, not by HTTP method (every MCP call is a POST).
    /// </summary>
    public static Task RefuseWritesFromReadOnlyTokens(HttpContext context, RequestDelegate next)
    {
        if (GrifMcp.IsMcpRequest(context) || IsReadMethod(context.Request.Method) || !IsReadOnly(context.User))
            return next(context);
        return WriteProblemAsync(context, StatusCodes.Status403Forbidden, "The access token is read-only.");
    }

    /// <summary>
    /// Middleware: a presented <c>grif_</c> token that fails to authenticate (unknown, revoked, expired, a locked account, or
    /// the wrong surface for its scope) is refused with 401 on every endpoint, even an <see cref="Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute"/>
    /// one. ASP.NET's own authorization (and so a requirement on its default or fallback policy) skips those entirely, which
    /// would otherwise let a dropped or wrong Authorization header silently fall back to an anonymous 200 instead of a 401
    /// (the docs' own example, an anonymous <c>GET /seasons</c>, with an MCP-only, revoked or expired token). Skips hubs,
    /// which <see cref="RefuseHubConnections"/> already refuses with its own, more specific reason. Calling
    /// <see cref="AuthenticationHttpContextExtensions.AuthenticateAsync(HttpContext, string)"/> again for this scheme reuses
    /// <see cref="HandleAuthenticateAsync"/>'s own cached result for the request (the handler instance is cached per scheme
    /// per request), so this costs no extra lookup when <c>UseAuthentication</c> already ran it.
    /// </summary>
    public static async Task RequireAValidToken(HttpContext context, RequestDelegate next)
    {
        if (BrowserAuth.IsHubRequest(context) || BrowserAuth.AccessToken(context) is null)
        {
            await next(context);
            return;
        }

        var result = await context.AuthenticateAsync(SchemeName);
        if (result.Succeeded)
        {
            await next(context);
            return;
        }

        // Never NoResult here: that only happens above (already excluded) or when BearerToken is null, and
        // BrowserAuth.AccessToken being non-null already means it isn't. Always a Fail with a reason.
        await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, result.Failure!.Message);
    }

    /// <summary>
    /// The 401/403 body for every refusal above, <see cref="HandleChallengeAsync"/> and <see cref="DenyAccessTokenFilter"/>,
    /// through <see cref="IProblemDetailsService"/> (registered once, <c>AddProblemDetails</c> in Program) instead of each
    /// writing its own JSON by hand, so they share whatever the app customises on every problem response (the trace ID).
    /// Internal, not private: the filter lives in a different file (same assembly) but the same rule.
    /// </summary>
    internal static async Task WriteProblemAsync(HttpContext context, int status, string title)
    {
        context.Response.StatusCode = status;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new ProblemDetails { Status = status, Title = title } });
    }
}
