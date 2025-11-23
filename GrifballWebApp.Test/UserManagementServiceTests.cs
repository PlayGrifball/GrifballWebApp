using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.UserManagement;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Dtos;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class UserManagementServiceTests
{
    private GrifballContext _context;
    private UserManagementService _service;
    private UserManager<User> _userManager;
    private IGetsertXboxUserService _getsertXboxUserService;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();
        _service = new UserManagementService(_context, _userManager, _getsertXboxUserService);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task GetUsers_ShouldReturnPaginatedUsers()
    {
        _context.Users.Add(new User { UserName = "TestUser", DisplayName = "TestDisplay" });
        await _context.SaveChangesAsync();
        var filter = new PaginationFilter { PageNumber = 1, PageSize = 10 };
        var result = await _service.GetUsers(filter, string.Empty, CancellationToken.None);
        Assert.That(result.Results.Length, Is.GreaterThan(0));
        Assert.That(result.Results[0].UserName, Is.EqualTo("TestUser"));
    }

    [Test]
    public async Task GetUser_ShouldReturnUser_WhenExists()
    {
        var user = new User { UserName = "TestUser", DisplayName = "TestDisplay" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var result = await _service.GetUser(user.Id, CancellationToken.None);
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UserName, Is.EqualTo("TestUser"));
    }

    [Test]
    public async Task CreateUser_ShouldReturnError_WhenUserNameTaken()
    {
        _context.Users.Add(new User { UserName = "TestUser" });
        await _context.SaveChangesAsync();
        var dto = new CreateUserRequestDto { UserName = "TestUser", Gamertag = "GT", DisplayName = "Display" };
        var result = await _service.CreateUser(dto, CancellationToken.None);
        Assert.That(result, Is.EqualTo("User name is taken"));
    }

    [Test]
    public async Task EditUser_ShouldUpdateUserProperties()
    {
        var user = new User { UserName = "TestUser", DisplayName = "OldDisplay" };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _userManager.SetUserNameAsync(user, "NewUser").Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, null).Returns(IdentityResult.Success);
        _userManager.SetLockoutEnabledAsync(user, false).Returns(IdentityResult.Success);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _userManager.AddToRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.RemoveFromRolesAsync(user, Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);

        var xboxUser = new XboxUser { Gamertag = "GT", XboxUserID = 1 };
        await _context.XboxUsers.AddAsync(xboxUser);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Is("GT"), Arg.Any<CancellationToken>()).Returns((xboxUser, null));
        var editDto = new UserResponseDto
        {
            UserID = user.Id,
            UserName = "NewUser",
            DisplayName = "NewDisplay",
            IsDummyUser = true,
            LockoutEnd = null,
            LockoutEnabled = false,
            Gamertag = "GT",
            Region = "NA",
            Discord = "discord",
            ExternalAuthCount = 0,
            HasPassword = true,
            Roles = new List<RoleDto> { new RoleDto { RoleName = "Admin", HasRole = true } }
        };
        var result = await _service.EditUser(editDto, CancellationToken.None);
        var updated = await _service.GetUser(user.Id, CancellationToken.None);
        Assert.That(result, Is.Null);
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated.DisplayName, Is.EqualTo("NewDisplay"));
        //Assert.That(updated.UserName, Is.EqualTo("NewUser")); // User manager is mocked rn so the username is not updated...
    }
}
