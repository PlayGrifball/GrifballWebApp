using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Match = GrifballWebApp.Domain.Halo.Match;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Application.Test;

/// <summary>HistoricalImportRecordedGamesTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportRecordedGamesUnitTests
{
    [Test]
    public void Format_AnUnknownArchiveFormat_FallsBackToTheLeagueAndWorkbook()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HistoricalKeys.Format("99", "agla", "wb", "Summer League"), Is.EqualTo(SeasonFormat.LeagueSeason));
            Assert.That(HistoricalKeys.Format(null, "side", "2021-side-respawn-h4-1", "Fall Series 1"), Is.EqualTo(SeasonFormat.Tournament));
            Assert.That(HistoricalKeys.Format("LateLeague", "agla", null, "Summer League"), Is.EqualTo(SeasonFormat.LeagueSeason), "No such format: Late League is matchmaking");
        }
    }

    [Test]
    public void Format_TheArchivesOwn_InAnyCase_WinsOverTheWorkbook()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HistoricalKeys.Format(" tournament ", "agla", "wb", "Summer League"), Is.EqualTo(SeasonFormat.Tournament));
            Assert.That(HistoricalKeys.Format("LeagueSeason", "side", "2021-side-respawn-h4-1", "Respawn 1"), Is.EqualTo(SeasonFormat.LeagueSeason),
                "the archive says league season: not taken for Respawn's tournament");
        }
    }

    [Test]
    public async Task ReadAsync_ReadsTheCatalogInAnyCase()
    {
        using var json = new MemoryStream("""{ "SOURCES": [ { "id": "wl19", "Url": "https://example.com/wl19" }, ] }"""u8.ToArray());

        var catalog = await HistoricalArchiveInput.ReadAsync<ArchiveCatalog>(json);

        Assert.That(catalog.Sources.Select(s => (s.Id, s.Url)), Is.EqualTo(OneSource));
    }

    private static readonly (string, string?)[] OneSource = [("wl19", "https://example.com/wl19")];

    [Test]
    public async Task ReadAsync_AFileOfNull_IsRefused()
    {
        using var json = new MemoryStream("null"u8.ToArray());

        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => HistoricalArchiveInput.ReadAsync<ArchiveCatalog>(json));

        Assert.That(ex!.Message, Is.EqualTo("The ArchiveCatalog file is empty"));
    }

    [Test]
    public void IsLateLeague_ByTheLeague_TheFormat_OrTheName()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(HistoricalKeys.IsLateLeague("Late League", null, "Season 5"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("late-league", null, "Season 5"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("AGLA", "LateLeague", "S8"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("AGLA", null, "Late League Season 8 (Halo 3)"), Is.True);
            Assert.That(HistoricalKeys.IsLateLeague("AGLA", "Tournament", "Summer League 2015"), Is.False);
        }
    }
}
