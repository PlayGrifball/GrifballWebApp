using System.Net;
using System.Collections.Concurrent;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Interceptors;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordInteractionPipelineTests
{
    private const ulong DiscordId = 4242;

    private readonly List<InteractionCallbackProperties> _callbacks = [];
    private readonly FakeRestRequestHandler _rest = new();
    private readonly RecordingLogger<DiscordInteractionPipelineTests> _logger = new();
    private RestException? _responseError;
    private GrifballContext? _context;
    private readonly IServiceProvider _ambientOnly = new ServiceCollection().AddSingleton<IAmbientCurrentUser, AmbientCurrentUser>().BuildServiceProvider();

    [TearDown]
    public async Task TearDown()
    {
        if (_context is not null)
            await _context.DropDatabaseAndDispose();
    }

    private static void FillNullArrays(params object[] models)
    {
        foreach (var o in models)
            foreach (var p in o.GetType().GetProperties())
                if (p.PropertyType.IsArray && p.CanWrite && p.GetValue(o) is null)
                    p.SetValue(o, Array.CreateInstance(p.PropertyType.GetElementType()!, 0));
    }

    private Task<InteractionCallbackResponse?> SendResponse(IInteraction _, InteractionCallbackProperties callback, bool __, RestRequestProperties? ___, CancellationToken ____)
    {
        if (_responseError is not null)
            throw _responseError;
        _callbacks.Add(callback);
        return Task.FromResult<InteractionCallbackResponse?>(null);
    }

    private ButtonInteractionContext Button(string customId = "signup:1", ulong userId = DiscordId)
    {
        var json = new JsonInteraction
        {
            Id = 1,
            ApplicationId = 2,
            Type = InteractionType.MessageComponent,
            Token = "tok",
            User = new JsonUser { Id = userId, Username = "u" },
            Data = new JsonInteractionData { CustomId = customId, ComponentType = ComponentType.Button },
            Message = new JsonMessage { Id = 5, ChannelId = 6, Author = new JsonUser { Id = 7, Username = "bot" } },
            Channel = new JsonChannel { Id = 6, Type = ChannelType.TextGuildChannel },
        };
        FillNullArrays(json, json.Message);
        var interaction = new ButtonInteraction(json, null, SendResponse, new RestClient(new RestClientConfiguration { RequestHandler = _rest }));
        return new ButtonInteractionContext(interaction, null!);
    }

    private ApplicationCommandContext SlashCommand(string name = "setgamertag")
    {
        var json = new JsonInteraction
        {
            Id = 1,
            ApplicationId = 2,
            Type = InteractionType.ApplicationCommand,
            Token = "tok",
            User = new JsonUser { Id = DiscordId, Username = "u" },
            Data = new JsonInteractionData { Id = 9, Name = name, Type = ApplicationCommandType.ChatInput },
            Channel = new JsonChannel { Id = 6, Type = ChannelType.TextGuildChannel },
        };
        FillNullArrays(json);
        var interaction = new SlashCommandInteraction(json, null, SendResponse, new RestClient(new RestClientConfiguration { RequestHandler = _rest }));
        return new ApplicationCommandContext(interaction, null!);
    }

    private InteractionMessageProperties SentMessage() =>
        (InteractionMessageProperties)_callbacks.Single().GetType().GetProperty("Data")!.GetValue(_callbacks.Single())!;

    private static Season NewSeason() => new()
    {
        SeasonName = "Discord Audit Season",
        PublicAt = DateTime.UtcNow,
        SignupsOpen = DateTime.UtcNow,
        SignupsClose = DateTime.UtcNow.AddDays(7),
        DraftStart = DateTime.UtcNow.AddDays(8),
        SeasonStart = DateTime.UtcNow.AddDays(14),
        SeasonEnd = DateTime.UtcNow.AddDays(60),
    };

    // Mirrors the per-interaction scope: the context the module writes through shares the scope's ICurrentUserService
    private async Task<IServiceProvider> ScopeWithDatabase()
    {
        var ambient = new AmbientCurrentUser();
        var currentUserService = new CurrentUserService(new HttpContextAccessor(), ambient);
        _context = await SetUpFixture.NewGrifballContext(new Database.Interceptors.AuditInterceptor(currentUserService));
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
        var user = new Database.Models.User { UserName = "linked", DiscordUser = new DiscordUser { DiscordUserID = (long)DiscordId, DiscordUsername = "linked" } };
        _context!.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleAsync(Button(), null, _logger, services);

        var season = NewSeason();
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(PreExecutionResult.Continue));
            Assert.That(season.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(season.ModifiedByID, Is.EqualTo(user.Id));
            // Set after the lookup's await, yet seen here, by the awaiting caller (NetCord, in production)
            Assert.That(services.GetRequiredService<IAmbientCurrentUser>().UserId, Is.EqualTo(user.Id));
            Assert.That(_logger.Entries, Is.Empty);
        });
    }

    [Test]
    public async Task PreExecution_UnlinkedDiscordUser_LeavesAuditUserNull()
    {
        var services = await ScopeWithDatabase();

        var result = await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleAsync(SlashCommand(), null, _logger, services);

        var season = NewSeason();
        _context!.Seasons.Add(season);
        await _context.SaveChangesAsync();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(PreExecutionResult.Continue));
            Assert.That(season.CreatedByID, Is.Null);
            Assert.That(season.ModifiedByID, Is.Null);
            Assert.That(services.GetRequiredService<ICurrentUserService>().GetCurrentUserId(), Is.Null);
        });
    }

    [Test]
    public async Task PreExecution_LookupFails_LogsAndStillContinues()
    {
        // No GrifballContext registered, so the lookup throws
        var services = _ambientOnly;

        var result = await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleAsync(Button(), null, _logger, services);

        var entry = _logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.SameAs(PreExecutionResult.Continue));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo($"Failed to look up the app user for Discord user {DiscordId}"));
            Assert.That(entry.Exception, Is.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task Result_ComponentException_LogsAndRespondsWithGenericMessage()
    {
        var exception = new InvalidOperationException("secret details");

        await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(new ExecutionExceptionResult(exception), Button("signup:1"), null, _logger, _ambientOnly);

        var entry = _logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo($"Discord interaction signup:1 failed for Discord user {DiscordId}"));
            Assert.That(entry.Exception, Is.SameAs(exception));
            Assert.That(SentMessage().Content, Is.EqualTo(DiscordInteractionPipeline.GenericErrorMessage));
            Assert.That(SentMessage().Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(_rest.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Result_SlashCommandException_AlreadyResponded_SendsFollowup()
    {
        _responseError = new RestException(HttpStatusCode.BadRequest, "Bad Request", new RestError(40060, "Interaction has already been acknowledged.", null));
        var exception = new InvalidOperationException("secret details");

        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new ExecutionExceptionResult(exception), SlashCommand("setgamertag"), null, _logger, _ambientOnly);

        var request = _rest.Requests.Single();
        var body = request.Json.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(_logger.Entries.Single().Message, Is.EqualTo($"Discord interaction setgamertag failed for Discord user {DiscordId}"));
            Assert.That(_callbacks, Is.Empty);
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Path, Does.EndWith("/webhooks/2/tok"));
            Assert.That(body.GetProperty("content").GetString(), Is.EqualTo(DiscordInteractionPipeline.GenericErrorMessage));
            Assert.That(body.GetProperty("flags").GetInt32(), Is.EqualTo((int)MessageFlags.Ephemeral));
            Assert.That(request.Body, Does.Not.Contain("secret details"));
        });
    }

    [Test]
    public async Task Result_OtherRestError_IsNotSwallowed()
    {
        _responseError = new RestException(HttpStatusCode.NotFound, "Not Found", new RestError(10062, "Unknown interaction", null));

        var thrown = await Assert.ThrowsAsync<RestException>(async () =>
            await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(new ExecutionExceptionResult(new Exception()), Button(), null, _logger, _ambientOnly));
        Assert.That(thrown?.Error?.Code, Is.EqualTo(10062));
        Assert.That(_rest.Requests, Is.Empty);
    }

    [Test]
    public async Task Result_NonExceptionFailure_RespondsWithNetCordMessage()
    {
        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new PreconditionFailResult("Required permissions"), SlashCommand(), null, _logger, _ambientOnly);

        Assert.Multiple(() =>
        {
            Assert.That(SentMessage().Content, Is.EqualTo("Required permissions"));
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Debug));
        });
    }

    [Test]
    public async Task Result_Success_SendsNothing()
    {
        var success = NSubstitute.Substitute.For<IExecutionResult>();

        await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(success, Button(), null, _logger, _ambientOnly);

        Assert.Multiple(() =>
        {
            Assert.That(_callbacks, Is.Empty);
            Assert.That(_rest.Requests, Is.Empty);
            Assert.That(_logger.Entries, Is.Empty);
        });
    }

    [Test]
    public void Use_InstallsOneHandlerForPreExecutionAndResults()
    {
        var commands = new ApplicationCommandServiceOptions<ApplicationCommandInteraction, ApplicationCommandContext, AutocompleteInteractionContext>();
        var buttons = new ComponentInteractionServiceOptions<ButtonInteraction, ButtonInteractionContext>();
        var menus = new ComponentInteractionServiceOptions<StringMenuInteraction, StringMenuInteractionContext>();
        var modals = new ComponentInteractionServiceOptions<ModalInteraction, ModalInteractionContext>();

        DiscordInteractionPipeline.Use(commands);
        DiscordInteractionPipeline.Use(buttons);
        DiscordInteractionPipeline.Use(menus);
        DiscordInteractionPipeline.Use(modals);

        Assert.Multiple(() =>
        {
            Assert.That(commands.PreExecutionHandler, Is.InstanceOf<ApplicationCommandPipeline<ApplicationCommandContext>>().And.SameAs(commands.ResultHandler));
            Assert.That(buttons.PreExecutionHandler, Is.InstanceOf<ComponentInteractionPipeline<ButtonInteractionContext>>().And.SameAs(buttons.ResultHandler));
            Assert.That(menus.PreExecutionHandler, Is.InstanceOf<ComponentInteractionPipeline<StringMenuInteractionContext>>().And.SameAs(menus.ResultHandler));
            Assert.That(modals.PreExecutionHandler, Is.InstanceOf<ComponentInteractionPipeline<ModalInteractionContext>>().And.SameAs(modals.ResultHandler));
        });
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

    private async Task<Database.Models.User> LinkedUser(ulong discordId, string name)
    {
        var user = new Database.Models.User { UserName = name, DiscordUser = new DiscordUser { DiscordUserID = (long)discordId, DiscordUsername = name } };
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
        Assert.Multiple(() =>
        {
            Assert.That(_callbacks, Is.Empty, "the module failed");
            Assert.That(season.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(season.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(season.ModifiedByID, Is.EqualTo(user.Id));
            Assert.That(probe.SeenInModule["one"], Is.EqualTo(user.Id));
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(first.CreatedByID, Is.EqualTo(user.Id));
            Assert.That(second.CreatedByID, Is.Null);
            Assert.That(second.CreatedAt, Is.Not.EqualTo(default(DateTime)));
            Assert.That(new AmbientCurrentUser().UserId, Is.Null, "the test's own flow must not see an interaction's user");
        });
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
        Assert.Multiple(() =>
        {
            Assert.That(aliceSeason.CreatedByID, Is.EqualTo(alice.Id));
            Assert.That(bobSeason.CreatedByID, Is.EqualTo(bob.Id));
        });
    }

    [Test]
    public async Task RealPipeline_WorkLeftRunningByTheModule_LosesTheUserWhenTheInteractionEnds()
    {
        var (handler, probe) = await RealPipeline();
        var user = await LinkedUser(DiscordId, "linked");

        await handler.HandleAsync(Button("factory-audit:background").Interaction);
        probe.InteractionEnded.SetResult();
        var seenAfterwards = await probe.Background!;

        Assert.Multiple(() =>
        {
            Assert.That(probe.SeenInModule["background"], Is.EqualTo(user.Id));
            Assert.That(seenAfterwards, Is.Null);
        });
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
        context.Seasons.Add(new Season
        {
            SeasonName = name,
            PublicAt = DateTime.UtcNow,
            SignupsOpen = DateTime.UtcNow,
            SignupsClose = DateTime.UtcNow.AddDays(7),
            DraftStart = DateTime.UtcNow.AddDays(8),
            SeasonStart = DateTime.UtcNow.AddDays(14),
            SeasonEnd = DateTime.UtcNow.AddDays(60),
        });
        await context.SaveChangesAsync();
    }
}
