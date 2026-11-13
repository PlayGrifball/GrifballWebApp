using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static GrifballWebApp.Application.Test.HistoricalImportUnitTestData;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// A season whose import fails (the database refuses a save) against the fake context: that season alone is rolled back
/// and reported as failed, and the run goes on to the next. The saves of a season or a matchmaking season whose name
/// starts with "Broken" are refused.
/// </summary>
[TestFixture]
internal sealed class HistoricalImportFailureUnitTests : ApplicationDbContextTests
{
    private const string Refusal = "The database refused it";
    private static readonly string[] OnlyWinter = ["Winter League 2019"];
    private static readonly string[] OnlySeason8 = ["Late League Season 8 (Halo 3)"];

    private readonly RefusingSaves _saves = new(context => context.ChangeTracker.Entries()
        .Any(e => e.State == EntityState.Added && e.Entity switch
        {
            Season s => s.SeasonName.StartsWith("Broken", StringComparison.Ordinal),
            MatchmakingSeason m => m.Name.StartsWith("Broken", StringComparison.Ordinal),
            _ => false,
        }));

    private HistoricalImportService _service = null!;

    protected override IReadOnlyList<IInterceptor> Interceptors => [_saves];

    [SetUp]
    public void SetUp()
    {
        _service = new HistoricalImportService(Context, Substitute.For<IDataPullService>(), Options.Create(new HistoricalImportOptions { MatchPullBackoff = TimeSpan.Zero }),
            NullLogger<HistoricalImportService>.Instance, new GamertagMatching(Context), TimeProvider.System);
    }

    [Test]
    public async Task Import_ASeasonThatFails_IsRolledBackAndReported_AndTheOthersAreImported()
    {
        var report = await _service.Import(Input(
            Entry("Broken League 2018", "halo3", "AGLA", false, Player("Lost Player", kills: 3)),
            Entry("Winter League 2019", "halo3", "AGLA", false, Player("Shadow S16", kills: 10))));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Has.Count.EqualTo(1));
            Assert.That(report.Errors[0], Does.EndWith($": {Refusal}"));
            Assert.That(report.SeasonsCreated, Is.EqualTo(1));
            Assert.That(await Context.Seasons.Select(s => s.SeasonName).ToListAsync(), Is.EqualTo(OnlyWinter));
            Assert.That(await Context.PlayerSeasonStats.CountAsync(), Is.EqualTo(1), "nothing of the failed season stays");
        }
    }

    [Test]
    public async Task Import_AMatchmakingSeasonThatFails_IsRolledBackAndReported_AndTheOthersAreImported()
    {
        var report = await _service.Import(Input(
            Entry("Late League Season 8 (Halo 3)", "halo3", "Late League", false, Player("Shadow S16", kills: 4)),
            Entry("Broken Late League Season 9", "halo3", "Late League", false, Player("Lost Player", kills: 3))));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Errors, Has.Count.EqualTo(1));
            Assert.That(report.Errors[0], Does.EndWith($": {Refusal}"));
            Assert.That(report.MatchmakingSeasonsCreated, Is.EqualTo(1));
            Assert.That(await Context.MatchmakingSeasons.Select(s => s.Name).ToListAsync(), Is.EqualTo(OnlySeason8));
        }
    }

    /// <summary>Refuses each save <paramref name="refuses"/> says to, as the database would refuse a row.</summary>
    private sealed class RefusingSaves(Func<DbContext, bool> refuses) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
            => eventData.Context is { } context && refuses(context)
                ? throw new DbUpdateException(Refusal)
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
