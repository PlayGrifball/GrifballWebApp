using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ServerProgram = GrifballWebApp.Server.Program;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A season not announced yet through the real app (#435): every endpoint naming it, its team or its season match answers a
/// signed-out caller 404, as for no such season, and a sysadmin (the preview's) the page; the season list and the home
/// calendar have it for the sysadmin alone. Non-parallelizable because Program.Main swaps the static Serilog logger.
/// </summary>
[TestFixture]
[NonParallelizable]
public class SeasonVisibilityHostTests
{
    private const string Password = "Smoke-Test-Password-1";

    private string _connectionString = null!;
    private WebApplicationFactory<ServerProgram> _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _sysadmin = null!;
    private int _seasonID;
    private int _teamID;
    private int _seasonMatchID;
    private int _publicTeamID;
    private int _awayTeamID;

    private sealed class PreviewFactory(Dictionary<string, string?> settings) : WebApplicationFactory<ServerProgram>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            foreach (var (key, value) in settings)
                builder.UseSetting(key, value);
        }
    }

    private HttpClient Client() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
        AllowAutoRedirect = false,
    });

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _connectionString = TestDatabase.ConnectionString(SetUpFixture.DatabaseContainer, $"SeasonVisibilityDb_{Guid.NewGuid():N}");
        _factory = new PreviewFactory(new()
        {
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
            ["ConnectionStrings:GrifballWebApp"] = _connectionString,
            ["ApplyMigrations"] = "true",
            ["CreateDatabase"] = "true",
            [PreviewMode.ConfigurationKey] = "true",
            [PreviewSmokeUsers.PasswordKey] = Password,
        });
        _anonymous = Client();
        _sysadmin = Client();
        using var login = await _sysadmin.PostAsJsonAsync("/identity/login?useCookies=false", new { Username = PreviewSmokeUsers.UserNames[0], Password });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the preview Sysadmin signs in");
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>() ?? throw new AssertionException("No token");
        _sysadmin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        // Starts in 60 days, announced in 30: set up, not public
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
        var now = DateTime.UtcNow;
        var season = Season.Seed(seasonName: "Secret Season").WithDates(signupsOpen: now.AddDays(35), signupsClose: now.AddDays(45),
            draftStart: now.AddDays(50), seasonStart: now.AddDays(60), seasonEnd: now.AddDays(120), publicAt: now.AddDays(30));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var home = Team.Seed("Hidden Home", seasonID: season.SeasonID);
        var away = Team.Seed("Hidden Away", seasonID: season.SeasonID);
        context.Teams.AddRange(home, away);
        await context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
        context.SeasonMatches.Add(match);
        await context.SaveChangesAsync();
        // The same franchise in a season that's public: its history mustn't list the hidden season's team
        var played = Season.Seed(seasonName: "Played Season").WithDates(signupsOpen: now.AddDays(-120), signupsClose: now.AddDays(-110),
            draftStart: now.AddDays(-100), seasonStart: now.AddDays(-90), seasonEnd: now.AddDays(-30));
        context.Seasons.Add(played);
        await context.SaveChangesAsync();
        var publicTeam = Team.Seed("Hidden Home", seasonID: played.SeasonID);
        context.Teams.Add(publicTeam);
        await context.SaveChangesAsync();
        (_seasonID, _teamID, _seasonMatchID, _publicTeamID, _awayTeamID) = (season.SeasonID, home.TeamID, match.SeasonMatchID, publicTeam.TeamID, away.TeamID);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _anonymous.Dispose();
        _sysadmin.Dispose();
        await _factory.DisposeAsync();
        await TestDatabase.DropDatabase(_connectionString, ifExists: true);
    }

    private static readonly string[] Pages =
    [
        "/seasons/{season}/summary",
        "/seasons/{season}/name",
        "/seasons/{season}/teams",
        "/seasons/{season}/standings",
        "/seasons/{season}/signups",
        "/seasons/{season}/matches/scheduled",
        "/teams/{team}",
        "/season-matches/{match}",
    ];

    /// <summary>The team IDs of the public team's franchise history, as <paramref name="client"/> gets it.</summary>
    private async Task<int[]> HistoryTeamIDs(HttpClient client)
    {
        using var history = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync($"/teams/{_publicTeamID}/history"));
        return history.RootElement.GetProperty("seasons").EnumerateArray().Select(s => s.GetProperty("teamID").GetInt32()).ToArray();
    }

    private string Path(string page) => page.Replace("{season}", _seasonID.ToString()).Replace("{team}", _teamID.ToString()).Replace("{match}", _seasonMatchID.ToString());

    [TestCaseSource(nameof(Pages))]
    public async Task APageOfASeasonNotAnnouncedYet_IsNotFound_SignedOut_ButShown_ToASysadmin(string page)
    {
        using var anonymous = await _anonymous.GetAsync(Path(page));
        using var sysadmin = await _sysadmin.GetAsync(Path(page));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), await anonymous.Content.ReadAsStringAsync());
            Assert.That(sysadmin.StatusCode, Is.EqualTo(HttpStatusCode.OK), await sysadmin.Content.ReadAsStringAsync());
        }
    }

    [TestCase("/seasons/{season}/summary", "/seasons/999999/summary")]
    [TestCase("/seasons/{season}/signups", "/seasons/999999/signups")]
    [TestCase("/teams/{team}", "/teams/999999")]
    [TestCase("/season-matches/{match}", "/season-matches/999999")]
    public async Task AHiddenSeasonsPage_AnswersAsOneThatDoesntExist(string hidden, string missing)
    {
        using var hiddenResponse = await _anonymous.GetAsync(Path(hidden));
        using var missingResponse = await _anonymous.GetAsync(missing);
        var hiddenProblem = await hiddenResponse.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();
        var missingProblem = await missingResponse.Content.ReadFromJsonAsync<Microsoft.AspNetCore.Mvc.ProblemDetails>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(missingResponse.StatusCode, Is.EqualTo(hiddenResponse.StatusCode));
            Assert.That(missingProblem!.Detail, Is.EqualTo(hiddenProblem!.Detail));
            Assert.That(missingProblem.Title, Is.EqualTo(hiddenProblem.Title));
        }
    }

    [Test]
    public async Task APublicFranchisesHistory_ListsItsTeamOfASeasonNotAnnouncedYet_ForTheSysadminAlone()
    {
        var anonymous = await HistoryTeamIDs(_anonymous);
        var sysadmin = await HistoryTeamIDs(_sysadmin);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(anonymous, Is.EqualTo(new[] { _publicTeamID }));
            Assert.That(sysadmin, Is.EquivalentTo(new[] { _publicTeamID, _teamID }));
        }
    }

    [Test]
    public async Task AHiddenTeamsHistory_AloneInItsFranchise_IsNoneToThePublic_AndItsOwnToStaff()
    {
        using var scope = _factory.Services.CreateScope();
        var history = new GrifballWebApp.Application.TeamPage.TeamHistoryService(scope.ServiceProvider.GetRequiredService<GrifballContext>(), new GrifballWebApp.Application.Seasons.SeasonVisibility(scope.ServiceProvider.GetRequiredService<GrifballContext>()));

        var everyone = await history.GetAsync(_awayTeamID);
        var staff = await history.GetAsync(_awayTeamID, GrifballWebApp.Application.Seasons.SeasonViewer.Staff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(everyone, Is.Null);
            Assert.That(staff!.Seasons.Select(s => s.TeamID), Is.EqualTo(new[] { _awayTeamID }));
        }
    }

    [Test]
    public async Task TheSeasonListAndTheCalendar_HaveASeasonNotAnnouncedYet_ForTheSysadminAlone()
    {
        var anonymousList = await _anonymous.GetStringAsync("/seasons?pageNumber=1&pageSize=50");
        var sysadminList = await _sysadmin.GetStringAsync("/seasons?pageNumber=1&pageSize=50");
        var anonymousEvents = await _anonymous.GetStringAsync("/events");
        var sysadminEvents = await _sysadmin.GetStringAsync("/events");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(anonymousList, Does.Not.Contain("Secret Season"));
            Assert.That(sysadminList, Does.Contain("Secret Season"));
            Assert.That(anonymousEvents, Does.Not.Contain("Secret Season"));
            Assert.That(sysadminEvents, Does.Contain("Secret Season"));
        }
    }
}
