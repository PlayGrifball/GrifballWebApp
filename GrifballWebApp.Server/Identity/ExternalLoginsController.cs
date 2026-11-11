using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GrifballWebApp.Server.Identity;

/// <summary>
/// Linking external logins (e.g. Discord) to an existing account, and removing them.
/// </summary>
/// <remarks>
/// Linking starts with a full-page navigation to the provider, which must not act on the session cookie alone (another site could
/// start one for a signed-in visitor). Linking is therefore:
/// <list type="number">
/// <item><see cref="StartLink"/> (signed in, with the antiforgery token) returns a short-lived token protected with Data Protection that names the signed-in user and the provider.</item>
/// <item>The browser navigates to <see cref="Link"/> with it. The server unprotects it (never trusting a user id from the query string)
/// and challenges the provider with that user id stored as the XSRF key in the authentication properties.</item>
/// <item>The provider returns to /signin-discord, which stores the external cookie and redirects to the profile page.</item>
/// <item>The profile page calls <see cref="LinkCallback"/> (signed in). <c>GetExternalLoginInfoAsync(userId)</c> only returns the login
/// when the XSRF key matches the signed-in user, so the login is added to the account that started the link and nobody else.</item>
/// </list>
/// </remarks>
[Authorize]
[ApiController]
public class ExternalLoginsController : ControllerBase
{
    public const string LinkTokenPurpose = "GrifballWebApp.ExternalLogins.Link";
    public static readonly TimeSpan LinkTokenLifetime = TimeSpan.FromMinutes(5);

    private readonly SignInManager<User> _signInManager;
    private readonly IExternalLoginService _externalLoginService;
    private readonly ITimeLimitedDataProtector _linkTokenProtector;

    public ExternalLoginsController(SignInManager<User> signInManager, IExternalLoginService externalLoginService, IDataProtectionProvider dataProtectionProvider)
    {
        _signInManager = signInManager;
        _externalLoginService = externalLoginService;
        _linkTokenProtector = dataProtectionProvider.CreateProtector(LinkTokenPurpose).ToTimeLimitedDataProtector();
    }

    /// <summary>The external providers this server is configured for.</summary>
    [AllowAnonymous]
    [HttpGet("external-logins/providers", Name = "GetExternalProviders")]
    [ProducesResponseType<List<ExternalProviderDto>>(StatusCodes.Status200OK)]
    public async Task<List<ExternalProviderDto>> Providers()
    {
        var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        return schemes
            .Select(s => new ExternalProviderDto { Name = s.Name, DisplayName = s.DisplayName ?? s.Name })
            .ToList();
    }

    /// <summary>The signed-in user's external logins, whether they have a password, and the providers they can link.</summary>
    [ProducesResponseType(typeof(LinkedLoginsDto), StatusCodes.Status200OK)]
    [HttpGet("me/external-logins", Name = "GetMyExternalLogins")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Mine()
    {
        if (GetUserID() is not int userId)
            return Unauthorized();

        var logins = await _externalLoginService.GetLogins(userId);
        if (logins is null)
            return this.NotFoundProblem("User not found");

        logins.Providers = await Providers();
        return Ok(logins);
    }

    /// <summary>Starts linking a provider to the signed-in user. Returns the token for <see cref="Link"/>.</summary>
    [ProducesResponseType(typeof(StartLinkResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [HttpPost("me/external-logins/link-token", Name = "StartExternalLoginLink")]
    public async Task<IActionResult> StartLink([FromQuery] string provider)
    {
        if (GetUserID() is not int userId)
            return Unauthorized();

        if (!await IsConfiguredProvider(provider))
            return this.BadRequestProblem($"Unknown login provider '{provider}'");

        var token = _linkTokenProtector.Protect(userId + ":" + provider, LinkTokenLifetime);
        return Ok(new StartLinkResponseDto { Token = token });
    }

    /// <summary>Full-page navigation target: sends the browser to the provider to link it to the user named in the token.</summary>
    [AllowAnonymous]
    [HttpGet("external-logins/link", Name = "ExternalLoginLink")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Link([FromQuery] string provider, [FromQuery] string token)
    {
        string payload;
        try
        {
            payload = _linkTokenProtector.Unprotect(token);
        }
        catch (CryptographicException)
        {
            return this.BadRequestProblem("This link request is invalid or has expired. Start again from your profile.");
        }

        var parts = payload.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var userId) || parts[1] != provider)
            return this.BadRequestProblem("This link request is invalid or has expired. Start again from your profile.");

        if (!await IsConfiguredProvider(provider))
            return this.BadRequestProblem($"Unknown login provider '{provider}'");

        // Relative like ExternalLogin's "login?callback=true": the provider callback (/signin-discord) redirects here.
        var redirectUrl = $"profile/{userId}?linkCallback=true";
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, userId.ToString());
        return Challenge(properties, provider);
    }

    /// <summary>Finishes linking: adds the external login from the provider round trip to the signed-in user.</summary>
    [ProducesResponseType(typeof(ExternalLoginDto), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [HttpPost("me/external-logins", Name = "ExternalLoginLinkCallback")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> LinkCallback()
    {
        if (GetUserID() is not int userId)
            return Unauthorized();

        // Passing the user id makes Identity check it against the XSRF key stored when the link started,
        // so a login fetched for one account cannot be added to another.
        var info = await _signInManager.GetExternalLoginInfoAsync(userId.ToString());
        if (info is null)
            return this.BadRequestProblem("The link request expired or was started by a different account. Try again.");

        // The external cookie has done its job; drop it so it cannot be reused.
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var displayName = info.ProviderDisplayName ?? info.LoginProvider;
        return await _externalLoginService.Link(userId, info) switch
        {
            LinkLoginResult.Linked or LinkLoginResult.AlreadyLinked => Ok(new ExternalLoginDto
            {
                LoginProvider = info.LoginProvider,
                ProviderKey = info.ProviderKey,
                ProviderDisplayName = displayName,
            }),
            LinkLoginResult.LinkedToAnotherUser => this.ConflictProblem($"This {displayName} account is already linked to another Grifball account. Ask an admin to merge the two accounts."),
            LinkLoginResult.UserNotFound => this.NotFoundProblem("User not found"),
            _ => this.BadRequestProblem($"Failed to link {displayName}"),
        };
    }

    /// <summary>Removes one of the signed-in user's external logins. Refuses the last way to sign in.</summary>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [HttpPost("me/external-logins/remove", Name = "UnlinkExternalLogin")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Unlink([FromBody] ExternalLoginKeyDto login)
    {
        if (GetUserID() is not int userId)
            return Unauthorized();

        return ToResult(await _externalLoginService.Unlink(userId, login.LoginProvider, login.ProviderKey),
            "This is your only way to sign in. Link another provider or set a password first.");
    }

    /// <summary>Sysadmin: a user's external logins.</summary>
    [Authorize(Roles = "Sysadmin")]
    [ProducesResponseType(typeof(LinkedLoginsDto), StatusCodes.Status200OK)]
    [HttpGet("users/{userID:int}/external-logins", Name = "GetUserExternalLogins")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UserLogins([FromRoute] int userID)
    {
        var logins = await _externalLoginService.GetLogins(userID);
        return logins is null ? this.NotFoundProblem("User not found") : Ok(logins);
    }

    /// <summary>Sysadmin: removes one of a user's external logins. Refuses the user's last way to sign in.</summary>
    [Authorize(Roles = "Sysadmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [HttpPost("users/{userID:int}/external-logins/remove", Name = "RemoveUserExternalLogin")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> RemoveUserLogin([FromRoute] int userID, [FromBody] ExternalLoginKeyDto login)
    {
        return ToResult(await _externalLoginService.Unlink(userID, login.LoginProvider, login.ProviderKey),
            "This is the user's only way to sign in. Give them a password reset link first, or merge the account instead.");
    }

    private IActionResult ToResult(UnlinkLoginResult result, string lastMethodMessage) => result switch
    {
        UnlinkLoginResult.Removed => NoContent(),
        UnlinkLoginResult.LastSignInMethod => this.BadRequestProblem(lastMethodMessage),
        UnlinkLoginResult.LoginNotFound => this.NotFoundProblem("Login not found"),
        UnlinkLoginResult.UserNotFound => this.NotFoundProblem("User not found"),
        _ => this.BadRequestProblem("Failed to remove the login"),
    };

    private async Task<bool> IsConfiguredProvider(string provider)
    {
        var schemes = await _signInManager.GetExternalAuthenticationSchemesAsync();
        return schemes.Any(s => s.Name == provider);
    }

    private int? GetUserID()
    {
        var stringName = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (stringName == null)
            return null;
        var parsed = int.TryParse(stringName, out var id);
        return parsed ? id : null;
    }
}

public class StartLinkResponseDto
{
    public required string Token { get; set; }
}
