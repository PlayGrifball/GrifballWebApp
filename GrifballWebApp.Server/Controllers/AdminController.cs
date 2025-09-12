using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.UserManagement;
using GrifballWebApp.Server.Dtos;
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
    private readonly ILogger<AdminController> _logger;
    private readonly IHaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly IAccountAuthorization _accountAuthorization;
    private readonly IDataPullService _dataPullService;
    private readonly IOptionsMonitor<ClientConfiguration> _options;
    private readonly IUserManagementService _userManagementService;

    public AdminController(ILogger<AdminController> logger, IHaloInfiniteClientFactory haloInfiniteClientFactory,
        IAccountAuthorization accountAuthorization, IDataPullService dataPullService,
        IOptionsMonitor<ClientConfiguration> optionsMonitor, IUserManagementService userManagementService)
    {
        _logger = logger;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _accountAuthorization = accountAuthorization;
        _dataPullService = dataPullService;
        _options = optionsMonitor;
        _userManagementService = userManagementService;
    }


    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "MatchStats")]
    public async Task Get([FromQuery] string game)
    {
        if (!Guid.TryParse(game, out var guid))
        {
            throw new Exception("Not valid guid");
        }
        await _dataPullService.GetAndSaveMatch(guid);
        //var foo = new GrifballWebApp.Database.Models.XboxUser();
        ////foo.
        //var c = await _haloInfiniteClientFactory.CreateAsync();

        //var response = await c.StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

        //var playerIDs = response.Result.Players.Select(x => x.PlayerId.Replace("xuid(", "").Replace(")", "")).ToList();
        //var users = await c.Users(playerIDs);

        //return response.Result;
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "SetCode")]
    public async Task<ActionResult> SetCode([FromQuery] string code)
    {
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

        await _accountAuthorization.SetCodeAsync(code);

        return Ok();
    }

    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "DeleteTokens")]
    public ActionResult DeleteTokens()
    {
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

    [Authorize(Roles = "Sysadmin")]
    [HttpGet(Name = "CheckStatus")]
    public async Task<string> CheckStatus()
    {
        try
        {
            var response = await _haloInfiniteClientFactory.StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

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
