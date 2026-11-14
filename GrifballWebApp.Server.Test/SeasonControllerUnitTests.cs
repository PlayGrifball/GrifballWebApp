using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Http;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SeasonController's unit tests, on a real GrifballContext (temp-file SQLite, #402): SeasonService is
/// IApplicationDbContext-backed, open to everyone. Its deeper pages stay GrifballWebApp.Integration.Test's.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonControllerUnitTests : SqliteGrifballContextFixture
{
    private SeasonController Controller() => new(new SeasonService(Db, new SeasonStatsService(Db), new GrifballWebApp.Application.Seasons.SeasonVisibility(Db)));

    [Test]
    public async Task GetCurrentSeasonID_ReturnsZero_WhenNoSeasonIsRunning()
    {
        Assert.That(await Controller().GetCurrentSeasonID(CancellationToken.None), Is.Zero);
    }

    [Test]
    public async Task GetSeasonName_RepliesNotFound_ForANonExistentSeason()
    {
        var result = await Controller().GetSeasonName(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season");
    }

    [Test]
    public async Task GetSeasonName_ReturnsTheName_ForAKnownSeason()
    {
        var season = await AddSeason("Winter League");

        var result = await Controller().GetSeasonName(season.SeasonID, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo("Winter League"));
    }

    [Test]
    public async Task GetSeasonSummary_RepliesNotFound_ForANonExistentSeason()
    {
        var result = await Controller().GetSeasonSummary(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season");
    }

    [Test]
    public async Task GetSeasonSummary_ReturnsIt_ForAKnownSeason()
    {
        var season = await AddSeason("Winter League");

        var result = await Controller().GetSeasonSummary(season.SeasonID, CancellationToken.None);

        Assert.That(result.Value!.SeasonName, Is.EqualTo("Winter League"));
    }

    [Test]
    public async Task GetSeasonTeams_RepliesNotFound_ForANonExistentSeason()
    {
        var result = await Controller().GetSeasonTeams(999, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season");
    }

    [Test]
    public async Task GetSeasonTeams_ReturnsNone_ForASeasonWithNoTeams()
    {
        var season = await AddSeason("Winter League");

        var result = await Controller().GetSeasonTeams(season.SeasonID, CancellationToken.None);

        Assert.That(result.Value, Is.Empty);
    }

    [Test]
    public async Task GetSeasonPlayerStats_ReturnsNone_ForASeasonWithNoGames()
    {
        var result = await Controller().GetSeasonPlayerStats(999, teamID: null, CancellationToken.None);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetSeasons_ReturnsNone_WhenThereAreNoSeasons()
    {
        var result = await Controller().GetSeasons(new SeasonListFilter(), CancellationToken.None);

        Assert.That(result.Results, Is.Empty);
    }
}
