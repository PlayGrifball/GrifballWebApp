using DiscordInterfaces;
using EntityFrameworkCore.Testing.NSubstitute;
using GrifballWebApp.Database;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
    private IProfileService _profileService;
    private DiscordSetGamertag _discordSetGamertag;
    private IDiscordInteractionContext _interactionContext;

    [SetUp]
    public void Setup()
    {
        // Configure in-memory database with unique name per test
        var options = new DbContextOptionsBuilder<GrifballContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique database per test
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _context = Create.MockedDbContextFor<GrifballContext>(options);

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

        _profileService = Substitute.For<IProfileService>();

        _discordSetGamertag = new DiscordSetGamertag(
            _context,
            Substitute.For<ILogger<DiscordSetGamertag>>(),
            _userManager,
            _profileService
        );

        _interactionContext = Substitute.For<IDiscordInteractionContext>();
    }

    [Test]
    public async Task Working()
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
        });
        
    }

    // TODO: need more testing here
}
