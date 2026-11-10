using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.UserManagement;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserManagementServiceBranchTests
{
    private GrifballContext _context;
    private UserManager<User> _userManager;
    private IGetsertXboxUserService _getsert;
    private UserManagementService _service;
    private static readonly string[] expected = new[] { "alice", "bob", "carl" };
    private static readonly string[] second = new[] { "Sysadmin" };
    private static readonly string[] secondArray = new[] { "Commissioner" };

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null, null, null, null, null, null, null, null);
        _getsert = Substitute.For<IGetsertXboxUserService>();
        _service = new UserManagementService(_context, _userManager, _getsert);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<(User alice, User bob, User carl)> SeedUsers()
    {
        var alice = User.Seed(userName: "alice", displayName: "Zed", xboxUser: XboxUser.FromXbox(11, "AliceGT"));
        var bob = User.Seed(userName: "bob", displayName: "Bobby");
        var carl = User.Seed(userName: "carl", displayName: "Carlos", xboxUser: XboxUser.FromXbox(12, "SearchMeGT"));
        _context.Users.AddRange(alice, bob, carl);
        await _context.SaveChangesAsync();
        return (alice, bob, carl);
    }

    // ---------- GetUsers ----------

    [Test]
    public async Task GetUsers_NoSort_OrdersByIdDescending()
    {
        var (alice, bob, carl) = await SeedUsers();

        var result = await _service.GetUsers(new PaginationFilter(1, 10), "", CancellationToken.None);

        var ours = result.Results.Select(r => r.UserID).Where(id => id == alice.Id || id == bob.Id || id == carl.Id).ToArray();
        Assert.That(ours, Has.Length.EqualTo(3));
        Assert.That(ours, Is.EqualTo(new[] { alice.Id, bob.Id, carl.Id }.OrderByDescending(x => x).ToArray()));
    }

    [Test]
    public async Task GetUsers_SortByUserNameAscending()
    {
        await SeedUsers();

        var filter = new PaginationFilter(1, 10) { SortColumn = "UserName", SortDirection = SortDirection.Asc };
        var result = await _service.GetUsers(filter, "", CancellationToken.None);

        var names = result.Results.Select(r => r.UserName).ToList();
        Assert.That(names, Is.Ordered);
        Assert.That(names, Is.SupersetOf(expected));
    }

    [TestCase("bob", "bob")]        // user name
    [TestCase("Carlos", "carl")]    // display name
    [TestCase("SearchMe", "carl")]  // gamertag
    public async Task GetUsers_SearchMatchesUserNameDisplayNameOrGamertag(string search, string expectedUser)
    {
        await SeedUsers();

        var result = await _service.GetUsers(new PaginationFilter(1, 10), search, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TotalCount, Is.EqualTo(1));
            Assert.That(result.Results.Single().UserName, Is.EqualTo(expectedUser));
        }
    }

    [Test]
    public async Task GetUser_MapsGamertagAndRoles()
    {
        var (alice, _, _) = await SeedUsers();
        var role = new Role { Name = "TestRole", NormalizedName = "TESTROLE" };
        var otherRole = new Role { Name = "OtherRole", NormalizedName = "OTHERROLE" };
        _context.Roles.AddRange(role, otherRole);
        await _context.SaveChangesAsync();
        _context.UserRoles.Add(new UserRole { UserId = alice.Id, RoleId = role.Id });
        await _context.SaveChangesAsync();

        var dto = await _service.GetUser(alice.Id, CancellationToken.None);

        Assert.That(dto, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto!.Gamertag, Is.EqualTo("AliceGT"));
            Assert.That(dto.HasPassword, Is.False);
            Assert.That(dto.Roles.Single(r => r.RoleName == "TestRole").HasRole, Is.True);
            Assert.That(dto.Roles.Single(r => r.RoleName == "OtherRole").HasRole, Is.False);
        }
    }

    [Test]
    public async Task GetUser_Missing_ReturnsNull()
    {
        Assert.That(await _service.GetUser(424242, CancellationToken.None), Is.Null);
    }

    // ---------- CreateUser ----------

    private static CreateUserRequestDto CreateReq() => new() { UserName = "newbie", Gamertag = "NewGT", DisplayName = "New Person" };

    [Test]
    public async Task CreateUser_XboxLookupFails_ReturnsServiceMessage()
    {
        _getsert.GetsertXboxUserByGamertag("NewGT", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, "Gamertag not found"));

        var result = await _service.CreateUser(CreateReq(), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Gamertag not found"));
        await _userManager.DidNotReceiveWithAnyArgs().CreateAsync(default!);
    }

    [Test]
    public async Task CreateUser_XboxLookupFailsWithoutMessage_ReturnsDefaultMessage()
    {
        _getsert.GetsertXboxUserByGamertag("NewGT", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, (string?)null));

        var result = await _service.CreateUser(CreateReq(), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to get xbox user, unknown reason"));
    }

    [Test]
    public async Task CreateUser_UserManagerFails_ReturnsErrorDescriptions()
    {
        var xbox = XboxUser.FromXbox(50, "NewGT");
        _getsert.GetsertXboxUserByGamertag("NewGT", Arg.Any<CancellationToken>()).Returns((xbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(
            new IdentityError { Description = "Bad 1" }, new IdentityError { Description = "Bad 2" }));

        var result = await _service.CreateUser(CreateReq(), CancellationToken.None);

        Assert.That(result, Is.EqualTo($"Bad 1{Environment.NewLine}Bad 2{Environment.NewLine}"));
    }

    [Test]
    public async Task CreateUser_Success_CreatesUserWithXboxUserAndDisplayName()
    {
        var xbox = XboxUser.FromXbox(50, "NewGT");
        _getsert.GetsertXboxUserByGamertag("NewGT", Arg.Any<CancellationToken>()).Returns((xbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Success);

        var result = await _service.CreateUser(CreateReq(), CancellationToken.None);

        Assert.That(result, Is.Null);
        await _userManager.Received(1).CreateAsync(Arg.Is<User>(u =>
            u.UserName == "newbie" && u.DisplayName == "New Person" && u.XboxUser == xbox));
        await _userManager.Received(1).AddToRoleAsync(Arg.Is<User>(u => u.UserName == "newbie"), "Player");
    }

    [Test]
    public async Task CreateUser_PlayerRoleFails_ReturnsError()
    {
        var xbox = XboxUser.FromXbox(50, "NewGT");
        _getsert.GetsertXboxUserByGamertag("NewGT", Arg.Any<CancellationToken>()).Returns((xbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Failed());

        var result = await _service.CreateUser(CreateReq(), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to add the Player role"));
    }

    [Test]
    public async Task CreateUser_GamertagOwnedByOtherUser_TakesGamertagFromThem()
    {
        var (alice, _, _) = await SeedUsers();
        var aliceXbox = await _context.XboxUsers.Include(x => x.User).SingleAsync(x => x.XboxUserID == 11);
        _getsert.GetsertXboxUserByGamertag("NewGT", Arg.Any<CancellationToken>()).Returns((aliceXbox, (string?)null));
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _userManager.AddToRoleAsync(Arg.Any<User>(), "Player").Returns(IdentityResult.Success);

        var result = await _service.CreateUser(CreateReq(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(alice.XboxUserID, Is.Null, "previous owner should lose the gamertag");
        }
        await _userManager.Received(1).CreateAsync(Arg.Is<User>(u => u.XboxUser == aliceXbox));
    }

    // ---------- EditUser ----------

    private static UserResponseDto EditFrom(User u, string? gamertag, List<RoleDto>? roles = null) => new()
    {
        UserID = u.Id,
        UserName = u.UserName!,
        DisplayName = "Edited",
        IsDummyUser = true,
        LockoutEnd = u.LockoutEnd,
        LockoutEnabled = u.LockoutEnabled,
        Gamertag = gamertag,
        Region = null,
        Discord = null,
        ExternalAuthCount = 0,
        HasPassword = false,
        Roles = roles ?? [],
    };

    private void NoRoles(User u) => _userManager.GetRolesAsync(Arg.Is<User>(x => x.Id == u.Id)).Returns(new List<string>());

    [Test]
    public async Task EditUser_UserMissing_Throws()
    {
        var dto = new UserResponseDto { UserID = 999999, UserName = "x", Region = null, DisplayName = null, Gamertag = null, Discord = null, ExternalAuthCount = 0, HasPassword = false, Roles = [] };

        var ex = await Assert.ThrowsAsync<Exception>(() => _service.EditUser(dto, CancellationToken.None));
        Assert.That(ex!.Message, Is.EqualTo("User does not exist"));
    }

    [Test]
    public async Task EditUser_ClearGamertag_UnlinksXboxUserAndSavesFields()
    {
        var (alice, _, _) = await SeedUsers();
        NoRoles(alice);

        var result = await _service.EditUser(EditFrom(alice, ""), CancellationToken.None);

        Assert.That(result, Is.Null);
        _context.ChangeTracker.Clear();
        var reloaded = await _context.Users.SingleAsync(u => u.Id == alice.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reloaded.XboxUserID, Is.Null);
            Assert.That(reloaded.DisplayName, Is.EqualTo("Edited"));
            Assert.That(reloaded.IsDummyUser, Is.True);
        }
        await _getsert.DidNotReceiveWithAnyArgs().GetsertXboxUserByGamertag(default!, default);
    }

    [Test]
    public async Task EditUser_SameGamertagDifferentCase_DoesNotLookUp()
    {
        var (alice, _, _) = await SeedUsers();
        NoRoles(alice);

        var result = await _service.EditUser(EditFrom(alice, "alicegt"), CancellationToken.None);

        Assert.That(result, Is.Null);
        await _getsert.DidNotReceiveWithAnyArgs().GetsertXboxUserByGamertag(default!, default);
    }

    [Test]
    public async Task EditUser_GamertagLookupFails_ReturnsMessage()
    {
        var (_, bob, _) = await SeedUsers();
        _getsert.GetsertXboxUserByGamertag("Nope", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, (string?)null));

        var result = await _service.EditUser(EditFrom(bob, "Nope"), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to get xbox user, unknown reason"));
    }

    [Test]
    public async Task EditUser_GamertagOwnedByOtherUser_IsTransferred()
    {
        var (alice, bob, _) = await SeedUsers();
        NoRoles(bob);
        var aliceXbox = await _context.XboxUsers.Include(x => x.User).SingleAsync(x => x.XboxUserID == 11);
        _getsert.GetsertXboxUserByGamertag("AliceGT", Arg.Any<CancellationToken>()).Returns((aliceXbox, (string?)null));

        var result = await _service.EditUser(EditFrom(bob, "AliceGT"), CancellationToken.None);

        Assert.That(result, Is.Null);
        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _context.Users.SingleAsync(u => u.Id == bob.Id)).XboxUserID, Is.EqualTo(11));
            Assert.That((await _context.Users.SingleAsync(u => u.Id == alice.Id)).XboxUserID, Is.Null);
        }
    }

    /// <summary>User.TieToXbox refuses a user tied to another account and an account another user holds: a sysadmin's edit
    /// unties both and ties the new one.</summary>
    [Test]
    public async Task EditUser_GamertagOwnedByOtherUser_ReplacesTheUsersOwnAccount()
    {
        var (alice, _, carl) = await SeedUsers();
        NoRoles(carl);
        var aliceXbox = await _context.XboxUsers.Include(x => x.User).SingleAsync(x => x.XboxUserID == 11);
        _getsert.GetsertXboxUserByGamertag("AliceGT", Arg.Any<CancellationToken>()).Returns((aliceXbox, (string?)null));

        var result = await _service.EditUser(EditFrom(carl, "AliceGT"), CancellationToken.None);

        Assert.That(result, Is.Null);
        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((await _context.Users.SingleAsync(u => u.Id == carl.Id)).XboxUserID, Is.EqualTo(11));
            Assert.That((await _context.Users.SingleAsync(u => u.Id == alice.Id)).XboxUserID, Is.Null);
            Assert.That(await _context.XboxUsers.AnyAsync(x => x.XboxUserID == 12), Is.True, "Carl's old account stays, untied");
        }
    }

    [Test]
    public async Task EditUser_LockoutEndFails_ReturnsMessage()
    {
        var (_, bob, _) = await SeedUsers();
        var dto = EditFrom(bob, null);
        dto.LockoutEnd = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _userManager.SetLockoutEndDateAsync(Arg.Any<User>(), dto.LockoutEnd).Returns(IdentityResult.Failed());

        Assert.That(await _service.EditUser(dto, CancellationToken.None), Is.EqualTo("Failed to set lockout end date"));
    }

    [Test]
    public async Task EditUser_LockoutEnabledFails_ReturnsMessage()
    {
        var (_, bob, _) = await SeedUsers();
        var dto = EditFrom(bob, null);
        dto.LockoutEnabled = !bob.LockoutEnabled;
        _userManager.SetLockoutEnabledAsync(Arg.Any<User>(), dto.LockoutEnabled).Returns(IdentityResult.Failed());

        Assert.That(await _service.EditUser(dto, CancellationToken.None), Is.EqualTo("Failed to set lockout enabled"));
    }

    [Test]
    public async Task EditUser_SetUserNameFails_ReturnsMessage()
    {
        var (_, bob, _) = await SeedUsers();
        var dto = EditFrom(bob, null);
        dto.UserName = "robert";
        _userManager.SetUserNameAsync(Arg.Any<User>(), "robert").Returns(IdentityResult.Failed());

        Assert.That(await _service.EditUser(dto, CancellationToken.None), Is.EqualTo("Failed to set username"));
    }

    [Test]
    public async Task EditUser_AllSettersSucceed_CallsUserManager()
    {
        var (_, bob, _) = await SeedUsers();
        NoRoles(bob);
        var dto = EditFrom(bob, null);
        dto.UserName = "robert";
        dto.LockoutEnabled = !bob.LockoutEnabled;
        dto.LockoutEnd = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _userManager.SetUserNameAsync(Arg.Any<User>(), "robert").Returns(IdentityResult.Success);
        _userManager.SetLockoutEnabledAsync(Arg.Any<User>(), dto.LockoutEnabled).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(Arg.Any<User>(), dto.LockoutEnd).Returns(IdentityResult.Success);

        Assert.That(await _service.EditUser(dto, CancellationToken.None), Is.Null);
        await _userManager.Received(1).SetUserNameAsync(Arg.Is<User>(u => u.Id == bob.Id), "robert");
        await _userManager.Received(1).SetLockoutEnabledAsync(Arg.Is<User>(u => u.Id == bob.Id), dto.LockoutEnabled);
        await _userManager.Received(1).SetLockoutEndDateAsync(Arg.Is<User>(u => u.Id == bob.Id), dto.LockoutEnd);
    }

    [Test]
    public async Task EditUser_AddRolesFails_ReturnsMessage()
    {
        var (_, bob, _) = await SeedUsers();
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(new List<string>());
        _userManager.AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Failed());

        var result = await _service.EditUser(EditFrom(bob, null, [new RoleDto { RoleName = "Player", HasRole = true }]), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to add roles"));
    }

    [Test]
    public async Task EditUser_RemoveRolesFails_ReturnsMessage()
    {
        var (_, bob, _) = await SeedUsers();
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(new List<string> { "Player" });
        _userManager.RemoveFromRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Failed());

        var result = await _service.EditUser(EditFrom(bob, null, [new RoleDto { RoleName = "Player", HasRole = false }]), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to remove roles"));
    }

    [Test]
    public async Task EditUser_RolesDiff_AddsMissingAndRemovesRevoked()
    {
        var (_, bob, _) = await SeedUsers();
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(new List<string> { "Player", "Commissioner" });
        _userManager.AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        _userManager.RemoveFromRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);
        var roles = new List<RoleDto>
        {
            new() { RoleName = "Player", HasRole = true },        // unchanged
            new() { RoleName = "Commissioner", HasRole = false }, // remove
            new() { RoleName = "Sysadmin", HasRole = true },      // add
        };

        var result = await _service.EditUser(EditFrom(bob, null, roles), CancellationToken.None);

        Assert.That(result, Is.Null);
        await _userManager.Received(1).AddToRolesAsync(Arg.Any<User>(), Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(second)));
        await _userManager.Received(1).RemoveFromRolesAsync(Arg.Any<User>(), Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(secondArray)));
    }
}
