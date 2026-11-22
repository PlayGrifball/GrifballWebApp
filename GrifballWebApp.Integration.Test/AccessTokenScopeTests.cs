using GrifballWebApp.Server;
using GrifballWebApp.Server.Mcp;
using GrifballWebApp.Server.Preview;
using GrifballWebApp.Infrastructure.Preview;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Mvc.Testing;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A personal access token's scope (<see cref="AccessTokenScope"/>) and read-only flag against the real
/// <see cref="ServerProgram"/> in preview mode: which surfaces a token opens, a read-only token's writes, and the
/// endpoints no token may ever call (<see cref="DenyAccessTokenAttribute"/>). <see cref="McpServerTests"/> keeps the
/// MCP-only behaviour this builds on. Non-parallelizable because Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[NonParallelizable]
public class AccessTokenScopeTests
{
    private const string Password = "Smoke-Test-Password-1";

    // Static: InstancePerTestCase makes a new instance per test, but the factory and its database are the fixture's
    // own, started once (DatabaseConversionTests does the same).
    private static string _connectionString = null!;
    private static WebApplicationFactory<ServerProgram> _factory = null!;

    private static string UserName => PreviewSmokeUsers.UserNames[0];

    private sealed class PreviewFactory(Dictionary<string, string?> settings) : WebApplicationFactory<ServerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        }
    }

    [OneTimeSetUp]
    public static void OneTimeSetUp()
    {
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"AccessTokenScopeDb_{Guid.NewGuid():N}");
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
    public static async Task OneTimeTearDown()
    {
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    /// <summary>
    /// Revokes every token the smoke user has: the fixture shares one database and one user across every test
    /// (a fresh one per test would mean a fresh database each time), and <see cref="AccessTokenService.MaxTokensPerUser"/>
    /// would otherwise refuse new ones partway through the fixture.
    /// </summary>
    [TearDown]
    public async Task TearDown()
    {
        using var site = Client(await ApiToken());
        using var list = await site.GetAsync("/identity/access-tokens");
        foreach (var token in (await list.Content.ReadFromJsonAsync<List<AccessTokenDto>>(ApiJson.Options()))!)
            await site.PostAsync($"/identity/access-tokens/{token.ID}/revoke", null);
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

    /// <summary>A new personal access token of the smoke user's, with the given scope and read-only flag.</summary>
    private async Task<CreatedAccessTokenDto> NewAccessToken(AccessTokenScope scope, bool readOnly = false, string name = "test token")
    {
        using var client = Client(await ApiToken());
        using var response = await client.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest(name, 30, scope, readOnly));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<CreatedAccessTokenDto>(ApiJson.Options()))!;
    }

    private async Task<McpClient> Connect(string accessToken)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri("https://localhost/" + GrifMcp.Path) },
            Client(accessToken), loggerFactory: null, ownsHttpClient: true);
        return await McpClient.CreateAsync(transport);
    }

    private static async Task<string> Reason(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("title").GetString() ?? "";
    }

    private static string Text(CallToolResult result)
        => string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    // ---------- Which surface a scope opens ----------

    [TestCase(AccessTokenScope.Api)]
    [TestCase(AccessTokenScope.Both)]
    public async Task AScopeThatAllowsTheApi_SignsInToTheRestApi(AccessTokenScope scope)
    {
        var created = await NewAccessToken(scope);
        using var client = Client(created.Token);

        using var response = await client.GetAsync("/identity/meta-info");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task AnMcpOnlyToken_OnTheApi_Is401WithAReason()
    {
        var created = await NewAccessToken(AccessTokenScope.Mcp);
        using var client = Client(created.Token);

        using var response = await client.GetAsync("/identity/meta-info");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(await Reason(response), Does.Contain("MCP server only"));
        }
    }

    [TestCase(AccessTokenScope.Mcp)]
    [TestCase(AccessTokenScope.Both)]
    public async Task AScopeThatAllowsMcp_SignsInToTheMcpEndpoint(AccessTokenScope scope)
    {
        var created = await NewAccessToken(scope);

        await using var mcp = await Connect(created.Token);
        var tools = await mcp.ListToolsAsync();

        Assert.That(tools, Is.Not.Empty);
    }

    [Test]
    public async Task AnApiOnlyToken_OnMcp_Is401WithAReason()
    {
        var created = await NewAccessToken(AccessTokenScope.Api);
        using var client = Client(created.Token);
        var request = new HttpRequestMessage(HttpMethod.Post, "/" + GrifMcp.Path)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await client.SendAsync(request);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(await Reason(response), Does.Contain("REST API only"));
        }
    }

    // ---------- Read-only ----------

    [Test]
    public async Task AReadOnlyToken_ReadsTheApi_ButARestWriteIs403SayingSo()
    {
        var created = await NewAccessToken(AccessTokenScope.Api, readOnly: true);
        using var client = Client(created.Token);

        using var read = await client.GetAsync("/identity/meta-info");
        using var write = await client.PostAsJsonAsync("/me/region", new { RegionID = 999_999 });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(read.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(write.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(await Reason(write), Does.Contain("read-only"));
        }
    }

    [Test]
    public async Task AReadWriteToken_CanWrite_OnTheApi()
    {
        var created = await NewAccessToken(AccessTokenScope.Api);
        using var client = Client(created.Token);

        // A region that doesn't exist: the guard must not be what answers, the service's own 400 does
        using var write = await client.PostAsJsonAsync("/me/region", new { RegionID = 999_999 });

        Assert.That(write.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), "past the read-only guard: refused by the service instead");
    }

    [Test]
    public async Task AReadOnlyToken_OnMcp_DoesNotListOrCallAChangingTool()
    {
        var created = await NewAccessToken(AccessTokenScope.Mcp, readOnly: true);
        await using var mcp = await Connect(created.Token);

        var tools = await mcp.ListToolsAsync();
        var refused = await mcp.CallToolAsync("decide_gamertag_claim", new Dictionary<string, object?> { ["claimID"] = 1, ["approve"] = true });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Select(t => t.Name), Does.Not.Contain("decide_gamertag_claim"), "a sysadmin tool that changes data");
            Assert.That(tools.Select(t => t.Name), Does.Contain("who_am_i"), "reads are still there");
            Assert.That(refused.IsError, Is.True);
            Assert.That(Text(refused), Does.Contain("read-only"));
        }
    }

    [Test]
    public async Task AReadOnlyToken_OnMcp_AlsoHidesAndRefusesAQueuedHaloPull()
    {
        // A pull isn't ToolEffect.ChangesData, but it does save games it finds: read-only must still hide and refuse
        // it, the same as the REST API's 403 on a write.
        var created = await NewAccessToken(AccessTokenScope.Mcp, readOnly: true);
        await using var mcp = await Connect(created.Token);

        var tools = await mcp.ListToolsAsync();
        var refused = await mcp.CallToolAsync("pull_xbox_account_games", new Dictionary<string, object?> { ["gamertag"] = "NobodyHasThisGamertag" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Select(t => t.Name), Does.Not.Contain("pull_xbox_account_games"));
            Assert.That(refused.IsError, Is.True);
            Assert.That(Text(refused), Does.Contain("read-only"));
        }
    }

    [Test]
    public async Task ANonReadOnlyToken_OnMcp_StillListsAndCallsChangingTools()
    {
        var created = await NewAccessToken(AccessTokenScope.Mcp, readOnly: false);
        await using var mcp = await Connect(created.Token);

        var tools = await mcp.ListToolsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.Select(t => t.Name), Does.Contain("decide_gamertag_claim"), "the smoke user is a sysadmin, and the token isn't read-only");
            Assert.That(tools.Select(t => t.Name), Does.Contain("pull_xbox_account_games"), "a queued pull is still there too");
        }
    }

    // ---------- Never allowed with any token (DenyAccessTokenAttribute) ----------

    private async Task<string> ReadWriteApiToken() => (await NewAccessToken(AccessTokenScope.Both)).Token;

    [Test]
    public async Task NoToken_MayManageTokens_WhateverItsOwnScope()
    {
        using var client = Client(await ReadWriteApiToken());

        using var list = await client.GetAsync("/identity/access-tokens");
        using var make = await client.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest("another", 30));
        using var revoke = await client.PostAsync("/identity/access-tokens/anything/revoke", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "listing tokens");
            Assert.That(make.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "making a token");
            Assert.That(revoke.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "revoking a token");
        }
    }

    [Test]
    public async Task NoToken_MayChangeLinkedAccounts()
    {
        using var client = Client(await ReadWriteApiToken());

        using var start = await client.PostAsync("/me/external-logins/link-token?provider=Discord", null);
        using var unlink = await client.PostAsJsonAsync("/me/external-logins/remove", new { LoginProvider = "Discord", ProviderKey = "1" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(start.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "starting a link");
            Assert.That(unlink.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "unlinking one");
        }
    }

    [Test]
    public async Task NoToken_MayGenerateAPasswordResetLink()
    {
        using var client = Client(await ReadWriteApiToken());

        using var response = await client.PostAsJsonAsync("/password-reset-links", new { Username = UserName });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task NoToken_MayMergeUsers_OrUndoAMerge()
    {
        using var client = Client(await ReadWriteApiToken());

        using var merge = await client.PostAsJsonAsync("/user-merges", new
        {
            MergeToId = 1,
            MergeFromId = 2,
            MergeOptions = new
            {
                TransferDiscord = false,
                TransferLogins = false,
                TransferXbox = false,
                TransferTeamPlayers = false,
                TransferExperiences = false,
                TransferSignups = false,
                DeleteMergeFrom = false,
            },
        });
        using var undo = await client.PostAsync("/user-merges/1/undo", null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merge.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "merging");
            Assert.That(undo.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "undoing a merge");
        }
    }
}
