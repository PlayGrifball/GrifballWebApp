using System.Text.Encodings.Web;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// Signs a request in by a personal access token (<see cref="AccessTokenService"/>) in its Authorization header, as the user
/// who made the token, with their roles as they are now. Only the MCP endpoint asks for this scheme (<see cref="GrifMcp"/>):
/// the API's default scheme (<see cref="BrowserAuth.Scheme"/>) doesn't know these tokens, so they open nothing else, and a
/// browser's cookie doesn't open the MCP endpoint.
/// </summary>
public class AccessTokenAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AccessToken";

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

        var user = await _tokens.FindUser(token, Context.RequestAborted);
        if (user is null)
            return AuthenticateResult.Fail("The access token is unknown, revoked or expired.");
        if (await _signIn.UserManager.IsLockedOutAsync(user))
            return AuthenticateResult.Fail("The account is locked out.");

        var principal = await _signIn.CreateUserPrincipalAsync(user);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }
}
