using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.UserManagement;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.HaloInfinite;
using GrifballWebApp.Server.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Core.Storage;
using Surprenant.Grunt.Models;
using System.Collections.Specialized;
using System.Security.Claims;

namespace GrifballWebApp.Server.Controllers;
[Route("[controller]/[action]")]
[ApiController]
public class AdminController : ControllerBase
{
    /// <summary>What the Direct-only endpoints answer (409) in Gateway mode.</summary>
    public const string GatewayModeMessage =
        "HaloInfinite:Mode is Gateway: the Halo account is signed in centrally at the Halo gateway, not here. Set HaloInfinite:Mode to Direct to use this site's own sign-in.";

    private readonly ILogger<AdminController> _logger;
    private readonly IHaloInfiniteClient _haloInfiniteClient;
    private readonly HaloInfiniteSettings _haloSettings;
    private readonly HaloInfiniteHealthCheck _haloStatus;
    private readonly IAccountAuthorization? _accountAuthorization;
    private readonly IDataPullService _dataPullService;
    private readonly IOptionsMonitor<ClientConfiguration> _options;
    private readonly IUserManagementService _userManagementService;

    /// <param name="haloInfiniteClient">Every Halo call: the gateway client, or in Direct mode the factory.</param>
    /// <param name="accountAuthorization">Direct mode only (the app's own sign-in); not registered in Gateway mode.</param>
    public AdminController(ILogger<AdminController> logger, IHaloInfiniteClient haloInfiniteClient,
        HaloInfiniteSettings haloSettings, HaloInfiniteHealthCheck haloStatus, IDataPullService dataPullService,
        IOptionsMonitor<ClientConfiguration> optionsMonitor, IUserManagementService userManagementService,
        IAccountAuthorization? accountAuthorization = null)
    {
        _logger = logger;
        _haloInfiniteClient = haloInfiniteClient;
        _haloSettings = haloSettings;
        _haloStatus = haloStatus;
        _accountAuthorization = accountAuthorization;
        _dataPullService = dataPullService;
        _options = optionsMonitor;
        _userManagementService = userManagementService;
    }


    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "MatchStats")]
    public async Task Get([FromQuery] string game)
    {
        if (!Guid.TryParse(game, out var guid))
        {
            throw new Exception("Not valid guid");
        }
        await _dataPullService.GetAndSaveMatch(guid);
    }

    /// <summary>
    /// Which Halo mode is active and, in Gateway mode, the gateway's status from a fresh probe (the Infinite Client page).
    /// </summary>
    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "HaloStatus")]
    public async Task<HaloStatusDto> HaloStatus(CancellationToken ct)
    {
        if (!_haloSettings.IsGateway)
            return HaloStatusDto.Direct();

        return HaloStatusDto.Gateway(_haloSettings, await _haloStatus.ProbeNowAsync(ct));
    }

    /// <summary>Direct mode only: stores the code from Microsoft's sign-in redirect. 409 in Gateway mode.</summary>
    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "SetCode")]
    public async Task<ActionResult> SetCode([FromQuery] string code)
    {
        if (DirectOnly() is { } conflict)
            return conflict;

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing code");

        // Ensure blank slate

        if (System.IO.File.Exists("AccountAuthorization.txt"))
        {
            System.IO.File.Delete("AccountAuthorization.txt");
        }

        if (System.IO.File.Exists("tokens.json"))
        {
            System.IO.File.Delete("tokens.json");
        }

        await _accountAuthorization!.SetCodeAsync(code);

        return Ok();
    }

    /// <summary>Direct mode only: deletes the app's own Halo tokens. 409 in Gateway mode.</summary>
    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "DeleteTokens")]
    public ActionResult DeleteTokens()
    {
        if (DirectOnly() is { } conflict)
            return conflict;

        if (System.IO.File.Exists("AccountAuthorization.txt"))
        {
            System.IO.File.Delete("AccountAuthorization.txt");
        }

        if (System.IO.File.Exists("tokens.json"))
        {
            System.IO.File.Delete("tokens.json");
        }

        return Ok();
    }

    /// <summary>
    /// "Good" when a test call to Halo works. Otherwise, Direct mode: the Microsoft sign-in URL to authorize the account
    /// with; Gateway mode: what is wrong (the gateway is where the account is signed in, so there is no URL here).
    /// </summary>
    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "CheckStatus")]
    public async Task<string> CheckStatus(CancellationToken ct = default)
    {
        if (_haloSettings.IsGateway)
        {
            var probe = await _haloStatus.ProbeNowAsync(ct);
            return probe.Passed ? "Good" : probe.Description;
        }

        try
        {
            var response = await _haloInfiniteClient.StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

            return "Good";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while checking stat pull status");

            var clientConfig = _options.CurrentValue;
            var url = GenerateAuthUrl(clientConfig.ClientId, clientConfig.RedirectUrl);
            return url;
        }
    }

    /// <summary>409 with <see cref="GatewayModeMessage"/> unless the app signs in to Halo itself (Direct mode).</summary>
    private ObjectResult? DirectOnly() =>
        _haloSettings.IsGateway || _accountAuthorization is null ? Conflict(GatewayModeMessage) : null;

    private string GenerateAuthUrl(string clientId, string redirectUrl, string[]? scopes = null)
    {
        NameValueCollection queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);

        queryString.Add("client_id", clientId);
        queryString.Add("response_type", "code");
        queryString.Add("approval_prompt", "auto");

        if (scopes != null && scopes.Length > 0)
        {
            queryString.Add("scope", string.Join(" ", scopes));
        }
        else
        {
            queryString.Add("scope", string.Join(" ", new string[] { "Xboxlive.signin", "Xboxlive.offline_access" }));
        }

        queryString.Add("redirect_uri", redirectUrl);

        return "https://login.live.com/oauth20_authorize.srf" + "?" + queryString.ToString();
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "GeneratePasswordResetLink")]
    public async Task<ActionResult<GeneratePasswordResetLinkResponseDto>> GeneratePasswordResetLink([FromBody] GeneratePasswordResetLinkRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("Username is required");

        var userId = GetUserID();
        if (userId == null)
            return Unauthorized("Unable to identify current user");

        var (success, message, token, expiresAt) = await _userManagementService.GeneratePasswordResetLink(request.Username, userId.Value, ct);

        if (!success)
            return BadRequest(message);

        // In a real application, you might want to send this via email or another secure channel
        // For now, return the token directly (since only sysadmins can generate these)
        return Ok(new GeneratePasswordResetLinkResponseDto
        {
            ResetLink = $"/reset-password?token={token}",
            ExpiresAt = expiresAt!.Value
        });
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpPost(Name = "CleanupExpiredPasswordResetLinks")]
    public async Task<ActionResult> CleanupExpiredPasswordResetLinks(CancellationToken ct)
    {
        await _userManagementService.CleanupExpiredPasswordResetLinks(ct);
        return Ok("Expired password reset links cleaned up");
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
