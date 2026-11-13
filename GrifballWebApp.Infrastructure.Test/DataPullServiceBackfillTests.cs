using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Models.HaloInfinite.Foundation;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DataPullService.BackfillGameVariants and GetMatch, on a real (temp-file SQLite) GrifballContext with a
/// substituted IHaloInfiniteClient: no database server, host or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DataPullServiceBackfillTests
{
    private string _path = null!;
    private GrifballContext _context = null!;
    private IHaloInfiniteClient _halo = null!;
    private DataPullService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-backfill-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        try
        {
            await _context.Database.EnsureCreatedAsync();
        }
        catch
        {
            await _context.DisposeAsync();
            if (File.Exists(_path))
                File.Delete(_path);
            throw;
        }
        _halo = Substitute.For<IHaloInfiniteClient>();
        _service = new DataPullService(NullLogger<DataPullService>.Instance, _halo, _context, Substitute.For<GrifballWebApp.Application.HaloInfinite.IGetsertXboxUserService>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        if (File.Exists(_path))
            File.Delete(_path);
    }

    private async Task<Match> AddMatchWithoutVariant()
    {
        var match = Match.Seed(Guid.NewGuid());
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    private static MatchStats StatsWithVariant(Guid matchId, Guid assetId, Guid versionId) => new()
    {
        MatchId = matchId,
        MatchInfo = new MatchInfo { UgcGameVariant = new UGCGameVariant { AssetId = assetId, VersionId = versionId } },
    };

    private static MatchStats StatsWithoutVariant(Guid matchId) => new() { MatchId = matchId, MatchInfo = new MatchInfo() };

    [Test]
    public async Task BackfillGameVariants_NoMissingMatches_DoesNothing()
    {
        var result = await _service.BackfillGameVariants(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Missing, Is.Zero);
            Assert.That(result.Attempted, Is.Zero);
        }
    }

    [Test]
    public async Task BackfillGameVariants_HaloGivesTheVariant_FillsItIn()
    {
        var match = await AddMatchWithoutVariant();
        var assetId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _halo.StatsGetMatchStats(match.MatchID).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(StatsWithVariant(match.MatchID, assetId, versionId), null!));

        var result = await _service.BackfillGameVariants(10);

        var saved = await _context.Matches.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Filled, Is.EqualTo(1));
            Assert.That(result.Versions, Is.EquivalentTo(new[] { versionId }));
            Assert.That(saved.UgcGameVariantAssetID, Is.EqualTo(assetId));
            Assert.That(saved.UgcGameVariantVersionID, Is.EqualTo(versionId));
        }
    }

    [Test]
    public async Task BackfillGameVariants_HaloGivesNoVariant_LeavesItAlone()
    {
        var match = await AddMatchWithoutVariant();
        _halo.StatsGetMatchStats(match.MatchID).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(StatsWithoutVariant(match.MatchID), null!));

        var result = await _service.BackfillGameVariants(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.NoVariant, Is.EqualTo(1));
            Assert.That(result.Filled, Is.Zero);
            Assert.That((await _context.Matches.SingleAsync()).UgcGameVariantVersionID, Is.Null);
        }
    }

    [Test]
    public async Task BackfillGameVariants_HaloHasNoAnswer_IsCountedAsFailedAndTriedAgainLater()
    {
        var match = await AddMatchWithoutVariant();
        _halo.StatsGetMatchStats(match.MatchID).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Code = 500 }));

        var result = await _service.BackfillGameVariants(10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Failed, Is.EquivalentTo(new[] { match.MatchID }));
            Assert.That((await _context.Matches.SingleAsync()).UgcGameVariantVersionID, Is.Null);
        }
    }

    [Test]
    public async Task BackfillGameVariants_HaloThrows_IsCountedAsFailed()
    {
        var match = await AddMatchWithoutVariant();
        _halo.StatsGetMatchStats(match.MatchID).ThrowsAsync(new HttpRequestException("down"));

        var result = await _service.BackfillGameVariants(10);

        Assert.That(result.Failed, Is.EquivalentTo(new[] { match.MatchID }));
    }

    [Test]
    public async Task BackfillGameVariants_BatchSizeLimitsHowManyAreAttempted()
    {
        await AddMatchWithoutVariant();
        await AddMatchWithoutVariant();
        _halo.StatsGetMatchStats(Arg.Any<Guid>()).Returns(callInfo => new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(StatsWithoutVariant(callInfo.Arg<Guid>()), null!));

        var result = await _service.BackfillGameVariants(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Missing, Is.EqualTo(2));
            Assert.That(result.Attempted, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetMatch_HaloAnswers_ReturnsIt()
    {
        var matchId = Guid.NewGuid();
        var stats = StatsWithoutVariant(matchId);
        _halo.StatsGetMatchStats(matchId).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(stats, null!));

        var result = await _service.GetMatch(matchId);

        Assert.That(result, Is.SameAs(stats));
    }

    [Test]
    public async Task GetMatch_HaloHasNoAnswer_ReturnsNull()
    {
        var matchId = Guid.NewGuid();
        _halo.StatsGetMatchStats(matchId).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null!, new HaloApiErrorContainer { Message = "down" }));

        var result = await _service.GetMatch(matchId);

        Assert.That(result, Is.Null);
    }
}
