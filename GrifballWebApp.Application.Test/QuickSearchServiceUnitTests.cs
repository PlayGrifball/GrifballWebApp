using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class QuickSearchServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private QuickSearchService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new QuickSearchService(Context, _time);
    }

    [Test]
    public async Task SearchAsync_TooShort_ReturnsEmpty()
    {
        var result = await _service.SearchAsync("a", CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Players, Is.Empty);
            Assert.That(result.Teams, Is.Empty);
            Assert.That(result.Seasons, Is.Empty);
        }
    }

    [Test]
    public async Task SearchAsync_Null_ReturnsEmpty()
    {
        var result = await _service.SearchAsync(null, CancellationToken.None);

        Assert.That(result.Players, Is.Empty);
    }

    [Test]
    public async Task SearchPlayersAsync_MatchesByGamertag()
    {
        var xbox = XboxUser.FromXbox(1, "Chief117");
        Context.XboxUsers.Add(xbox);
        var user = User.Seed(userName: "chief", xboxUser: xbox, id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.SearchPlayersAsync("chief", CancellationToken.None);

        Assert.That(result, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result[0].UserID, Is.EqualTo(1));
            Assert.That(result[0].Name, Is.EqualTo("Chief117"));
            Assert.That(result[0].Kind, Is.EqualTo(QuickSearchPlayerKind.Xbox));
        }
    }

    [Test]
    public async Task SearchPlayersAsync_MatchesByDisplayNameForDummyUser()
    {
        var user = User.ImportPlaceholder("historical:ArbiterX", "ArbiterX", null);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.SearchPlayersAsync("arbiter", CancellationToken.None);

        Assert.That(result, Has.Length.EqualTo(1));
        Assert.That(result[0].Kind, Is.EqualTo(QuickSearchPlayerKind.Archive));
    }

    [Test]
    public async Task SearchPlayersAsync_NoMatch_ReturnsEmpty()
    {
        var user = User.Seed(userName: "nobody", displayName: "Nobody", id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.SearchPlayersAsync("zzzz", CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task SearchAsync_MatchesTeamNameOfPublicSeason()
    {
        var season = Season.Seed(seasonName: "Season One", seasonID: 1);
        Context.Seasons.Add(season);
        var team = Team.Seed(teamName: "Reclaimers", seasonID: 1, teamID: 1);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();

        var result = await _service.SearchAsync("reclaim", CancellationToken.None);

        Assert.That(result.Teams, Has.Length.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Teams[0].TeamID, Is.EqualTo(1));
            Assert.That(result.Teams[0].SeasonName, Is.EqualTo("Season One"));
        }
    }

    [Test]
    public async Task SearchAsync_MatchesSeasonByName()
    {
        var season = Season.Seed(seasonName: "Winter Cup", seasonID: 1);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();

        var result = await _service.SearchAsync("winter", CancellationToken.None);

        Assert.That(result.Seasons, Has.Length.EqualTo(1));
        Assert.That(result.Seasons[0].SeasonName, Is.EqualTo("Winter Cup"));
    }

    [Test]
    public async Task SearchAsync_MatchesSeasonByYear()
    {
        var season = Season.Seed(seasonName: "AGLA 5", seasonID: 1);
        season.Schedule(new DateTime(2019, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2019, 12, 5, 0, 0, 0, DateTimeKind.Utc), new DateTime(2019, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();

        var result = await _service.SearchAsync("2019", CancellationToken.None);

        Assert.That(result.Seasons, Has.Length.EqualTo(1));
        Assert.That(result.Seasons[0].SeasonID, Is.EqualTo(1));
    }

    [Test]
    public void TryParseYear_FourDigitInRange_ReturnsTrue()
    {
        var ok = QuickSearchService.TryParseYear("2024", out var year);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ok, Is.True);
            Assert.That(year, Is.EqualTo(2024));
        }
    }

    [TestCase("abcd")]
    [TestCase("123")]
    [TestCase("1999999")]
    public void TryParseYear_NotAFourDigitYear_ReturnsFalse(string q)
    {
        var ok = QuickSearchService.TryParseYear(q, out _);

        Assert.That(ok, Is.False);
    }

    [TestCase("2009", "2009")]
    public void YearSpan_SameYear_ReturnsOneYear(string year, string expected)
    {
        var span = QuickSearchService.YearSpan(int.Parse(year), int.Parse(year));

        Assert.That(span, Is.EqualTo(expected));
    }

    [Test]
    public void YearSpan_DifferentYears_ReturnsRange()
    {
        var span = QuickSearchService.YearSpan(2009, 2011);

        Assert.That(span, Is.EqualTo("2009–2011"));
    }
}
