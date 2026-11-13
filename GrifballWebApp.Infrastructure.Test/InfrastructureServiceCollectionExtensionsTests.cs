using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// InfrastructureServiceCollectionExtensions: the registrations AddGrifballDatabase, AddDiscordInfrastructure,
/// AddValidatedDiscordOptions and AddInfrastructure make, and (where it's cheap, real behavior rather than lines
/// executed) what they let the container resolve. No database server, host or network: AddInfrastructure's own HTTP
/// clients and Quartz job store are configured, never opened or started here.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class InfrastructureServiceCollectionExtensionsTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static readonly Dictionary<string, string?> ValidDiscordSettings = new()
    {
        ["Discord:ClientId"] = "client-id",
        ["Discord:ClientSecret"] = "client-secret",
        ["Discord:Token"] = "token",
        ["Discord:DraftChannel"] = "123",
    };

    [Test]
    public void AddValidatedDiscordOptions_AllSettingsPresent_ResolvesWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddValidatedDiscordOptions(Config(ValidDiscordSettings));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<DiscordOptions>>().Value;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.ClientId, Is.EqualTo("client-id"));
            Assert.That(options.Token, Is.EqualTo("token"));
            Assert.That(options.DraftChannel, Is.EqualTo(123UL));
        }
    }

    [Test]
    public void AddValidatedDiscordOptions_EverythingMissing_ListsEveryMissingSetting()
    {
        var services = new ServiceCollection();
        services.AddValidatedDiscordOptions(Config([]));
        using var provider = services.BuildServiceProvider();

        // The validator throws ArgumentException itself (with every missing setting joined into its message), rather
        // than returning false (which the options framework would wrap in OptionsValidationException).
        var ex = Assert.Throws<ArgumentException>(() => _ = provider.GetRequiredService<IOptions<DiscordOptions>>().Value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("ClientId is required"));
            Assert.That(ex.Message, Does.Contain("ClientSecret is required"));
            Assert.That(ex.Message, Does.Contain("Token is required"));
            Assert.That(ex.Message, Does.Contain("DraftChannel is required"));
        }
    }

    [TestCase("Discord:ClientId", "ClientId is required")]
    [TestCase("Discord:ClientSecret", "ClientSecret is required")]
    [TestCase("Discord:Token", "Token is required")]
    public void AddValidatedDiscordOptions_OneSettingMissing_NamesOnlyThatOne(string missingKey, string expectedError)
    {
        var settings = new Dictionary<string, string?>(ValidDiscordSettings) { [missingKey] = null };
        var services = new ServiceCollection();
        services.AddValidatedDiscordOptions(Config(settings));
        using var provider = services.BuildServiceProvider();

        var ex = Assert.Throws<ArgumentException>(() => _ = provider.GetRequiredService<IOptions<DiscordOptions>>().Value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain(expectedError));
            Assert.That(ex.Message, Does.Not.Contain("DraftChannel is required"), "DraftChannel is still set in this test");
        }
    }

    [Test]
    public void AddGrifballDatabase_ResolvesARealContext_SharedAsIApplicationDbContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"grif-infra-di-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(Config(new()
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:GrifballWebApp"] = $"Data Source={path};Pooling=False",
            }));
            // AddGrifballDatabase's context needs an OutboxSignalInterceptor too; production adds it with AddDiscordOutbox.
            services.AddSingleton<GrifballWebApp.Infrastructure.Outbox.OutboxSignal>();
            services.AddScoped<GrifballWebApp.Infrastructure.Outbox.OutboxSignalInterceptor>();
            services.AddGrifballDatabase();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<GrifballContext>();
            var applicationContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<GrifballContext>>();
            using var fromFactory = factory.CreateDbContext();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(applicationContext, Is.SameAs(context), "The use cases share the scope's own context");
                Assert.That(context.Database.IsSqlite(), Is.True);
                Assert.That(fromFactory, Is.Not.SameAs(context), "The factory's own context, not the scoped one");
            }
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AddDiscordInfrastructure_RegistersTheQueueAndEventsServices(bool discordOff)
    {
        var services = new ServiceCollection();

        services.AddDiscordInfrastructure(Config(discordOff ? [] : ValidDiscordSettings), discordOff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(QueueService)), Is.True);
            // AddOptions<DiscordOptions>().Bind(...) registers IConfigureOptions<DiscordOptions>, not IOptions<DiscordOptions>
            // itself directly (that comes from the open generic IOptions<> factory, resolved later).
            Assert.That(services.Any(d => d.ServiceType == typeof(IConfigureOptions<DiscordOptions>)), Is.True);
        }
    }

    private static Dictionary<string, string?> ValidInfrastructureSettings(bool preview) => new()
    {
        ["Database:Provider"] = "Sqlite",
        ["ConnectionStrings:GrifballWebApp"] = "Data Source=grif-infra-registration-only;Mode=Memory",
        ["Preview:Enabled"] = preview ? "true" : "false",
    };

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void AddInfrastructure_RegistersItsServices_WithoutOpeningAnyConnection(bool discordOff, bool preview)
    {
        var services = new ServiceCollection();

        services.AddInfrastructure(Config(ValidInfrastructureSettings(preview)), discordOff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(IDataPullService)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(IDraftChannel)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(IDatabaseLocks)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(ExcelService)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(HistoricalImportRunner)), Is.True);
            Assert.That(services.Any(d => d.ImplementationType == (preview ? typeof(PreviewSmokeXboxUsers) : typeof(GetsertXboxUserService)) && d.ServiceType == typeof(IGetsertXboxUserService)), Is.True,
                preview ? "A preview's smoke players get made-up gamertags" : "Everything else is looked up on Halo");
        }
    }
}
