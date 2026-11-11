using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The request bodies whose value-type fields are required (SetEnabledRequest, SetPausedRequest, SetJobPausedRequest,
/// SetJobIntervalRequest), through the real app (<see cref="ServerProgram"/>, signed in as a preview Sysadmin): a body
/// without the value is refused with MVC's 400 rather than read as false or 0, which would turn a job or a source off,
/// or unpause one, that the caller never asked about; a body with it gets past binding to the action.
/// Non-parallelizable because Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[NonParallelizable]
public class RequestBodyRequiredTests
{
    private const string Password = "Smoke-Test-Password-1";

    private string _connectionString = null!;
    private WebApplicationFactory<ServerProgram> _factory = null!;
    private HttpClient _client = null!;

    private sealed class PreviewFactory(Dictionary<string, string?> settings) : WebApplicationFactory<ServerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        }
    }

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"RequestBodyDb_{Guid.NewGuid():N}");
        _factory = new PreviewFactory(new()
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
            ["ApplyMigrations"] = "true",
            ["CreateDatabase"] = "true",
            [PreviewMode.ConfigurationKey] = "true",
            [PreviewSmokeUsers.PasswordKey] = Password,
        });
        // A script's bearer token: no cookies, so no antiforgery token either
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
        using var login = await _client.PostAsJsonAsync("/Identity/Login?useCookies=false", new { Username = PreviewSmokeUsers.UserNames[0], Password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the preview Sysadmin signs in");
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>() ?? throw new AssertionException("No token");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    private async Task<HttpResponseMessage> Post(string path, string json)
        => await _client.PostAsync(path, new StringContent(json, Encoding.UTF8, "application/json"));

    [TestCase("/HistoricalImport/SetSourceEnabled/some-source", "{}", "enabled")]
    [TestCase("/HistoricalImport/SetSchedulePaused", "{}", "paused")]
    [TestCase("/Jobs/SetPaused", """{"group":"g","name":"n"}""", "paused")]
    [TestCase("/Jobs/SetInterval", """{"group":"g","name":"n"}""", "intervalSeconds")]
    public async Task A_body_without_the_value_is_refused(string path, string json, string field)
    {
        using var response = await Post(path, json);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>() ?? throw new AssertionException("No problem details");
        Assert.That(problem.Errors, Does.ContainKey("$"), "the body itself, which JSON couldn't read");
        Assert.That(problem.Errors["$"], Has.Some.Contains($"'{field}'"), "naming the missing property");
    }

    [TestCase("/HistoricalImport/SetSourceEnabled/some-source", """{"enabled":false}""")]
    [TestCase("/HistoricalImport/SetSchedulePaused", """{"paused":true}""")]
    [TestCase("/Jobs/SetPaused", """{"group":"g","name":"n","paused":false}""")]
    [TestCase("/Jobs/SetInterval", """{"group":"g","name":"n","intervalSeconds":3600}""")]
    public async Task A_body_with_the_value_reaches_the_action(string path, string json)
    {
        using var response = await Post(path, json);

        // What the action makes of it (a source or job that isn't there, a schedule not made yet) isn't this test's
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());
    }
}
