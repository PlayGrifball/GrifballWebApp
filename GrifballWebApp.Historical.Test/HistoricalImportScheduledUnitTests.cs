using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Historical.Import;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.HaloInfinite;
using static GrifballWebApp.Historical.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Historical.Test;

/// <summary>
/// A schedule's matches of a season whose results are lost (<see cref="ArchiveScheduledMatchInput"/>): dated season matches with
/// no result, reconciled run after run. Against the fake SQLite context, as HistoricalImportForfeitsUnitTests.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class HistoricalImportScheduledUnitTests : ApplicationDbContextTests
{
    private const string SeasonKey = "winter-league-2020/Stats";
    private HistoricalImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System, Substitute.For<IUserMergeService>());
    }

    private static ArchiveSeason Season() => Entry("Winter League 2020", "reach", "AGLA", false,
        Player("Church", team: "Red Team"), Player("Tex", team: "Blue Team"), Player("Sarge", team: "Gold Team"));

    private static ArchiveScheduledMatchInput Scheduled(int day, string home, string away) => new()
    {
        SeasonSourceId = SeasonKey,
        SourceKey = $"wl20-reach/Full Schedule@2020-01-{day:00}",
        Home = home,
        Away = away,
        PlayedAt = new DateTime(2020, 1, day, 1, 0, 0, DateTimeKind.Utc),
    };

    private static HistoricalArchiveInput Input(params ArchiveScheduledMatchInput[] matches)
        => new() { Seasons = new UnifiedArchive { Seasons = [Season()] }, ScheduledMatches = [.. matches] };

    private async Task<List<(string? Home, string? Away, DateTime? At, SeasonMatchResult? HomeResult, SeasonMatchResult? AwayResult)>> Matches()
        => (await Context.SeasonMatches.AsNoTracking().OrderBy(m => m.SeasonMatchID)
            .Select(m => new { Home = m.HomeTeam!.TeamName, Away = m.AwayTeam!.TeamName, m.ScheduledTime, m.HomeTeamResult, m.AwayTeamResult })
            .ToListAsync())
            .Select(m => ((string?)m.Home, (string?)m.Away, m.ScheduledTime, m.HomeTeamResult, m.AwayTeamResult)).ToList();

    [Test]
    public async Task Import_AScheduledMatch_IsADatedMatchWithNoResult()
    {
        var report = await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team"), Scheduled(6, "Gold Team", "Red Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Is.Empty);
            Assert.That(await Matches(), Is.EqualTo(new[]
            {
                ((string?)"Red Team", (string?)"Blue Team", (DateTime?)new DateTime(2020, 1, 5, 1, 0, 0, DateTimeKind.Utc), (SeasonMatchResult?)null, (SeasonMatchResult?)null),
                ("Gold Team", "Red Team", new DateTime(2020, 1, 6, 1, 0, 0, DateTimeKind.Utc), null, null),
            }));
            Assert.That(report.SeasonMatchesCreated, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_AgainWithTheSameMatches_ChangesNothing_AndOneNoLongerListedGoes()
    {
        await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team"), Scheduled(6, "Gold Team", "Red Team")));
        var first = await Context.SeasonMatches.Select(m => m.SeasonMatchID).OrderBy(id => id).FirstAsync();
        Context.ChangeTracker.Clear();

        var again = await _service.Import(Input(Scheduled(5, "Red Team", "Blue Team")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((again.SeasonMatchesCreated, again.SeasonMatchesRemoved), Is.EqualTo((0, 1)));
            Assert.That(await Context.SeasonMatches.Select(m => m.SeasonMatchID).ToListAsync(), Is.EqualTo(new[] { first }), "The same match kept, not made again");
        }
    }

    [Test]
    public async Task Import_AScheduledMatchOfATeamTheSeasonHasnt_OrOfNoSeason_IsLeftOut_AndReported()
    {
        var other = new ArchiveScheduledMatchInput { SeasonSourceId = "nothing/Stats", SourceKey = "x@1", Home = "Red Team", Away = "Blue Team", PlayedAt = DateTime.UnixEpoch };

        var report = await _service.Import(Input(Scheduled(5, "Red Team", "Nobody"), other));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.SeasonMatches.CountAsync(), Is.Zero);
            Assert.That(report.Warnings, Does.Contain($"{SeasonKey}: 1 scheduled match(es) naming a team that is none of the season's, left out: wl20-reach/Full Schedule@2020-01-05"));
            Assert.That(report.Skipped, Does.Contain("Scheduled match x@1: no season with the key nothing/Stats in the archive"));
        }
    }
}
