using System.Collections.Concurrent;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordInteractionPipelineTests : DiscordInteractionPipelineFixture
{
    private GrifballContext? _context;

    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    private static Season NewSeason() => Season.Seed(seasonName: "Discord Audit Season").WithDates(publicAt: DateTime.UtcNow, signupsOpen: DateTime.UtcNow, signupsClose: DateTime.UtcNow.AddDays(7), draftStart: DateTime.UtcNow.AddDays(8), seasonStart: DateTime.UtcNow.AddDays(14), seasonEnd: DateTime.UtcNow.AddDays(60));

    // Mirrors the per-interaction scope: the context the module writes through shares the scope's ICurrentUserService
    private async Task<IServiceProvider> ScopeWithDatabase()
    {
        var ambient = new AmbientCurrentUser();
        var currentUserService = new CurrentUserService(new HttpContextAccessor(), ambient);
        _context = await SetUpFixture.NewGrifballContext(new Domain.Shared.AuditInterceptor(currentUserService));
        return new ServiceCollection()
            .AddSingleton<GrifballContext>(_context)
            .AddSingleton<ICurrentUserService>(currentUserService)
            .AddSingleton<IAmbientCurrentUser>(ambient)
            .BuildServiceProvider();
    }

    [Test]
    public async Task PreExecution_LinkedDiscordUser_SetsAuditUserOnWrites()
    {
        var services = await ScopeWithDatabase();
        var user = Domain.Players.User.Seed(userName: "linked", discordUser: DiscordUser.FromDiscord((long)DiscordId, "linked"));
        _context!.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleAsync(Button(), null, _logger, services);

        var season = NewSeason();
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.SameAs(PreExecutionResult.Continue));
            Assert.That(season.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(season.ModifiedByID, Is.EqualTo(user.Id));
            // Set after the lookup's await, yet seen here, by the awaiting caller (NetCord, in production)
            Assert.That(services.GetRequiredService<IAmbientCurrentUser>().UserId, Is.EqualTo(user.Id));
            Assert.That(_logger.Entries, Is.Empty);
        }
    }

    [Test]
    public async Task PreExecution_UnlinkedDiscordUser_LeavesAuditUserNull()
    {
        var services = await ScopeWithDatabase();

        var result = await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleAsync(SlashCommand(), null, _logger, services);

        var season = NewSeason();
        _context!.Seasons.Add(season);
        await _context.SaveChangesAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.SameAs(PreExecutionResult.Continue));
            Assert.That(season.CreatedByID, Is.Null);
            Assert.That(season.ModifiedByID, Is.Null);
            Assert.That(services.GetRequiredService<ICurrentUserService>().GetCurrentUserId(), Is.Null);
        }
    }

    // The real NetCord hosting pipeline (its internal ComponentInteractionHandler), registered like Program.cs, running
    // FactoryAuditModule, which writes through IDbContextFactory: the ambient user has to flow from the pre-execution
    // handler into the module, and not out of the interaction.
    private async Task<(IInteractionCreateGatewayHandler Handler, FactoryAuditModule.Probe Probe)> RealPipeline()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:GrifballWebApp"] = _context.Database.GetConnectionString(),
            ["Database:Provider"] = TestDatabase.Provider.ToString(),
        }).Build();
        var collection = new ServiceCollection()
            .AddSingleton<IConfiguration>(config)
            .AddLogging()
            .AddHttpContextAccessor()
            .AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>()
            .AddScoped<ICurrentUserService, CurrentUserService>()
            .AddScoped<AuditInterceptor>()
            .AddDbContext<GrifballContext>((sp, options) => options
                .UseGrifballDatabase(sp.GetRequiredService<IConfiguration>())
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()))
            .AddDbContextFactory<GrifballContext, GrifballContextFactory>()
            .AddSingleton<FactoryAuditModule.Probe>()
            .AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>(options => DiscordInteractionPipeline.Use(options));
        var handlerType = collection.Single(d => typeof(IInteractionCreateGatewayHandler).IsAssignableFrom(d.ServiceType)).ServiceType;
        var services = collection.BuildServiceProvider();
        services.GetRequiredService<ComponentInteractionService<ButtonInteractionContext>>().AddModule<FactoryAuditModule>();
        var handler = (IInteractionCreateGatewayHandler)services.GetRequiredService(handlerType);
        return (handler, services.GetRequiredService<FactoryAuditModule.Probe>());
    }

    private async Task<Domain.Players.User> LinkedUser(ulong discordId, string name)
    {
        var user = Domain.Players.User.Seed(userName: name, discordUser: DiscordUser.FromDiscord((long)discordId, name));
        _context!.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Season> SavedSeason(string name)
    {
        using var read = _context!.NewContextLike();
        return await read.Seasons.AsNoTracking().SingleAsync(x => x.SeasonName == name);
    }

    [Test]
    public async Task RealPipeline_FactoryContextInModule_RecordsTheInteractionsUser()
    {
        var (handler, probe) = await RealPipeline();
        var user = await LinkedUser(DiscordId, "linked");

        await handler.HandleAsync(Button("factory-audit:one").Interaction);

        var season = await SavedSeason("one");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_callbacks, Is.Empty, "the module failed");
            Assert.That(season.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(season.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(season.ModifiedByID, Is.EqualTo(user.Id));
            Assert.That(probe.SeenInModule["one"], Is.EqualTo(user.Id));
        }
    }

    [Test]
    public async Task RealPipeline_UserDoesNotLeakIntoTheNextInteraction()
    {
        var (handler, _) = await RealPipeline();
        var user = await LinkedUser(DiscordId, "linked");

        await handler.HandleAsync(Button("factory-audit:first").Interaction);
        await handler.HandleAsync(Button("factory-audit:second", userId: 9999).Interaction); // not linked

        var first = await SavedSeason("first");
        var second = await SavedSeason("second");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(second.CreatedByID, Is.Null);
            Assert.That(second.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(new AmbientCurrentUser().UserId, Is.Null, "the test's own flow must not see an interaction's user");
        }
    }

    [Test]
    public async Task RealPipeline_ConcurrentInteractions_EachRecordItsOwnUser()
    {
        var (handler, probe) = await RealPipeline();
        var alice = await LinkedUser(DiscordId, "alice");
        var bob = await LinkedUser(5151, "bob");
        probe.Expected = 2; // both modules wait for each other, so the two interactions overlap

        await Task.WhenAll(
            handler.HandleAsync(Button("factory-audit:alice").Interaction).AsTask(),
            handler.HandleAsync(Button("factory-audit:bob", userId: 5151).Interaction).AsTask());

        var aliceSeason = await SavedSeason("alice");
        var bobSeason = await SavedSeason("bob");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(aliceSeason.CreatedByID, Is.EqualTo(alice.Id));
            Assert.That(bobSeason.CreatedByID, Is.EqualTo(bob.Id));
        }
    }

    [Test]
    public async Task RealPipeline_WorkLeftRunningByTheModule_LosesTheUserWhenTheInteractionEnds()
    {
        var (handler, probe) = await RealPipeline();
        var user = await LinkedUser(DiscordId, "linked");

        await handler.HandleAsync(Button("factory-audit:background").Interaction);
        probe.InteractionEnded.SetResult();
        var seenAfterwards = await probe.Background!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(probe.SeenInModule["background"], Is.EqualTo(user.Id));
            Assert.That(seenAfterwards, Is.Null);
        }
    }
}

/// <summary>A component module that writes through IDbContextFactory, for <see cref="DiscordInteractionPipelineTests"/>.</summary>
public class FactoryAuditModule(IDbContextFactory<GrifballContext> contextFactory, IAmbientCurrentUser ambientCurrentUser, FactoryAuditModule.Probe probe)
    : ComponentInteractionModule<ButtonInteractionContext>
{
    public class Probe
    {
        private int _arrived;
        private readonly TaskCompletionSource _allArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Expected { get; set; } = 1;
        public ConcurrentDictionary<string, int?> SeenInModule { get; } = new();
        public TaskCompletionSource InteractionEnded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<int?>? Background { get; set; }

        public Task ArriveAsync()
        {
            if (Interlocked.Increment(ref _arrived) >= Expected)
                _allArrived.TrySetResult();
            return _allArrived.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [ComponentInteraction("factory-audit")]
    public async Task SaveAsync(string name)
    {
        await probe.ArriveAsync();
        probe.SeenInModule[name] = ambientCurrentUser.UserId;
        if (name == "background")
        {
            // Started inside the interaction, still running after it
            probe.Background = Task.Run(async () =>
            {
                await probe.InteractionEnded.Task;
                return ambientCurrentUser.UserId;
            });
        }

        await using var context = await contextFactory.CreateDbContextAsync();
        context.Seasons.Add(Season.Seed(seasonName: name).WithDates(publicAt: DateTime.UtcNow, signupsOpen: DateTime.UtcNow, signupsClose: DateTime.UtcNow.AddDays(7), draftStart: DateTime.UtcNow.AddDays(8), seasonStart: DateTime.UtcNow.AddDays(14), seasonEnd: DateTime.UtcNow.AddDays(60)));
        await context.SaveChangesAsync();
    }
}
