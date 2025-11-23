using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class ProfileControllerTests
{
    private GrifballContext _context;
    private ISetGamertagService _setGamertagService;
    private ProfileController _controller;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _setGamertagService = Substitute.For<ISetGamertagService>();
        _controller = new ProfileController(_context, _setGamertagService);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task GetGamertag_Should_ReturnNull_When_UserDoesNotExist()
    {
        // Arrange
        const int nonExistentUserId = 999;

        // Act
        var result = await _controller.GetGamertag(nonExistentUserId, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetGamertag_Should_ReturnGamertag_When_UserHasXboxUser()
    {
        // Arrange
        var user = new User { UserName = "testuser", DisplayName = "Test User" };
        var xboxUser = new XboxUser { Gamertag = "TestGamer123", XboxUserID = 12345 };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        user.XboxUserID = xboxUser.XboxUserID;
        await _context.XboxUsers.AddAsync(xboxUser);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetGamertag(user.Id, CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Gamertag, Is.EqualTo("TestGamer123"));
    }

    [Test]
    public async Task GetGamertag_Should_ReturnDifferentGamertags_When_MultipleUsersExist()
    {
        // Arrange
        var user1 = new User { UserName = "user1", DisplayName = "User 1" };
        var user2 = new User { UserName = "user2", DisplayName = "User 2" };
        var xboxUser1 = new XboxUser { Gamertag = "Gamer1", XboxUserID = 111 };
        var xboxUser2 = new XboxUser { Gamertag = "Gamer2", XboxUserID = 222 };

        await _context.Users.AddRangeAsync(user1, user2);
        await _context.SaveChangesAsync();

        user1.XboxUserID = xboxUser1.XboxUserID;
        user2.XboxUserID = xboxUser2.XboxUserID;
        await _context.XboxUsers.AddRangeAsync(xboxUser1, xboxUser2);
        await _context.SaveChangesAsync();

        // Act
        var result1 = await _controller.GetGamertag(user1.Id, CancellationToken.None);
        var result2 = await _controller.GetGamertag(user2.Id, CancellationToken.None);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result1!.Gamertag, Is.EqualTo("Gamer1"));
            Assert.That(result2!.Gamertag, Is.EqualTo("Gamer2"));
        });
    }
}
