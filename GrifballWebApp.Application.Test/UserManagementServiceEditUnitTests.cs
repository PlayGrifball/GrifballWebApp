using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.UserManagement;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// UserManagementService.EditUser's gamertag, sign-in and role steps (each refusal, and a gamertag tied, untied or taken
/// from its holder), beyond UserManagementServiceUnitTests. Not here: GetUsers and GetUser (their Roles projection needs
/// SQL APPLY, see UserManagementServiceUnitTests).
/// </summary>
[TestFixture]
internal sealed class UserManagementServiceEditUnitTests : ApplicationDbContextTests
{
    private const int UserID = 1;
    private const int HolderID = 2;
    private static readonly DateTimeOffset LockedUntil = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Commissioner = ["Commissioner"];

    private IGetsertXboxUserService _xboxService = null!;
    private UserManager<User> _userManager = null!;
    private UserManagementService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _xboxService = Substitute.For<IGetsertXboxUserService>();
        _userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(), null!, null!, null!, null!, null!, new IdentityErrorDescriber(), null!, null!);
        IList<string> noRoles = [];
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(noRoles);
        _service = new UserManagementService(Context, _userManager, _xboxService);
    }

    private static IdentityResult Failed() => IdentityResult.Failed(new IdentityError { Code = "x", Description = "refused" });

    private async Task SeedUser(XboxUser? xbox = null)
    {
        Context.Users.Add(User.Seed(userName: "alice", displayName: "Alice", xboxUser: xbox, id: UserID));
        await Context.SaveChangesAsync();
    }

    /// <summary>The user as they are (alice, no lockout, no roles), with <paramref name="gamertag"/>: only what a test changes differs.</summary>
    private static UserResponseDto Edit(string? gamertag = null, string userName = "alice", string displayName = "Alice", DateTimeOffset? lockoutEnd = null, bool lockoutEnabled = false,
        List<RoleDto>? roles = null) => new()
    {
        UserID = UserID,
        UserName = userName,
        DisplayName = displayName,
        Region = null,
        Gamertag = gamertag,
        Discord = null,
        ExternalAuthCount = 0,
        HasPassword = false,
        LockoutEnd = lockoutEnd,
        LockoutEnabled = lockoutEnabled,
        Roles = roles ?? [],
    };

    private Task<User> ReadUser(int id)
    {
        Context.ChangeTracker.Clear();
        return Context.Users.AsNoTracking().SingleAsync(u => u.Id == id);
    }

    [Test]
    public async Task EditUser_GamertagCleared_UntiesTheXboxAccount()
    {
        await SeedUser(XboxUser.FromXbox(100, "Chief"));

        var result = await _service.EditUser(Edit(gamertag: null), CancellationToken.None);

        var user = await ReadUser(UserID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(user.XboxUserID, Is.Null);
        }
        await _xboxService.DidNotReceive().GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EditUser_GamertagSameButCase_LooksNothingUp()
    {
        await SeedUser(XboxUser.FromXbox(100, "Chief"));

        var result = await _service.EditUser(Edit(gamertag: "CHIEF"), CancellationToken.None);

        var user = await ReadUser(UserID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(user.XboxUserID, Is.EqualTo(100));
        }
        await _xboxService.DidNotReceive().GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestCase("No such gamertag", "No such gamertag")]
    [TestCase(null, "Failed to get xbox user, unknown reason")]
    public async Task EditUser_GamertagLookupFails_ReturnsWhyAndSavesNothing(string? message, string expected)
    {
        await SeedUser();
        _xboxService.GetsertXboxUserByGamertag("Ghost", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, message));

        var result = await _service.EditUser(Edit(gamertag: "Ghost"), CancellationToken.None);

        var user = await ReadUser(UserID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(user.XboxUserID, Is.Null);
        }
    }

    [Test]
    public async Task EditUser_NewGamertag_NoneBefore_TiesIt()
    {
        await SeedUser();
        _xboxService.GetsertXboxUserByGamertag("Fresh", Arg.Any<CancellationToken>()).Returns((XboxUser.FromXbox(400, "Fresh"), (string?)null));

        var result = await _service.EditUser(Edit(gamertag: "Fresh"), CancellationToken.None);

        var user = await ReadUser(UserID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(user.XboxUserID, Is.EqualTo(400));
        }
    }

    [Test]
    public async Task EditUser_NewGamertag_TiedToAnother_ReplacesIt()
    {
        await SeedUser(XboxUser.FromXbox(200, "Old"));
        _xboxService.GetsertXboxUserByGamertag("New", Arg.Any<CancellationToken>()).Returns((XboxUser.FromXbox(300, "New"), (string?)null));

        var result = await _service.EditUser(Edit(gamertag: "New"), CancellationToken.None);

        var user = await ReadUser(UserID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(user.XboxUserID, Is.EqualTo(300));
            Assert.That(await Context.XboxUsers.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task EditUser_GamertagHeldByAnotherUser_TakesItFromThem()
    {
        var taken = XboxUser.FromXbox(500, "Taken");
        Context.Users.Add(User.Seed(userName: "holder", xboxUser: taken, id: HolderID));
        await SeedUser();
        // The lookup finds the account the holder's row already tracks (its User is the holder)
        _xboxService.GetsertXboxUserByGamertag("Taken", Arg.Any<CancellationToken>()).Returns((taken, (string?)null));

        var result = await _service.EditUser(Edit(gamertag: "Taken"), CancellationToken.None);

        var user = await ReadUser(UserID);
        var holder = await ReadUser(HolderID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.Null);
            Assert.That(user.XboxUserID, Is.EqualTo(500));
            Assert.That(holder.XboxUserID, Is.Null);
        }
    }

    [Test]
    public async Task EditUser_LockoutEndRefused_ReturnsMessage()
    {
        await SeedUser();
        _userManager.SetLockoutEndDateAsync(Arg.Any<User>(), LockedUntil).Returns(Failed());

        var result = await _service.EditUser(Edit(lockoutEnd: LockedUntil), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to set lockout end date"));
        await _userManager.DidNotReceive().SetLockoutEnabledAsync(Arg.Any<User>(), Arg.Any<bool>());
    }

    [Test]
    public async Task EditUser_LockoutEnabledRefused_ReturnsMessage()
    {
        await SeedUser();
        _userManager.SetLockoutEnabledAsync(Arg.Any<User>(), true).Returns(Failed());

        var result = await _service.EditUser(Edit(lockoutEnabled: true), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to set lockout enabled"));
        await _userManager.DidNotReceive().SetLockoutEndDateAsync(Arg.Any<User>(), Arg.Any<DateTimeOffset?>());
    }

    [Test]
    public async Task EditUser_UserNameRefused_ReturnsMessageAndSavesNothing()
    {
        await SeedUser();
        _userManager.SetUserNameAsync(Arg.Any<User>(), "taken").Returns(Failed());

        var result = await _service.EditUser(Edit(userName: "taken", displayName: "Renamed"), CancellationToken.None);

        var user = await ReadUser(UserID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo("Failed to set username"));
            Assert.That(user.DisplayName, Is.EqualTo("Alice"));
        }
        await _userManager.DidNotReceive().GetRolesAsync(Arg.Any<User>());
    }

    [Test]
    public async Task EditUser_LockoutAndUserNameAccepted_SetsEachThroughIdentity()
    {
        await SeedUser();
        _userManager.SetLockoutEndDateAsync(Arg.Any<User>(), LockedUntil).Returns(IdentityResult.Success);
        _userManager.SetLockoutEnabledAsync(Arg.Any<User>(), true).Returns(IdentityResult.Success);
        _userManager.SetUserNameAsync(Arg.Any<User>(), "alice2").Returns(IdentityResult.Success);

        var result = await _service.EditUser(Edit(userName: "alice2", lockoutEnd: LockedUntil, lockoutEnabled: true), CancellationToken.None);

        Assert.That(result, Is.Null);
        await _userManager.Received(1).SetLockoutEndDateAsync(Arg.Is<User>(u => u.Id == UserID), LockedUntil);
        await _userManager.Received(1).SetLockoutEnabledAsync(Arg.Is<User>(u => u.Id == UserID), true);
        await _userManager.Received(1).SetUserNameAsync(Arg.Is<User>(u => u.Id == UserID), "alice2");
    }

    [Test]
    public async Task EditUser_AddingRolesRefused_ReturnsMessage()
    {
        await SeedUser();
        _userManager.AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(Failed());

        var result = await _service.EditUser(Edit(roles: [new RoleDto { RoleName = "Commissioner", HasRole = true }]), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to add roles"));
        await _userManager.Received(1).AddToRolesAsync(Arg.Any<User>(), Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(Commissioner)));
    }

    [Test]
    public async Task EditUser_RemovingRolesRefused_ReturnsMessage()
    {
        await SeedUser();
        IList<string> held = ["Commissioner"];
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(held);
        _userManager.RemoveFromRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(Failed());

        var result = await _service.EditUser(Edit(roles: [new RoleDto { RoleName = "Commissioner", HasRole = false }]), CancellationToken.None);

        Assert.That(result, Is.EqualTo("Failed to remove roles"));
        await _userManager.DidNotReceive().AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>());
    }

    [Test]
    public async Task EditUser_RoleUnticked_RemovesItAndKeepsTheOthers()
    {
        await SeedUser();
        IList<string> held = ["Commissioner", "Player"];
        _userManager.GetRolesAsync(Arg.Any<User>()).Returns(held);
        _userManager.RemoveFromRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>()).Returns(IdentityResult.Success);

        var result = await _service.EditUser(Edit(roles:
            [new RoleDto { RoleName = "Commissioner", HasRole = false }, new RoleDto { RoleName = "Player", HasRole = true }]), CancellationToken.None);

        Assert.That(result, Is.Null);
        await _userManager.Received(1).RemoveFromRolesAsync(Arg.Any<User>(), Arg.Is<IEnumerable<string>>(r => r.SequenceEqual(Commissioner)));
        await _userManager.DidNotReceive().AddToRolesAsync(Arg.Any<User>(), Arg.Any<IEnumerable<string>>());
    }
}
