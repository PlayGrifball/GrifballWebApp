using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Players;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class SetGamertagServiceUnitTests : ApplicationDbContextTests
{
    private IGetsertXboxUserService _xboxService = null!;
    private IUserMergeService _mergeService = null!;
    private SetGamertagService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _xboxService = Substitute.For<IGetsertXboxUserService>();
        _mergeService = Substitute.For<IUserMergeService>();
        _service = new SetGamertagService(Context, _xboxService, _mergeService, new GamertagMatching(Context));
    }

    [Test]
    public async Task SetGamertag_NoSuchUser_ReturnsMessage()
    {
        var result = await _service.SetGamertag(999, "Chief117", CancellationToken.None);

        Assert.That(result, Is.EqualTo("User does not exist"));
    }

    [Test]
    public async Task SetGamertag_AlreadyHasOne_ReturnsMessage()
    {
        var xbox = XboxUser.FromXbox(1, "Chief117");
        Context.XboxUsers.Add(xbox);
        var user = User.Seed(userName: "alice", xboxUser: xbox, id: 1);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var result = await _service.SetGamertag(1, "Someone Else", CancellationToken.None);

        Assert.That(result, Does.Contain("already set"));
    }

    [Test]
    public async Task SetGamertag_LookupFails_ReturnsMessage()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)null, "no such gamertag"));

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        Assert.That(result, Does.Contain("no such gamertag"));
    }

    [Test]
    public async Task SetGamertag_FreeGamertag_TiesItAndSucceeds()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();
        var xbox = XboxUser.FromXbox(111, "GamerOne");
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        Assert.That(result, Is.Null);
        Context.ChangeTracker.Clear();
        var reloaded = await Context.Users.FindAsync(1);
        Assert.That(reloaded!.XboxUserID, Is.EqualTo(111L));
    }

    [Test]
    public async Task SetGamertag_HeldByNonDummyUser_ReturnsMessage()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        var xbox = XboxUser.FromXbox(111, "GamerOne");
        Context.XboxUsers.Add(xbox);
        var holder = User.Seed(userName: "bob", xboxUser: xbox, isDummyUser: false, id: 2);
        Context.Users.Add(holder);
        await Context.SaveChangesAsync();
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        Assert.That(result, Does.Contain("already attached to a user"));
    }

    [Test]
    public async Task SetGamertag_HeldByPlaceholder_MergesPlaceholderAndSucceeds()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        var xbox = XboxUser.FromXbox(111, "GamerOne");
        Context.XboxUsers.Add(xbox);
        var placeholder = User.Seed(userName: "historical:GamerOne", displayName: "GamerOne", xboxUser: xbox, isDummyUser: true, id: 2);
        Context.Users.Add(placeholder);
        await Context.SaveChangesAsync();
        _xboxService.GetsertXboxUserByGamertag("GamerOne", Arg.Any<CancellationToken>()).Returns(((XboxUser?)xbox, (string?)null));

        var result = await _service.SetGamertag(1, "GamerOne", CancellationToken.None);

        Assert.That(result, Is.Null);
        await _mergeService.Received().Merge(1, 2, Arg.Is<MergeOptions>(o => o.Trigger == MergeTrigger.SetGamertag), Arg.Any<CancellationToken>());
    }
}
