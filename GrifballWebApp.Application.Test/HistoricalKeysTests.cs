using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Application.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalKeysTests
{
    [TestCase("  Shadow   S16 ", "Shadow S16")]
    [TestCase("x0 pie x0", "x0 pie x0")]
    public void CleanGamertag_TrimsAndCollapsesWhitespace(string raw, string clean)
    {
        Assert.That(HistoricalKeys.CleanGamertag(raw), Is.EqualTo(clean));
        Assert.That(HistoricalKeys.Normalize(raw), Is.EqualTo(clean.ToUpperInvariant()));
    }

    [TestCase("Summer League 2021 (Pro)", "summer-league-2021-pro")]
    [TestCase("Winter league 2020", "winter-league-2020")]
    [TestCase("Q2 Major Season 2026!", "q2-major-season-2026")]
    public void Slug(string text, string slug) => Assert.That(HistoricalKeys.Slug(text), Is.EqualTo(slug));

    [TestCase("Winter League 2019", 2019)]
    [TestCase("Original 8 Tounament", null)]
    public void Year(string name, int? year) => Assert.That(HistoricalKeys.Year(name), Is.EqualTo(year));

    [TestCase("MCC Halo 3", "halo3")]
    [TestCase("Halo Reach", "reach")]
    [TestCase("MCC Halo Reach: Dash", "reach-dash")]
    [TestCase("MCC Halo 4", "halo4")]
    [TestCase("Halo 5", "halo5")]
    [TestCase("Infinite", "infinite")]
    [TestCase("MCC Split", null)]
    [TestCase(null, null)]
    [TestCase(" ", null)]
    public void GameVersionSlug(string? game, string? slug) => Assert.That(HistoricalKeys.GameVersionSlug(game), Is.EqualTo(slug));

    [Test]
    public void MatchId_FromAnIdOrAWaypointUrl()
    {
        var id = Guid.NewGuid();
        Assert.Multiple(() =>
        {
            Assert.That(HistoricalKeys.MatchId(id.ToString()), Is.EqualTo(id));
            Assert.That(HistoricalKeys.MatchId($"https://www.halowaypoint.com/halo-infinite/players/ill%20shrek%20u/matches/{id}"), Is.EqualTo(id));
            Assert.That(HistoricalKeys.MatchId($"https://www.halowaypoint.com/halo-infinite/players/a/matches/{id}/?x=1"), Is.EqualTo(id));
            Assert.That(HistoricalKeys.MatchId("https://www.halowaypoint.com/halo-infinite/players/a"), Is.Null);
        });
    }

    [Test]
    public void SeasonSourceId_IsTheWorkbookAndSheet()
    {
        Assert.That(HistoricalKeys.SeasonSourceId("2018-sml18-fantasy-rey", "Total Stats "), Is.EqualTo("2018-sml18-fantasy-rey/Total Stats"));
        Assert.That(HistoricalKeys.FallbackSeasonSourceId("agla", "Winter League 2019"), Is.EqualTo("agla/winter-league-2019"));
    }

    [Test]
    public void DummyUserName_HasThePrefix() => Assert.That(HistoricalKeys.DummyUserName(" Old  Tag"), Is.EqualTo("historical:Old Tag"));
}
