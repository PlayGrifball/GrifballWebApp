using GrifballWebApp.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models.HaloInfinite;
using Surprenant.Grunt.Models;
using Microsoft.EntityFrameworkCore;
using MatchType = Surprenant.Grunt.Models.HaloInfinite.MatchType;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
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
        _context = await TestDatabase.NewGrifballContext();
        _logger = Substitute.For<ILogger<DataPullService>>();
        _haloInfiniteClientFactory = Substitute.For<IHaloInfiniteClientFactory>();
        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();
        _service = new DataPullService(_logger, _haloInfiniteClientFactory, _context, _getsertXboxUserService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
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
        _context.Matches.Add(GrifballWebApp.Domain.Halo.Match.Seed(matchId));
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
        _getsertXboxUserService.GetsertXboxUserByXuid(Arg.Any<long>()).Returns(Domain.Players.XboxUser.FromXbox(long.Parse(xuid), "TestUser"));
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
        _context.MedalDifficulties.Add(Domain.Halo.MedalDifficulty.Imported(1, "Test"));
        _context.MedalTypes.Add(Domain.Halo.MedalType.Imported(1, "Test"));
        _context.Medals.Add(Domain.Halo.Medal.Imported(1, "Test", "desc", 0, 0, 1, 1, 0));
        await _context.SaveChangesAsync();
        await Assert.ThrowsAsync<Exception>(async () => await _service.DownloadMedals());
    }

    [Test]
    public async Task DownloadMedals_ShouldSaveMedals_WhenApiReturnsValid()
    {
        // Two of each list, and a medal whose every number differs, so a swapped argument or index can't pass
        var medalsApiResult = new Surprenant.Grunt.Models.HaloInfinite.Medals.MedalMetadataResponse
        {
            Types = ["Spree", "Mode"],
            Difficulties = ["Normal", "Heroic"],
            Medals =
            [
                new()
                {
                    Name = new Surprenant.Grunt.Models.HaloInfinite.Medals.TextWithTranslations { Value = "Medal1" },
                    Description = new Surprenant.Grunt.Models.HaloInfinite.Medals.TextWithTranslations { Value = "Desc1" },
                    SpriteIndex = 11,
                    SortingWeight = 22,
                    DifficultyIndex = 0,
                    TypeIndex = 1,
                    PersonalScore = 33,
                    NameID = 4444,
                }
            ]
        };
        _haloInfiniteClientFactory.Medals().Returns(new HaloApiResultContainer<Surprenant.Grunt.Models.HaloInfinite.Medals.MedalMetadataResponse, HaloApiErrorContainer>(medalsApiResult, null));
        await _service.DownloadMedals();
        _context.ChangeTracker.Clear();

        var types = await _context.MedalTypes.OrderBy(t => t.MedalTypeID).Select(t => new { t.MedalTypeID, t.MedalTypeName }).ToListAsync();
        var difficulties = await _context.MedalDifficulties.OrderBy(d => d.MedalDifficultyID).Select(d => new { d.MedalDifficultyID, d.MedalDifficultyName }).ToListAsync();
        var medal = await _context.Medals.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            // Each ID is its index in Halo's list + 1
            Assert.That(types.Select(t => (t.MedalTypeID, t.MedalTypeName)), Is.EqualTo(new[] { (1, "Spree"), (2, "Mode") }));
            Assert.That(difficulties.Select(d => (d.MedalDifficultyID, d.MedalDifficultyName)), Is.EqualTo(new[] { (1, "Normal"), (2, "Heroic") }));
            Assert.That(medal.MedalID, Is.EqualTo(4444));
            Assert.That(medal.MedalName, Is.EqualTo("Medal1"));
            Assert.That(medal.Description, Is.EqualTo("Desc1"));
            Assert.That(medal.SpriteIndex, Is.EqualTo(11));
            Assert.That(medal.SortingWeight, Is.EqualTo(22));
            Assert.That(medal.PersonalScore, Is.EqualTo(33));
            Assert.That(medal.MedalTypeID, Is.EqualTo(2), "TypeIndex 1 + 1");
            Assert.That(medal.MedalDifficultyID, Is.EqualTo(1), "DifficultyIndex 0 + 1");
        }
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_ShouldSaveMatches()
    {
        var xuid = 123456789L;
        var matchId = Guid.NewGuid();
        var teamId = 1;
        var gamertag = "TestUser";
        var xboxIDs = new List<long> { xuid };
        // Mock match history response
        var matchHistoryResult = new MatchHistoryResponse
        {
            Results = [
                new PlayerMatchHistoryRecord
                {
                    MatchId = matchId.ToString(),
                    MatchInfo = new MatchInfo
                    {
                        GameVariantCategory = (GameVariantCategory)41,
                    }
                }
            ]
        };
        // All other pages return empty
        _haloInfiniteClientFactory.StatsGetMatchHistory(Arg.Is<string>("xuid(123456789)"), Arg.Any<int>(), Arg.Is(25), Arg.Any<MatchType>())
            .Returns(new HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>(new MatchHistoryResponse()
            {
                Count = 0,
                Results = [],
            }, null));
        // Mock the first page
        _haloInfiniteClientFactory.StatsGetMatchHistory(Arg.Is<string>("xuid(123456789)"), Arg.Is(0), Arg.Is(25), Arg.Any<MatchType>())
            .Returns(new HaloApiResultContainer<MatchHistoryResponse, HaloApiErrorContainer>(matchHistoryResult, null));
        
        // Mock match stats response
        var matchStats = new MatchStats
        {
            MatchId = matchId,
            MatchInfo = new MatchInfo
            {
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow.AddMinutes(10),
                Duration = TimeSpan.FromMinutes(10),
            },
            Teams = [
                new Team
                {
                    TeamId = teamId,
                    Outcome = 2,
                    Stats = new Stats
                    {
                        CoreStats = new CoreStats { Score = 10 }
                    }
                }
            ],
            Players = [
                new Player
                {
                    PlayerId = $"xuid({xuid})",
                    LastTeamId = teamId,
                    PlayerTeamStats = [
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

        _getsertXboxUserService.GetsertXboxUsersByXuid(Arg.Is<long[]>(x => x.Length == 1 && x.Contains(xuid)), Arg.Any<CancellationToken>())
            .Returns([Domain.Players.XboxUser.FromXbox(xuid, gamertag)]);
        _getsertXboxUserService.GetsertXboxUserByXuid(Arg.Is(xuid), Arg.Any<CancellationToken>())
            .Returns(Domain.Players.XboxUser.FromXbox(xuid, gamertag));

        await _service.DownloadRecentMatchesForPlayers(xboxIDs, endPage: 0);
        var matchExists = await _context.Matches.AnyAsync(x => x.MatchID == matchId);
        Assert.That(matchExists, Is.True);
    }
}
