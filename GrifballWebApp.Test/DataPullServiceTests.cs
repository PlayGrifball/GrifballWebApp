using GrifballWebApp.Database;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DataPullServiceTests
{
    private GrifballContext _context;
    private ILogger<DataPullService> _logger;
    private IHaloInfiniteClientFactory _haloInfiniteClientFactory;
    private IGetsertXboxUserService _getsertXboxUserService;
    private DataPullService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _logger = Substitute.For<ILogger<DataPullService>>();
        _haloInfiniteClientFactory = Substitute.For<IHaloInfiniteClientFactory>();
        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();
        _service = new DataPullService(_logger, _haloInfiniteClientFactory, _context, _getsertXboxUserService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task GetMatch_ShouldReturnNull_WhenApiReturnsNull()
    {
        var matchId = Guid.NewGuid();
        _haloInfiniteClientFactory.StatsGetMatchStats(matchId).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null, null));
        var result = await _service.GetMatch(matchId);
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetMatch_ShouldReturnMatchStats_WhenApiReturnsValid()
    {
        var matchId = Guid.NewGuid();
        var matchStats = new MatchStats { MatchId = matchId };
        _haloInfiniteClientFactory.StatsGetMatchStats(matchId).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(matchStats, null));
        var result = await _service.GetMatch(matchId);
        Assert.That(result, Is.EqualTo(matchStats));
    }

    [Test]
    public async Task GetAndSaveMatch_ShouldNotSave_WhenMatchExists()
    {
        var matchId = Guid.NewGuid();
        _context.Matches.Add(new GrifballWebApp.Database.Models.Match { MatchID = matchId });
        await _context.SaveChangesAsync();
        await _service.GetAndSaveMatch(matchId);
        // Should not add another match
        var count = await _context.Matches.CountAsync(x => x.MatchID == matchId);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAndSaveMatch_ShouldNotSave_WhenApiReturnsNull()
    {
        var matchId = Guid.NewGuid();
        _haloInfiniteClientFactory.StatsGetMatchStats(matchId).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(null, null));
        await _service.GetAndSaveMatch(matchId);
        var exists = await _context.Matches.AnyAsync(x => x.MatchID == matchId);
        Assert.That(exists, Is.False);
    }

    [Test]
    public async Task DownloadMedals_ShouldThrow_WhenMedalsExist()
    {
        _context.MedalDifficulties.Add(new Database.Models.MedalDifficulty { MedalDifficultyID = 1, MedalDifficultyName = "Test" });
        _context.MedalTypes.Add(new Database.Models.MedalType { MedalTypeID = 1, MedalTypeName = "Test" });
        _context.Medals.Add(new Database.Models.Medal { MedalID = 1, MedalName = "Test", Description = "desc", SpriteIndex = 0, SortingWeight = 0, MedalDifficultyID = 1, MedalTypeID = 1, PersonalScore = 0 });
        await _context.SaveChangesAsync();
        Assert.ThrowsAsync<Exception>(async () => await _service.DownloadMedals());
    }

    [Test]
    public async Task DownloadMedals_ShouldSaveMedals_WhenApiReturnsValid()
    {
        var medalsApiResult = new Surprenant.Grunt.Models.HaloInfinite.Medals.MedalMetadataResponse
        {
            Types = ["Type1"],
            Difficulties = ["Difficulty1"],
            Medals =
            [
                new()
                {
                    Name = new Surprenant.Grunt.Models.HaloInfinite.Medals.TextWithTranslations { Value = "Medal1" },
                    Description = new Surprenant.Grunt.Models.HaloInfinite.Medals.TextWithTranslations { Value = "Desc1" },
                    SpriteIndex = 0,
                    SortingWeight = 1,
                    DifficultyIndex = 0,
                    TypeIndex = 0,
                    PersonalScore = 100,
                    NameID = 1,
                }
            ]
        };
        _haloInfiniteClientFactory.Medals().Returns(new HaloApiResultContainer<Surprenant.Grunt.Models.HaloInfinite.Medals.MedalMetadataResponse, HaloApiErrorContainer>(medalsApiResult, null));
        await _service.DownloadMedals();
        Assert.Multiple(() => {
            Assert.That(_context.MedalTypes.Any(), Is.True);
            Assert.That(_context.MedalDifficulties.Any(), Is.True);
            Assert.That(_context.Medals.Any(), Is.True);
        });
    }
}
