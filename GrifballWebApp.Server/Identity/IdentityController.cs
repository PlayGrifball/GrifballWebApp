﻿using GrifballWebApp.Server.Dtos;
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
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IOptionsMonitor<BearerTokenOptions> _optionsMonitor;
    private readonly TimeProvider _timeProvider;
    public IdentityController(ILogger<IdentityController> logger, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager,
        IOptionsMonitor<BearerTokenOptions> optionsMonitor, TimeProvider timeProvider)
    {
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
        _optionsMonitor = optionsMonitor;
        _timeProvider = timeProvider;
    }

    //[ProducesResponseType(typeof(string), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [HttpPost(Name = "Login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(loginDto?.Username))
            return BadRequest("Username is required");

        if (string.IsNullOrEmpty(loginDto?.Password))
            return BadRequest("Password is required");

        _signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

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

        var user = new IdentityUser()
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

        return Ok();
    }


    [HttpGet(Name = "ExternalLogin")]
    public IActionResult ExternalLogin()
    {
        var provider = "Discord";
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Identity");
        redirectUrl = "login?callback=true";
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    // TODO: delete me
    [Authorize(Roles = "Player")]
    //[Authorize]
    [HttpGet(Name = "Test")]
    public IActionResult Test(CancellationToken ct)
    {
        var a = User;
        var c = a.Claims.Any();
        return Ok(c);
    }

    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [HttpGet(Name = "ExternalLoginCallback")]
    public async Task<IActionResult> ExternalLoginCallback([FromQuery] bool useCookies, CancellationToken ct)
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

            var user = new IdentityUser()
            {
                UserName = name,
                Email = email,
            };
            var result = await _userManager.CreateAsync(user);

            if (result.Succeeded)
            {
                result = await _userManager.AddToRoleAsync(user, "Player");
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


                // Add role
                await _signInManager.SignInAsync(user, isPersistent: false);
                return Ok("Signed In");
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

    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [HttpPost(Name = "Refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest refreshRequest)
    {
        var refreshTokenProtector = _optionsMonitor.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

        if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                _timeProvider.GetUtcNow() >= expiresUtc ||
                await _signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not IdentityUser user)

        {
            return Challenge();
        }

        var newPrincipal = await _signInManager.CreateUserPrincipalAsync(user);
        return SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    //[ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    [Authorize]
    [HttpGet(Name = "MetaInfo")]
    public MetaInfoResponse MetaInfo()
    {
        return new MetaInfoResponse()
        {
            IsSysAdmin = User.IsInRole("SysAdmin"),
            IsCommissioner = User.IsInRole("Commissioner"),
            IsPlayer = User.IsInRole("Player"),
            DisplayName = User?.Identity?.Name ?? "Friend"
        };
    }
}
