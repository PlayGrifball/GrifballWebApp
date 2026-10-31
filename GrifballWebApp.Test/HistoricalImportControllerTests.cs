using GrifballWebApp.Server.Historical;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Text;

namespace GrifballWebApp.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportControllerTests
{
    private static IFormFile File(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "file.json");
    }

    [Test]
    public async Task Import_ReadsTheUploadedFiles_AndReturnsTheReport()
    {
        var service = Substitute.For<IHistoricalImportService>();
        HistoricalArchiveInput? passed = null;
        var report = new HistoricalImportReport { SeasonsCreated = 1 };
        service.Import(Arg.Do<HistoricalArchiveInput>(i => passed = i), Arg.Any<CancellationToken>()).Returns(report);
        var controller = new HistoricalImportController(service);

        var result = await controller.Import(
            File("""{ "seasons": [ { "seasonName": "S 2019", "gameVersion": "halo3", "league": "AGLA", "players": [ { "gamertag": "a", "kills": "3" } ] } ] }"""),
            File("""{ "champions": [ { "season": "S 2019", "league": "Pro", "team": "T", "roster": ["a"] } ] }"""),
            File("""{ "agla/s-2019": { "regular": ["x"] } }"""),
            File("""{ "sources": [ { "id": "w", "url": "https://example.org/w" } ] }"""),
            CancellationToken.None);

        Assert.That(result.Value, Is.SameAs(report));
        Assert.That(passed!.Seasons.Seasons.Single().Players.Single().Kills, Is.EqualTo(3));
        Assert.That(passed.Championships!.Champions.Single().Tier, Is.EqualTo("Pro"));
        Assert.That(passed.WaypointMatches!["agla/s-2019"].Regular, Is.EqualTo(new[] { "x" }));
        Assert.That(passed.Catalog!.Sources.Single().Url, Is.EqualTo("https://example.org/w"));
    }

    [Test]
    public async Task Import_OnlyTheSeasons()
    {
        var service = Substitute.For<IHistoricalImportService>();
        service.Import(Arg.Any<HistoricalArchiveInput>(), Arg.Any<CancellationToken>()).Returns(new HistoricalImportReport());
        var result = await new HistoricalImportController(service).Import(File("""{ "seasons": [] }"""), null, null, null, CancellationToken.None);

        Assert.That(result.Value, Is.Not.Null);
        await service.Received(1).Import(Arg.Is<HistoricalArchiveInput>(i => i.Championships == null && i.WaypointMatches == null && i.Catalog == null), Arg.Any<CancellationToken>());
    }

    [TestCase("not json")]
    [TestCase("null")]
    public async Task Import_AFileThatIsNotTheArchive_IsABadRequest(string json)
    {
        var service = Substitute.For<IHistoricalImportService>();
        var result = await new HistoricalImportController(service).Import(File(json), null, null, null, CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<BadRequestObjectResult>());
        await service.DidNotReceiveWithAnyArgs().Import(default!, default);
    }
}

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
