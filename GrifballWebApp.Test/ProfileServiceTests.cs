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
public class ProfileServiceTests
{
    private GrifballContext _context;
    private IGetsertXboxUserService _getsertXboxUserService;
    private ProfileService _profileService;

    [SetUp]
    public void Setup()
    {
        // Configure in-memory database with unique name per test
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique database per test
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = Create.MockedDbContextFor<GrifballContext>(options);

        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();

        _profileService = new ProfileService(
            _context,
            _getsertXboxUserService
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
        var result = await _profileService.SetGamertag(1, "Grunt Padre");
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
        var result = await _profileService.SetGamertag(2, "Grunt Padre");

        Assert.That(result, Is.EqualTo("That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect"));
    }

    [Test]
    public async Task CannotSetIfUserDoesNotExist()
    {
        // Act  
        var result = await _profileService.SetGamertag(1, "Grunt Padre");

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
        var result = await _profileService.SetGamertag(1, "New Example Tag - Doesn't matter for test");

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
        var result = await _profileService.SetGamertag(1, "New Example Tag - Doesn't matter for test");

        Assert.That(result, Is.EqualTo("Failed to set gamertag, reason: foobar"));
    }

    [Test]
    public async Task AllowDummyTakeover()
    {
        // Arrange  
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            },
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
        var result = await _profileService.SetGamertag(2, "Grunt Padre");
        var user1After = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == 1)
            .FirstOrDefaultAsync();
        var user2After = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == 2)
            .FirstOrDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(user1After, Is.Null); // Should have been deleted (dummy user was merged into new real account)
            Assert.That(user2After, Is.Not.Null);
            Assert.That(user2After?.XboxUser, Is.Not.Null);
            Assert.That(user2After?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"));
        });
    }

    // Could use another test or two of more advanced dummy take over. Can we port over things the dummy owned to the new account?
}
