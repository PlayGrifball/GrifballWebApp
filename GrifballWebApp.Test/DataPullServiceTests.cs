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
    public async Task GetAndSaveMatch_ShouldSave()
    {
        var matchId = Guid.NewGuid();
        var teamId = 1;
        var xuid = "123456789";
        var matchStats = new MatchStats
        {
            MatchId = matchId,
            MatchInfo = new MatchInfo
            {
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddMinutes(10),
                Duration = TimeSpan.FromMinutes(10),
            },
            Teams =
            [
                new Team
                {
                    TeamId = teamId,
                    Outcome = 2, // Won
                    Stats = new Stats
                    {
                        CoreStats = new CoreStats
                        {
                            Score = 10
                        }
                    }
                }
            ],
            Players =
            [
                new Player
                {
                    PlayerId = $"xuid({xuid})",
                    LastTeamId = teamId,
                    PlayerTeamStats =
                    [
                        new PlayerTeamStat
                        {
                            TeamId = teamId,
                            Stats = new Stats
                            {
                                CoreStats = new CoreStats
                                {
                                    Score = 5,
                                    PersonalScore = 3,
                                    Kills = 2,
                                    Deaths = 1,
                                    Assists = 1,
                                    KDA = 2.0f,
                                    Suicides = 0,
                                    Betrayals = 0,
                                    AverageLifeDuration = TimeSpan.FromSeconds(30),
                                    MeleeKills = 1,
                                    PowerWeaponKills = 0,
                                    ShotsFired = 10,
                                    ShotsHit = 5,
                                    Accuracy = 50.0f,
                                    DamageDealt = 100,
                                    DamageTaken = 80,
                                    CalloutAssists = 0,
                                    MaxKillingSpree = 1,
                                    RoundsLost = 0,
                                    RoundsTied = 0,
                                    RoundsWon = 1,
                                    Spawns = 1,
                                    ObjectivesCompleted = 0,
                                    Medals = []
                                }
                            }
                        }
                    ],
                    ParticipationInfo = new ParticipationInfo
                    {
                        FirstJoinedTime = DateTimeOffset.UtcNow,
                        JoinedInProgress = false,
                        LastLeaveTime = null,
                        LeftInProgress = false,
                        PresentAtBeginning = true,
                        PresentAtCompletion = true,
                        TimePlayed = TimeSpan.FromMinutes(10)
                    },
                    Rank = 1
                }
            ]
        };
        _haloInfiniteClientFactory.StatsGetMatchStats(matchId).Returns(new HaloApiResultContainer<MatchStats, HaloApiErrorContainer>(matchStats, null));
        _getsertXboxUserService.GetsertXboxUserByXuid(Arg.Any<long>()).Returns(new Database.Models.XboxUser { XboxUserID = long.Parse(xuid), Gamertag = "TestUser" });
        await _service.GetAndSaveMatch(matchId);
        var match = await _context.Matches
            .Include(x => x.MatchTeams)
                .ThenInclude(mt => mt.MatchParticipants)
                    .ThenInclude(mp => mp.XboxUser)
            .FirstOrDefaultAsync(x => x.MatchID == matchId);
        Assert.That(match, Is.Not.Null);
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
