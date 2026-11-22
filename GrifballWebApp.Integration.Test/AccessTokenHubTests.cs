using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Chat;
using GrifballWebApp.Server.Live;
using GrifballWebApp.Server.Mcp;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// An access token never opens a hub (<see cref="AccessTokenAuthentication.RefuseHubConnections"/>): hubs are the
/// browser's own. Whatever the token's scope or read-only flag, the connection attempt itself is refused (401), closing
/// the read-only bypass through <see cref="ChatHub.Ask"/>/<see cref="ChatHub.Confirm"/> and the gap where a hub connection
/// a token opened would outlive revoking it. Non-parallelizable: Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[NonParallelizable]
public class AccessTokenHubTests
{
    private const string Password = "Hub-Token-Password-1";

    // Static: InstancePerTestCase makes a new instance per test, but the factory and its database are the fixture's own.
    private static string _connectionString = null!;
    private static WebApplicationFactory<ServerProgram> _factory = null!;

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
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"AccessTokenHubDb_{Guid.NewGuid():N}");
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
        using var login = await client.PostAsJsonAsync("/identity/login?useCookies=false", new { Username = PreviewSmokeUsers.UserNames[0], Password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await login.Content.ReadFromJsonAsync<AccessTokenResponse>())!.AccessToken;
    }

    /// <summary>A new personal access token of the smoke user's, with the given scope and read-only flag; revoked after the test.</summary>
    private async Task<string> NewAccessToken(AccessTokenScope scope, bool readOnly)
    {
        using var client = Client(await ApiToken());
        using var response = await client.PostAsJsonAsync("/identity/access-tokens", new CreateAccessTokenRequest("hub-test", 30, scope, readOnly));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return (await response.Content.ReadFromJsonAsync<CreatedAccessTokenDto>(ApiJson.Options()))!.Token;
    }

    [TearDown]
    public async Task TearDown()
    {
        // Leaves the smoke user with no tokens: AccessTokenService.MaxTokensPerUser would otherwise refuse new ones.
        using var site = Client(await ApiToken());
        using var list = await site.GetAsync("/identity/access-tokens");
        foreach (var token in (await list.Content.ReadFromJsonAsync<List<AccessTokenDto>>(ApiJson.Options()))!)
            await site.PostAsync($"/identity/access-tokens/{token.ID}/revoke", null);
    }

    /// <summary>Tries to connect to <paramref name="hub"/> over a direct WebSocket upgrade, with the token as a bearer header.</summary>
    private async Task<Exception?> TryConnect(string hub, string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"https://localhost/{hub}", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, ct) =>
                {
                    var socket = _factory.Server.CreateWebSocketClient();
                    socket.ConfigureRequest = request =>
                    {
                        request.Headers.Authorization = "Bearer " + token;
                        request.Headers.Origin = "https://localhost";
                    };
                    return await socket.ConnectAsync(context.Uri, ct);
                };
            })
            .Build();
        try
        {
            await connection.StartAsync();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [TestCase(ChatHub.Path, AccessTokenScope.Api, false)]
    [TestCase(ChatHub.Path, AccessTokenScope.Api, true)]
    [TestCase(LiveHub.Path, AccessTokenScope.Api, false)]
    [TestCase(LiveHub.Path, AccessTokenScope.Api, true)]
    [TestCase(ChatHub.Path, AccessTokenScope.Both, false)]
    [TestCase(LiveHub.Path, AccessTokenScope.Both, false)]
    public async Task AnAccessToken_NeverOpensAHub(string hub, AccessTokenScope scope, bool readOnly)
    {
        var token = await NewAccessToken(scope, readOnly);

        var failure = await TryConnect(hub, token);

        Assert.That(failure, Is.Not.Null, "the connection must be refused, whatever the token's scope or read-only flag");
    }
}
