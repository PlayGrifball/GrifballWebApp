using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Util;
using System.Collections.Specialized;

namespace GrifballWebApp.Server.Controllers;
[Route("[controller]/[action]")]
[ApiController]
public class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly HaloInfiniteClientFactory _haloInfiniteClientFactory;
    private readonly IAccountAuthorization _accountAuthorization;
    private readonly DataPullService _dataPullService;
    private readonly IOptionsMonitor<ClientConfiguration> _options;

    public AdminController(ILogger<AdminController> logger, HaloInfiniteClientFactory haloInfiniteClientFactory,
        IAccountAuthorization accountAuthorization, DataPullService dataPullService,
        IOptionsMonitor<ClientConfiguration> optionsMonitor)
    {
        _logger = logger;
        _haloInfiniteClientFactory = haloInfiniteClientFactory;
        _accountAuthorization = accountAuthorization;
        _dataPullService = dataPullService;
        _options = optionsMonitor;
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
            var client = await _haloInfiniteClientFactory.CreateAsync();

            var response = await client.StatsGetMatchStats("1fde4c2a-7935-4fb0-9706-e226f4d13683");

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

}
