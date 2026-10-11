using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NSubstitute;
using Role = GrifballWebApp.Database.Models.Role;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

/// <summary>
/// Note: every path ends in ModifyTempResponse, which waits 5 seconds in app code before deleting the response.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DiscordSetGamertagBranchTests
{
    private const ulong DiscordId = 987654321;

    private GrifballContext _context;
    private ServiceProvider _provider;
    private IServiceScope _scope;
    private UserManager<User> _userManager;
    private ISetGamertagService _setGamertagService;
    private DiscordSetGamertag _sut;
    private IDiscordInteractionContext _interaction;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _context);
        services.AddIdentity<User, Role>().AddEntityFrameworkStores<GrifballContext>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        _setGamertagService = Substitute.For<ISetGamertagService>();
        _sut = new DiscordSetGamertag(_context, Substitute.For<ILogger<DiscordSetGamertag>>(), _userManager, _setGamertagService);
        _interaction = Substitute.For<IDiscordInteractionContext>();
        _interaction.Interaction.User.Id.Returns(DiscordId);
        _interaction.Interaction.User.Username.Returns("discorduser");
    }

    [TearDown]
    public async Task TearDown()
    {
        var connectionString = _context.Database.GetConnectionString()!;
        _scope.Dispose();
        await _provider.DisposeAsync();
        _ = Task.Run(() => TestDatabase.DropDatabase(connectionString));
    }

    /// <summary>Replays the ModifyResponseAsync callback on a substitute and returns the content it set.</summary>
    private IDiscordMessageOptions ModifiedResponse()
    {
        var call = _interaction.Interaction.ReceivedCalls().Single(c => c.GetMethodInfo().Name == nameof(IDiscordInteraction.ModifyResponseAsync));
        var options = Substitute.For<IDiscordMessageOptions>();
        options.WithContent(Arg.Any<string>()).Returns(options);
        ((Action<IDiscordMessageOptions>)call.GetArguments()[0]!)(options);
        return options;
    }

    [Test]
    public async Task ExistingAccountWithDiscordLogin_IsLinkedInsteadOfCreatingNewUser()
    {
        var existing = new User { UserName = "web-account" };
        await _userManager.CreateAsync(existing);
        await _userManager.AddLoginAsync(existing, new UserLoginInfo("Discord", DiscordId.ToString(), "Discord"));
        _context.DiscordUsers.Add(new Database.Models.DiscordUser { DiscordUserID = (long)DiscordId, DiscordUsername = "discorduser" });
        _setGamertagService.SetGamertag(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        await _context.SaveChangesAsync();

        await _sut.SetGamertag(_interaction, "Grunt Padre");

        var users = await _context.Users.AsNoTracking().ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That(users, Has.Count.EqualTo(1), "No new account is created");
            Assert.That(users.Single().DiscordUserID, Is.EqualTo((long)DiscordId));
        });
        await _setGamertagService.Received(1).SetGamertag(existing.Id, "Grunt Padre", Arg.Any<CancellationToken>());
        ModifiedResponse().Received(1).WithContent("Gamertag has been set");
    }

    [Test]
    public async Task UserCreationFailure_RepliesWithErrorAndDoesNotSetGamertag()
    {
        // A different account already owns the user name, so CreateAsync fails with DuplicateUserName.
        await _userManager.CreateAsync(new User { UserName = "discorduser" });

        await _sut.SetGamertag(_interaction, "Grunt Padre");

        ModifiedResponse().Received(1).WithContent("Failed to create user account");
        await _setGamertagService.DidNotReceiveWithAnyArgs().SetGamertag(default, default!, default);
        await _interaction.Interaction.Received(1).DeleteResponseAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SetGamertagServiceError_IsRelayedToUser()
    {
        _setGamertagService.SetGamertag(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("Did not find that gamertag");

        await _sut.SetGamertag(_interaction, "Nobody");

        ModifiedResponse().Received(1).WithContent("Did not find that gamertag");
    }
}
