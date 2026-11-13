using Microsoft.Extensions.Configuration;

namespace GrifballWebApp.Application.Test;

/// <summary>UrlService's links that UrlServiceTests doesn't build: grades, teams, profiles, matches, reschedules and
/// plain pages.</summary>
[TestFixture]
internal sealed class UrlServicePageTests
{
    private const string BaseUrl = "https://grif.example";
    private static readonly Guid MatchID = new("6f1c2d3e-4a5b-4c6d-8e9f-0a1b2c3d4e5f");

    private static UrlService Service() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = BaseUrl })
        .Build());

    [Test]
    public void EveryLink_IsUnderTheBaseUrl()
    {
        var service = Service();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(service.Grades(3), Is.EqualTo("https://grif.example/season/3/playergrades"));
            Assert.That(service.Team(3, 40), Is.EqualTo("https://grif.example/season/3/team/40"));
            Assert.That(service.TeamHistory(40), Is.EqualTo("https://grif.example/history/team/40"));
            Assert.That(service.Profile(7), Is.EqualTo("https://grif.example/profile/7"));
            Assert.That(service.SeasonMatch(55), Is.EqualTo("https://grif.example/seasonmatch/55"));
            Assert.That(service.Match(MatchID), Is.EqualTo("https://grif.example/match/6f1c2d3e-4a5b-4c6d-8e9f-0a1b2c3d4e5f"));
            Assert.That(service.RescheduleRequest(55), Is.EqualTo("https://grif.example/reschedule-request/55"));
        }
    }

    [TestCase("seasons", "https://grif.example/seasons")]
    [TestCase("/leaderboards", "https://grif.example/leaderboards")]
    [TestCase("//late-league", "https://grif.example/late-league")]
    public void Page_TrimsLeadingSlashes(string path, string expected)
    {
        Assert.That(Service().Page(path), Is.EqualTo(expected));
    }
}
