using EntityFrameworkCore.Testing.NSubstitute;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class SetGamertagServiceTests
{
    private GrifballContext _context;
    private IGetsertXboxUserService _getsertXboxUserService;
    private IUserMergeService _userMergeService;
    private SetGamertagService _setGamertagService;

    [SetUp]
    public void Setup()
    {
        // Configure in-memory database with unique name per test
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique database per test
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = Create.MockedDbContextFor<GrifballContext>(options);
        // TODO: Mock transaction methods better, or switch to testcontainers.
        _context.StartTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _context.CommitTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _context.RollbackTransactionAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();

        // We'll test user merge service too
        _userMergeService = new UserMergeService(_context);

        _setGamertagService = new SetGamertagService(
            _context,
            _getsertXboxUserService,
            _userMergeService
        );
    }

    [Test]
    public async Task CanSetGamertag()
    {
        // Arrange  
        var user = new User
        {
            Id = 1
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre",
                User = null // Simulating a new gamertag not associated with any user
            }, null));

        // Act  
        var result = await _setGamertagService.SetGamertag(1, "Grunt Padre");
        var userAfter = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == 1)
            .FirstOrDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(userAfter, Is.Not.Null);
            Assert.That(userAfter?.XboxUser, Is.Not.Null);
            Assert.That(userAfter?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"));
        });
    }

    [Test]
    public async Task CannotStealGamertag()
    {
        // Arrange  
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            }
        };
        _context.Users.Add(user);
        var user2 = new User
        {
            Id = 2,
        };
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user.XboxUser, null));

        // Act  
        var result = await _setGamertagService.SetGamertag(2, "Grunt Padre");

        Assert.That(result, Is.EqualTo("That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect"));
    }

    [Test]
    public async Task CannotSetIfUserDoesNotExist()
    {
        // Act  
        var result = await _setGamertagService.SetGamertag(1, "Grunt Padre");

        Assert.That(result, Is.EqualTo("User does not exist"));
    }

    [Test]
    public async Task CannotSetAlreadySetGamertag()
    {
        // Arrange  
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act  
        var result = await _setGamertagService.SetGamertag(1, "New Example Tag - Doesn't matter for test");

        Assert.That(result, Is.EqualTo("Gamertag is already set for this user. Contact sysadmin if it needs to be changed"));
    }

    [Test]
    public async Task HandleGetsertError()
    {
        // Arrange  
        var user = new User
        {
            Id = 1,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((null, "foobar"));

        // Act  
        var result = await _setGamertagService.SetGamertag(1, "New Example Tag - Doesn't matter for test");

        Assert.That(result, Is.EqualTo("Failed to set gamertag, reason: foobar"));
    }

    [Test]
    public async Task AllowDummyTakeover()
    {
        // Arrange
        var season = new Season()
        {
            SeasonName = "Test Season",
        };
        var teamplayer = new TeamPlayer
        {
            Team = new Team()
            {
                Season = season,
            },
        };
        _context.TeamPlayers.Add(teamplayer);
        var seasonSignup = new SeasonSignup
        {
            Season = season,
        };
        _context.SeasonSignups.Add(seasonSignup);
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            },
            DiscordUser = new DiscordUser
            {
                DiscordUserID = 1337,
                DiscordUsername = "discordusernamegohere",
            },
            TeamPlayers = [
                teamplayer,
                ],
            // PersonExperiences
            SeasonSignups = [
                seasonSignup,
                ],
            IsDummyUser = true // Simulating a dummy user
        };
        _context.Users.Add(user);
        var user2 = new User
        {
            Id = 2,
        };
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user.XboxUser, null));

        // Act  
        var result = await _setGamertagService.SetGamertag(2, "Grunt Padre");
        var user1After = await _context.Users
            .Where(x => x.Id == 1)
            .FirstOrDefaultAsync();
        var user2After = await _context.Users
            .Include(x => x.XboxUser)
            .Include(x => x.DiscordUser)
            .Include(x => x.TeamPlayers).ThenInclude(x => x.Team.Season)
            .Include(x => x.SeasonSignups).ThenInclude(x => x.Season)
            .Where(x => x.Id == 2)
            .FirstOrDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(user1After, Is.Null); // Should have been deleted (dummy user was merged into new real account)
            Assert.That(user2After, Is.Not.Null);
            // Now from this line on lets make sure that user2 owns everything that user1 did own prior
            Assert.That(user2After?.XboxUser, Is.Not.Null);
            Assert.That(user2After?.XboxUser?.XboxUserID, Is.EqualTo(1234567890));
            Assert.That(user2After?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"));
            Assert.That(user2After?.DiscordUser, Is.Not.Null);
            Assert.That(user2After?.DiscordUser?.DiscordUserID, Is.EqualTo(1337));
            Assert.That(user2After?.DiscordUser?.DiscordUsername, Is.EqualTo("discordusernamegohere"));
            Assert.That(user2After?.TeamPlayers, Is.Not.Null);
            Assert.That(user2After?.TeamPlayers.Count, Is.EqualTo(1));
            Assert.That(user2After?.TeamPlayers.ElementAtOrDefault(0)?.Team.Season.SeasonName, Is.EqualTo("Test Season"));
            Assert.That(user2After?.SeasonSignups, Is.Not.Null);
            Assert.That(user2After?.SeasonSignups.Count, Is.EqualTo(1));
            Assert.That(user2After?.SeasonSignups.ElementAtOrDefault(0)?.Season.SeasonName, Is.EqualTo("Test Season"));
        });
    }
}
