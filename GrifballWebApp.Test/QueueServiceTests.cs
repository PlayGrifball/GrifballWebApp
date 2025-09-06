using DiscordInterface.Generated;
using Docker.DotNet.Models;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Seeder;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class QueueServiceTests
{
    private ILogger<QueueService> _logger;
    private IQueueRepository _queueRepository;
    private IDataPullService _dataPullService;
    private GrifballContext _context;
    private IDiscordRestClient _discordClient;
    private IOptions<DiscordOptions> _discordOptions;

    private QueueService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();

        // Substitute dependencies
        _logger = Substitute.For<ILogger<QueueService>>();
        _queueRepository = new QueueRepository(_context);
        _dataPullService = Substitute.For<IDataPullService>();
        _discordClient = Substitute.For<IDiscordRestClient>();
        var msg = Substitute.For<IDiscordRestMessage>();
        var author = Substitute.For<IDiscordUser>();
        author.Id.Returns(1ul); // Should match bot id
        msg.Author.Returns(author);
        _discordClient.SendMessageAsync(Arg.Any<ulong>(), Arg.Any<MessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(msg);
        var thread = Substitute.For<IDiscordGuildThread>();
        thread.Id.Returns(1ul);
        _discordClient.CreateGuildThreadAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<GuildThreadFromMessageProperties>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns(thread);
        _discordOptions = Substitute.For<IOptions<DiscordOptions>>();
        _discordOptions.Value.Returns(new DiscordOptions
        {
            QueueChannel = 123456789012345678, // Example channel ID
            MatchPlayers = 8,
        });

        // Initialize the service
        _service = new QueueService(_logger, _discordOptions, _queueRepository, _discordClient, _context, _dataPullService);

        // Seed ranks
        await new RankSeeder(_context, new TestReader()).SeedRanks();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task ShouldHaveRanks()
    {
        // Arrange
        // No specific arrangement needed for this test. Currently, arranged in setup. TODO: move this test to its own file.

        // Act
        var hasRanks = await _context.Ranks.AnyAsync();

        // Assert
        Assert.That(hasRanks, Is.True, "There should be ranks");
    }

    [Test]
    public async Task Go_ShouldCreateMatch_WhenEnoughPlayersInQueue()
    {
        var now = DateTime.UtcNow;
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        _context.XboxUsers.AddRange([
            new() { XboxUserID = 1, Gamertag = "1" },
            new() { XboxUserID = 2, Gamertag = "2" },
            new() { XboxUserID = 3, Gamertag = "3" },
            new() { XboxUserID = 4, Gamertag = "4" },
            new() { XboxUserID = 5, Gamertag = "5" },
            new() { XboxUserID = 6, Gamertag = "6" },
            new() { XboxUserID = 7, Gamertag = "7" },
            new() { XboxUserID = 8, Gamertag = "8" },
            new() { XboxUserID = 9, Gamertag = "9" },
            new() { XboxUserID = 10, Gamertag = "10" },
            ]);
        await _context.SaveChangesAsync();
        var queuedPlayers = new[]
        {
            new QueuedPlayer { UserID = 1, JoinedAt = now.AddMinutes(-9), User = new User { MMR = 1000, Id = 1, XboxUserID = 1 } },
            new QueuedPlayer { UserID = 2, JoinedAt = now.AddMinutes(-8), User = new User { MMR = 1000, Id = 2, XboxUserID = 2 } },
            new QueuedPlayer { UserID = 3, JoinedAt = now.AddMinutes(-7), User = new User { MMR = 1000, Id = 3, XboxUserID = 3 } },
            new QueuedPlayer { UserID = 4, JoinedAt = now.AddMinutes(-6), User = new User { MMR = 1000, Id = 4, XboxUserID = 4 } },
            new QueuedPlayer { UserID = 5, JoinedAt = now.AddMinutes(-5), User = new User { MMR = 1000, Id = 5, XboxUserID = 5 } },
            new QueuedPlayer { UserID = 6, JoinedAt = now.AddMinutes(-4), User = new User { MMR = 1000, Id = 6, XboxUserID = 6 } },
            new QueuedPlayer { UserID = 7, JoinedAt = now.AddMinutes(-3), User = new User { MMR = 1000, Id = 7, XboxUserID = 7 } },
            new QueuedPlayer { UserID = 8, JoinedAt = now.AddMinutes(-2), User = new User { MMR = 1000, Id = 8, XboxUserID = 8 } },
            new QueuedPlayer { UserID = 9, JoinedAt = now.AddMinutes(-1), User = new User { MMR = 1000, Id = 9, XboxUserID = 9 } },
            new QueuedPlayer { UserID = 10, JoinedAt = now.AddMinutes(-1), User = new User { MMR = 1000, Id = 10, XboxUserID = 10 } },
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatches
            .Where(x => x.Active)
            .CountAsync();
        var stillQueued = await _context.QueuedPlayer
            .Select(x => x.UserID)
            .ToArrayAsync();
        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1), "There should be one active match created when 8 players have been queued");
            Assert.That(stillQueued.Count(), Is.EqualTo(2), "There should two players left in the queue since we started with 10 queued players");
            Assert.That(stillQueued.Any(x => x is 9), Is.True, "Player 9 should still be in the queue since he queued recently");
            Assert.That(stillQueued.Any(x => x is 10), Is.True, "Player 10 should still be in the queue since he queued recently");
        });

        // Check the dataPullerService check match has been called 
        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(x => x.Count == 0), 0, 0, 2, Arg.Any<CancellationToken>());

        // Arrange - Fake match
        var start = now.AddSeconds(10);
        var end = now.AddMinutes(12);
        var duration = end - start;
        var match = new Match()
        {
            MatchID = Guid.NewGuid(),
            StartTime = start,
            EndTime = end,
            Duration = duration,
            StatsPullDate = DateTime.UtcNow,
        };
        _context.Matches.Add(match);

        var team0 = new MatchTeam()
        {
            Match = match,
            TeamID = 0,
            Score = 5,
            Outcome = Outcomes.Won,
            MatchParticipants = [
                new MatchParticipant { XboxUserID = 1},
                new MatchParticipant { XboxUserID = 3},
                new MatchParticipant { XboxUserID = 5},
                new MatchParticipant { XboxUserID = 7},
            ],
        };
        var team1 = new MatchTeam()
        {
            Match = match,
            TeamID = 1,
            Score = 3,
            Outcome = Outcomes.Lost,
            MatchParticipants = [
                new MatchParticipant { XboxUserID = 2},
                new MatchParticipant { XboxUserID = 4},
                new MatchParticipant { XboxUserID = 6},
                new MatchParticipant { XboxUserID = 8},
            ],
        };
        await _context.MatchTeams.AddRangeAsync([team0, team1]);
        match.MatchTeams = [team0, team1];

        await _context.SaveChangesAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        // Matched match should be created now not be active
        var count2 = await _context.MatchedMatches
            .Where(x => x.Active)
            .CountAsync();
        var count3 = await _context.MatchedMatches
            .Where(x => !x.Active)
            .CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(count2, Is.EqualTo(0), "There should be no active matches after we pull in a 'matched' infinite match");
            Assert.That(count3, Is.EqualTo(1), "There previously active match has been complete after we pull in a 'matched' infinite match");
        });

        // Check the players MMR has been adjusted
        var winners = await _context.Users
            .Where(x => x.Id == 1 || x.Id == 3 || x.Id == 5 || x.Id == 7)
            .Select(x => x.MMR)
            .ToArrayAsync();
        var losers = await _context.Users
            .Where(x => x.Id == 2 || x.Id == 4 || x.Id == 6 || x.Id == 8)
            .Select(x => x.MMR)
            .ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(winners.Length, Is.EqualTo(4), "There should be 4 winners");
            Assert.That(losers.Length, Is.EqualTo(4), "There should be 4 losers");
            Assert.That(winners.All(x => x > 1000), Is.True, "All winners should have an MMR greater than 1000");
            Assert.That(losers.All(x => x < 1000), Is.True, "All losers should have an MMR less than 1000");
        });
    }

    [Test]
    public async Task Go_ShouldNotCreateMatch_WhenNotEnoughPlayersInQueue()
    {
        //Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var queuedPlayers = new[]
        {
            new QueuedPlayer { UserID = 9, User = new User { MMR = 1000, Id = 9 } },
        };
        _context.QueuedPlayer.AddRange(queuedPlayers);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Act
        await _service.Go(CancellationToken.None);

        // Assert
        var count = await _context.MatchedMatches
            .Where(x => x.Active)
            .CountAsync();
        Assert.That(count, Is.EqualTo(0), "There should be no active matches when only 1 player has been queued");
    }
}
