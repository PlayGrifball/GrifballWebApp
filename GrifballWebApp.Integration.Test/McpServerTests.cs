using GrifballWebApp.Server.Mcp;
using GrifballWebApp.Server.Preview;
using GrifballWebApp.Infrastructure.Preview;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using User = GrifballWebApp.Domain.Players.User;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The MCP server (<see cref="GrifMcp"/>) against the real <see cref="ServerProgram"/> in preview mode: a smoke-test user
/// (Sysadmin and Commissioner) makes a personal access token on the site, and an MCP client signs in with it and acts as them.
/// Non-parallelizable because Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[NonParallelizable]
public class McpServerTests
{
    private const string Password = "Smoke-Test-Password-1";

    private string _connectionString = null!;
    private WebApplicationFactory<ServerProgram> _factory = null!;

    private static string UserName => PreviewSmokeUsers.UserNames[0];

    private static readonly string[] SomeTools = ["who_am_i", "search", "list_seasons", "get_season_standings", "get_team", "get_player", "get_season_match"];

    private sealed class PreviewFactory(Dictionary<string, string?> settings) : WebApplicationFactory<ServerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        }
    }

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"McpServerDb_{Guid.NewGuid():N}");
        _factory = new PreviewFactory(new()
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
            ["ApplyMigrations"] = "true",
            ["CreateDatabase"] = "true",
            [PreviewMode.ConfigurationKey] = "true",
            [PreviewSmokeUsers.PasswordKey] = Password,
        });
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    /// <summary>A client with no cookies, over HTTPS, following no redirects.</summary>
    private HttpClient Client(string? bearer = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        if (bearer is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return client;
    }

    /// <summary>The smoke user's API bearer token (/identity/login?useCookies=false), as a script signs in.</summary>
    private async Task<string> ApiToken()
    {
        using var client = Client();
        using var login = await client.PostAsJsonAsync("/identity/login?useCookies=false", new { Username = UserName, Password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken;
    }

    /// <summary>A new personal access token of the smoke user's, made as the site makes one.</summary>
    private async Task<CreatedAccessTokenDto> NewAccessToken(string name = "Claude Code")
    {
        using var client = Client(await ApiToken());
        using var response = await client.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest(name, 30));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<CreatedAccessTokenDto>())!;
    }

    private async Task<McpClient> Connect(string accessToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri("https://localhost/" + GrifMcp.Path) },
            Client(accessToken), loggerFactory: null, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    /// <summary>A bare MCP request (tools/list), for the statuses the endpoint answers before any MCP is spoken.</summary>
    private static HttpRequestMessage ListToolsRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/" + GrifMcp.Path)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        return request;
    }

    /// <summary>The tools that change something: the pull tools and staff's actions (<see cref="GrifTools.All"/>'s effects).</summary>
    private static readonly HashSet<string> Changing = GrifTools.All.Where(s => s.Effect is not ToolEffect.ReadsOnly)
        .SelectMany(s => GrifTools.Methods(s.Type)).Select(GrifTools.Name).ToHashSet();

    private static string Text(CallToolResult result) => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    [Test]
    public async Task AnAccessToken_ListsTheReadOnlyTools_AndCallsThemAsTheUserWhoMadeIt()
    {
        var created = await NewAccessToken();
        Assert.That(created.Token, Does.StartWith(AccessTokenService.Prefix));

        await using var mcp = await Connect(created.Token);
        var tools = await mcp.ListToolsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Select(t => t.Name), Is.SupersetOf(SomeTools));
            Assert.That(tools.Where(t => !Changing.Contains(t.Name)).Select(t => t.ProtocolTool.Annotations?.ReadOnlyHint), Has.All.True,
                "every tool but the pull tools (their pulls save games) and staff's actions only reads");
            Assert.That(tools.Where(t => Changing.Contains(t.Name)).Select(t => t.ProtocolTool.Annotations?.ReadOnlyHint), Has.All.False);
            Assert.That(tools.Select(t => t.Description), Has.None.Null.And.None.Empty);
        }

        var me = await mcp.CallToolAsync("who_am_i", new Dictionary<string, object?>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(me.IsError, Is.Not.True, Text(me));
            Assert.That(Text(me), Does.Contain("Sysadmin").And.Contain("Commissioner"), "the token's user and their roles");
        }
    }

    [Test]
    public async Task AToolsRefusal_ReachesTheClient_AsAnErrorItCanActOn()
    {
        var created = await NewAccessToken();
        await using var mcp = await Connect(created.Token);

        var result = await mcp.CallToolAsync("get_season", new Dictionary<string, object?> { ["seasonID"] = 987654 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(Text(result), Does.Contain("There is no season 987654"));
        }
    }

    [Test]
    public async Task AServicesRefusal_ReachesTheClient_AsTheSitesWouldReachIt()
    {
        // The claim service refuses a claim that isn't there with its own exception, which the site answers 400: here its reason
        var created = await NewAccessToken();
        await using var mcp = await Connect(created.Token);

        var result = await mcp.CallToolAsync("decide_gamertag_claim", new Dictionary<string, object?> { ["claimID"] = 987654, ["approve"] = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsError, Is.True);
            Assert.That(Text(result), Does.Contain("There is no claim 987654"));
        }
    }

    [Test]
    public async Task TheEndpoint_RefusesNoToken_AndTheApisOwnBearerToken()
    {
        using (var client = Client())
        using (var response = await client.SendAsync(ListToolsRequest()))
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "no token");

        using (var client = Client(await ApiToken()))
        using (var response = await client.SendAsync(ListToolsRequest()))
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "the API's 15-minute bearer token opens the API only");

        using (var client = Client(AccessTokenService.Prefix + new string('0', 32) + "_not-a-secret"))
        using (var response = await client.SendAsync(ListToolsRequest()))
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "an unknown access token");
    }

    [Test]
    public async Task ACookieSession_DoesNotOpenTheEndpoint()
    {
        using var browser = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
        using (var login = await browser.PostAsJsonAsync("/identity/login", new { Username = UserName, Password }))
            Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var response = await browser.SendAsync(ListToolsRequest());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "another site's page could ride the cookie");
    }

    [Test]
    public async Task AnAccessToken_OpensNothingButTheMcpEndpoint()
    {
        var created = await NewAccessToken();
        using var client = Client(created.Token);

        using (var me = await client.GetAsync("/identity/meta-info"))
            Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using (var more = await client.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest("another", 30)))
            Assert.That(more.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "a token can't make more");
    }

    [Test]
    public async Task ALockedOutUsersToken_StopsWorking()
    {
        var userName = "locked-" + Guid.NewGuid().ToString("N")[..8];
        using var site = Client();
        using (var register = await site.PostAsJsonAsync("/identity/register", new { Username = userName, Password }))
            Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var login = await site.PostAsJsonAsync("/identity/login?useCookies=false", new { Username = userName, Password });
        using var signedIn = Client((await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken);
        using var made = await signedIn.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest("mine", 30));
        var created = (await made.Content.ReadFromJsonAsync<CreatedAccessTokenDto>())!;

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByNameAsync(userName))!;
            await users.SetLockoutEnabledAsync(user, true);
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1));
        }

        using var client = Client(created.Token);
        using var response = await client.SendAsync(ListToolsRequest());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ARoleTool_IsHiddenFrom_AndRefusedTo_AUserWithoutTheRole()
    {
        // A new account is a Player: taken out of the role, it may not pull
        var userName = "norole-" + Guid.NewGuid().ToString("N")[..8];
        using var site = Client();
        using (var register = await site.PostAsJsonAsync("/identity/register", new { Username = userName, Password }))
            Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            await users.RemoveFromRoleAsync((await users.FindByNameAsync(userName))!, "Player");
        }
        using var login = await site.PostAsJsonAsync("/identity/login?useCookies=false", new { Username = userName, Password });
        using var signedIn = Client((await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken);
        using var made = await signedIn.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest("mine", 30));
        var created = (await made.Content.ReadFromJsonAsync<CreatedAccessTokenDto>())!;
        await using var mcp = await Connect(created.Token);
        await using var staff = await Connect((await NewAccessToken()).Token);

        var theirs = (await mcp.ListToolsAsync()).Select(t => t.Name).ToList();
        var staffs = (await staff.ListToolsAsync()).Select(t => t.Name).ToList();
        var refused = await mcp.CallToolAsync("pull_season_match_games", new Dictionary<string, object?> { ["seasonMatchID"] = 1 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(theirs, Does.Not.Contain("pull_season_match_games").And.Contain("get_recent_games"));
            Assert.That(staffs, Does.Contain("pull_season_match_games"), "the smoke user is a sysadmin");
            Assert.That(refused.IsError, Is.True);
            Assert.That(Text(refused), Does.Contain("Only a player or commissioner or sysadmin"));
        }
    }

    [Test]
    public async Task ARevokedToken_StopsWorking_AndLeavesTheList()
    {
        var created = await NewAccessToken("to revoke");
        using var site = Client(await ApiToken());
        using (var list = await site.GetAsync("/identity/access-tokens"))
            Assert.That((await list.Content.ReadFromJsonAsync<List<AccessTokenDto>>())!.Select(t => t.ID), Does.Contain(created.AccessToken.ID));

        using (var revoke = await site.PostAsync($"/identity/access-tokens/{created.AccessToken.ID}/revoke", null))
            Assert.That(revoke.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using (var client = Client(created.Token))
        using (var response = await client.SendAsync(ListToolsRequest()))
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using (var list = await site.GetAsync("/identity/access-tokens"))
            Assert.That((await list.Content.ReadFromJsonAsync<List<AccessTokenDto>>())!.Select(t => t.ID), Does.Not.Contain(created.AccessToken.ID));
        using (var again = await site.PostAsync($"/identity/access-tokens/{created.AccessToken.ID}/revoke", null))
            Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Create_RefusesANameless_OrTooLongLivedToken()
    {
        using var site = Client(await ApiToken());

        using (var nameless = await site.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest(" ", 30)))
            Assert.That(nameless.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using (var forever = await site.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest("forever", AccessTokenService.MaxDays + 1)))
            Assert.That(forever.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
