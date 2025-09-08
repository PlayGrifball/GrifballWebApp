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
using System.Security.Claims;
using Role = GrifballWebApp.Database.Models.Role;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class DiscordSetGamertagTests
{
    private GrifballContext _context;
    private IServiceScope _scope;
    private UserManager<User> _userManager;
    private ISetGamertagService _profileService;
    private DiscordSetGamertag _discordSetGamertag;
    private IDiscordInteractionContext _interactionContext;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();

        // All this to get a UserManager<User> instance, not mocking it cause I sort of want to be sure its working
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _context);
        services.AddIdentity<User, Role>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.SignIn.RequireConfirmedPhoneNumber = false;
            options.SignIn.RequireConfirmedEmail = false;
        })
            .AddDefaultTokenProviders()
            .AddSignInManager<SignInManager<User>>()
            .AddEntityFrameworkStores<GrifballContext>();
        var serviceProvider = services.BuildServiceProvider();
        _scope = serviceProvider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        _profileService = Substitute.For<ISetGamertagService>();

        _discordSetGamertag = new DiscordSetGamertag(
            _context,
            Substitute.For<ILogger<DiscordSetGamertag>>(),
            _userManager,
            _profileService
        );

        _interactionContext = Substitute.For<IDiscordInteractionContext>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabase();
        _scope.Dispose();
    }

    // Test in different scenerios that may occur.

    [Test]
    public async Task EmptyDatabase()
    {
        // Arrange
        _interactionContext.Interaction.User.Username.Returns("discorduser");
        _interactionContext.Interaction.User.Id.Returns((ulong)1234567890);

        // Act
        await _discordSetGamertag.SetGamertag(_interactionContext, "Grunt Padre");
        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Include(x => x.DiscordUser)
            .Where(x => x.UserName == "discorduser")
            .FirstOrDefaultAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(user, Is.Not.Null, "User should be created");
            // Gamertag will not be set unless we 1 mock it, or 2 have a real Profile service
            //Assert.That(user?.XboxUser, Is.Not.Null, "XboxUser should be created");
            //Assert.That(user?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"), "Gamertag should be set correctly");
            Assert.That(user?.DiscordUser, Is.Not.Null, "DiscordUser should be created");
            Assert.That(user?.DiscordUser?.DiscordUserID, Is.EqualTo((long)1234567890), "DiscordUserID should match the interaction user ID");
            Assert.That(user?.DiscordUser?.DiscordUsername, Is.EqualTo("discorduser"), "DiscordUsername should match the interaction username");
            Assert.DoesNotThrowAsync(async () =>
            {
                await _interactionContext.Interaction.Received(1)
                    .ModifyResponseAsync(Arg.Any<Action<IDiscordMessageOptions>>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
            }, "Message should be modified");
            Assert.DoesNotThrowAsync(async () =>
            {
                await _interactionContext.Interaction.Received(1)
                .DeleteResponseAsync();
            }, "Message should be deleted");
        });
        
    }

    [Test]
    public async Task DiscordUserExists()
    {
        // Arrange
        _interactionContext.Interaction.User.Username.Returns("discorduser");
        _interactionContext.Interaction.User.Id.Returns((ulong)1234567890);
        _context.DiscordUsers.Add(new()
        {
            DiscordUserID = 1234567890,
            DiscordUsername = "discorduser",
        });
        await _context.SaveChangesAsync();

        // Act
        await _discordSetGamertag.SetGamertag(_interactionContext, "Grunt Padre");
        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Include(x => x.DiscordUser)
            .Where(x => x.UserName == "discorduser")
            .FirstOrDefaultAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(user, Is.Not.Null, "User should be created");
            // Gamertag will not be set unless we 1 mock it, or 2 have a real Profile service
            //Assert.That(user?.XboxUser, Is.Not.Null, "XboxUser should be created");
            //Assert.That(user?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"), "Gamertag should be set correctly");
            Assert.That(user?.DiscordUser, Is.Not.Null, "DiscordUser should be created");
            Assert.That(user?.DiscordUser?.DiscordUserID, Is.EqualTo((long)1234567890), "DiscordUserID should match the interaction user ID");
            Assert.That(user?.DiscordUser?.DiscordUsername, Is.EqualTo("discorduser"), "DiscordUsername should match the interaction username");
            Assert.DoesNotThrowAsync(async () =>
            {
                await _interactionContext.Interaction.Received(1)
                    .ModifyResponseAsync(Arg.Any<Action<IDiscordMessageOptions>>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
            }, "Message should be modified");
            Assert.DoesNotThrowAsync(async () =>
            {
                await _interactionContext.Interaction.Received(1)
                .DeleteResponseAsync();
            }, "Message should be deleted");
        });

    }

    [Test]
    public async Task DiscordUserExistsAndUserExists()
    {
        // Arrange
        _interactionContext.Interaction.User.Username.Returns("discorduser");
        _interactionContext.Interaction.User.Id.Returns((ulong)1234567890);
        var discordUser = new Database.Models.DiscordUser()
        {
            DiscordUserID = 1234567890,
            DiscordUsername = "discorduser",
        };
        _context.DiscordUsers.Add(discordUser);
        await _context.SaveChangesAsync();
        discordUser.User = new()
        {
            UserName = discordUser.DiscordUsername, // Need to avoid conflicts
        };
        await _userManager.CreateAsync(discordUser.User);
        // Setup extenal auth similar to result = await _userManager.AddLoginAsync(user, info); from IdentityController.cs
        await _userManager.AddLoginAsync(discordUser.User, new UserLoginInfo("Discord", discordUser.DiscordUserID.ToString(), "Discord"));
        // Add claims
        await _userManager.AddClaimsAsync(discordUser.User, new[]
        {
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", discordUser.DiscordUserID.ToString()),
            new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", discordUser.DiscordUsername),
        });

        // Act
        await _discordSetGamertag.SetGamertag(_interactionContext, "Grunt Padre");
        var user = await _context.Users
            .Include(x => x.XboxUser)
            .Include(x => x.DiscordUser)
            .Where(x => x.UserName == "discorduser")
            .FirstOrDefaultAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(user, Is.Not.Null, "User should be created");
            // Gamertag will not be set unless we 1 mock it, or 2 have a real Profile service
            //Assert.That(user?.XboxUser, Is.Not.Null, "XboxUser should be created");
            //Assert.That(user?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"), "Gamertag should be set correctly");
            Assert.That(user?.DiscordUser, Is.Not.Null, "DiscordUser should be created");
            Assert.That(user?.DiscordUser?.DiscordUserID, Is.EqualTo((long)1234567890), "DiscordUserID should match the interaction user ID");
            Assert.That(user?.DiscordUser?.DiscordUsername, Is.EqualTo("discorduser"), "DiscordUsername should match the interaction username");
            Assert.DoesNotThrowAsync(async () =>
            {
                await _interactionContext.Interaction.Received(1)
                    .ModifyResponseAsync(Arg.Any<Action<IDiscordMessageOptions>>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
            }, "Message should be modified");
            Assert.DoesNotThrowAsync(async () =>
            {
                await _interactionContext.Interaction.Received(1)
                .DeleteResponseAsync();
            }, "Message should be deleted");
        });

    }
}
