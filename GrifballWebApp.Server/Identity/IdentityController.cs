using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.UserManagement;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text;

namespace GrifballWebApp.Server.Identity;

[Route("[controller]/[action]")]
[ApiController]
public class IdentityController : ControllerBase
{
    private readonly ILogger<IdentityController> _logger;
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IOptionsMonitor<BearerTokenOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly IUserManagementService _userManagementService;
    
    public IdentityController(ILogger<IdentityController> logger, UserManager<User> userManager, SignInManager<User> signInManager,
        IOptionsMonitor<BearerTokenOptions> optionsMonitor, TimeProvider timeProvider, IUserManagementService userManagementService)
    {
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider;
        _userManagementService = userManagementService;
    }

    /// <summary>
    /// Signs in with a password. A browser gets the session cookie (<see cref="BrowserAuth.CookieName"/>) and an
    /// empty 200. With <paramref name="useCookies"/> false the answer is a bearer token instead
    /// (<see cref="AccessTokenResponse"/>), for clients that are not browsers.
    /// </summary>
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [HttpPost(Name = "Login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken ct, [FromQuery] bool useCookies = true)
    {
        if (string.IsNullOrEmpty(loginDto?.Username))
            return BadRequest("Username is required");

        if (string.IsNullOrEmpty(loginDto?.Password))
            return BadRequest("Password is required");

        _signInManager.AuthenticationScheme = useCookies ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

        var result = await _signInManager.PasswordSignInAsync(loginDto.Username, loginDto.Password, true, lockoutOnFailure: true);
        //var foo = await _signInManager.

        if (result.Succeeded)
        {
            return Empty;
            //return TypedResults.Empty;
        }
        else
        {
            return Unauthorized();
        }
    }

    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpPost(Name = "Register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(registerDto?.Username))
            return BadRequest("Username is required");

        if (string.IsNullOrEmpty(registerDto?.Password))
            return BadRequest("Password is required");

        var user = new User()
        {
            UserName = registerDto.Username,
        };
        var result = await _userManager.CreateAsync(user, registerDto.Password);

        if (!result.Succeeded)
        {
            var sb = new StringBuilder();
            foreach (var error in result.Errors)
            {
                sb.Append(error.Code);
                sb.Append(": ");
                sb.AppendLine(error.Description);
            }
            return BadRequest(sb.ToString());
        }

        result = await _userManager.AddToRoleAsync(user, "Player");

        if (!result.Succeeded)
        {
            return BadRequest("Failed to add role");
        }

        return Ok();
    }

    /// <summary>
    /// Leaves for Discord's sign-in, which comes back to the login page (<c>/login?callback=true</c>), and that
    /// page calls <see cref="ExternalLoginCallback"/>. <paramref name="followUp"/>, the page to go on to, is kept
    /// only if it is a path on this site (<see cref="IsAppRelativePath"/>), escaped as one query value.
    /// </summary>
    [HttpGet(Name = "ExternalLogin")]
    public IActionResult ExternalLogin([FromQuery] string? followUp)
    {
        var provider = "Discord";
        var redirectUrl = "login?callback=true";
        if (IsAppRelativePath(followUp))
            redirectUrl += "&followUp=" + Uri.EscapeDataString(followUp!);
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    /// <summary>
    /// A path on this site ("/season/3?tab=bracket"): starts with a single '/', not "//" or "/\" (which browsers
    /// read as another host), and has no control characters.
    /// </summary>
    public static bool IsAppRelativePath(string? path)
        => !string.IsNullOrEmpty(path)
            && path[0] == '/'
            && (path.Length == 1 || (path[1] != '/' && path[1] != '\\'))
            && !path.Any(char.IsControl);

    /// <summary>
    /// Finishes a Discord sign-in: the session cookie, or with <paramref name="useCookies"/> false a bearer token.
    /// A POST, as it creates the user and signs in: the browser comes back from Discord to the login page (a page
    /// of the app, not this action), which then calls this with the antiforgery token like any other change.
    /// </summary>
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [HttpPost(Name = "ExternalLoginCallback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] bool useCookies = true, CancellationToken ct = default)
    {
        var info = await _signInManager.GetExternalLoginInfoAsync();

        if (info == null)
        {
            return BadRequest("Missing info");
        }
        
        _signInManager.AuthenticationScheme = useCookies ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            return Empty;
        }
        else if (signInResult.IsLockedOut)
        {
            return BadRequest("Locked");
        }
        else if (signInResult.RequiresTwoFactor)
        {
            return BadRequest("Requires 2FA");
        }
        else if (signInResult.IsNotAllowed)
        {
            return BadRequest("Not allowed");
        }
        else
        {
            var p = info.Principal;
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrEmpty(email))
            {
                var existing = await _userManager.FindByEmailAsync(email!);

                if (existing is not null)
                {
                    return BadRequest("Already a user");
                }
            }

            var name = info.Principal.FindFirstValue(ClaimTypes.Name);
            if (!string.IsNullOrEmpty(name))
            {
                var existing = await _userManager.FindByNameAsync(name!);

                if (existing is not null)
                {
                    return BadRequest("Already a user");
                }
            }
            else
            {
                throw new Exception("Missing name");
            }

            var user = new User()
            {
                UserName = name,
                Email = email,
            };
            var result = await _userManager.CreateAsync(user);

            if (result.Succeeded)
            {
                result = await _userManager.AddToRoleAsync(user, "Player");

                if (!result.Succeeded)
                {
                    return BadRequest("Failed to add role");
                }

                result = await _userManager.AddLoginAsync(user, info);

                if (!result.Succeeded)
                {
                    return BadRequest("Failed to add login");
                }

                var claims = p.Claims;
                if (claims.Any())
                {
                    await _userManager.AddClaimsAsync(user, claims);
                }


                // Done with Discord's half-finished sign-in (ExternalLoginSignInAsync drops it on the other branch).
                // Before signing in: a bearer sign-in writes the response, after which no cookie can be cleared.
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                await _signInManager.SignInAsync(user, isPersistent: true);
                // For a bearer token, SignInAsync has already written the token response
                return Empty;
            }

            var errors = new StringBuilder();
            foreach (var error in result.Errors)
            {
                errors.AppendFormat("{0}: {1}", error.Code, error.Description);
                errors.AppendLine();
            }

            return BadRequest($"Errors: {errors.ToString()}");
        }
    }

    /// <summary>A new bearer token for a refresh token (bearer clients only; browsers have the cookie).</summary>
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [HttpPost(Name = "Refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest)
    {
        var refreshTokenProtector = _optionsMonitor.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

        if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                _timeProvider.GetUtcNow() >= expiresUtc ||
                await _signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not User user)

        {
            return Challenge();
        }

        var newPrincipal = await _signInManager.CreateUserPrincipalAsync(user);
        return SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    [ProducesResponseType(typeof(MetaInfoResponse), StatusCodes.Status200OK)]
    [Authorize]
    [HttpGet(Name = "MetaInfo")]
    public MetaInfoResponse MetaInfo()
    {
        
        return new MetaInfoResponse()
        {
            IsSysAdmin = User.IsInRole("Sysadmin"),
            IsCommissioner = User.IsInRole("Commissioner"),
            IsPlayer = User.IsInRole("Player"),
            DisplayName = User?.Identity?.Name ?? "Friend",
            UserID = GetUserID() ?? 0,
        };
    }

    /// <summary>
    /// Who is signed in, for the client (in the browser, and on the server while it renders a page with the
    /// visitor's cookie): <see cref="MetaInfo"/>, or 204 No Content for a visitor. Unlike MetaInfo, a visitor is
    /// not an error, so the client asks on every first load without logging a failed request.
    /// </summary>
    [ProducesResponseType(typeof(MetaInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet(Name = "Me")]
    public MetaInfoResponse? Me()
        => User.Identity?.IsAuthenticated is true ? MetaInfo() : null;

    /// <summary>
    /// The antiforgery request token, which the browser sends in <see cref="BrowserAuth.AntiforgeryHeader"/> with
    /// every change (<see cref="CookieAntiforgeryFilter"/>). Also sets the antiforgery cookie it pairs with. The
    /// token belongs to the user signed in when it was made, so the client asks again after signing in or out.
    /// </summary>
    [ProducesResponseType(typeof(AntiforgeryTokenResponse), StatusCodes.Status200OK)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet(Name = "Antiforgery")]
    public AntiforgeryTokenResponse Antiforgery([FromServices] IAntiforgery antiforgery)
        => new() { Token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken! };

    /// <summary>
    /// Ends the browser's session: clears the session cookie (and any half-finished Discord sign-in). No
    /// antiforgery token needed: signing someone out is all a forged request could do, and SameSite=Lax already
    /// keeps the cookie off cross-site POSTs. Also changes the user's security stamp, so a copy of the cookie (and
    /// the user's other sessions) stops working within <see cref="BrowserAuth.SecurityStampValidationInterval"/>.
    /// </summary>
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [IgnoreAntiforgeryToken]
    [HttpPost(Name = "Logout")]
    public async Task<IActionResult> Logout()
    {
        if (User?.Identity?.IsAuthenticated is true && await _userManager.GetUserAsync(User) is { } user)
            await _userManager.UpdateSecurityStampAsync(user);
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [HttpPost(Name = "ResetPassword")]
    public async Task<IActionResult> ResetPassword([FromBody] UsePasswordResetLinkRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest("Token is required");

        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest("New password is required");

        var (success, message) = await _userManagementService.UsePasswordResetLink(request.Token, request.NewPassword, ct);

        if (!success)
            return BadRequest(message);

        return Ok(message);
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
