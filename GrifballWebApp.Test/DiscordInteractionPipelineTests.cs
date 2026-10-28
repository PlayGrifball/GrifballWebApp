using System.Net;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
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

    private ButtonInteractionContext Button(string customId = "signup:1")
    {
        var json = new JsonInteraction
        {
            Id = 1,
            ApplicationId = 2,
            Type = InteractionType.MessageComponent,
            Token = "tok",
            User = new JsonUser { Id = DiscordId, Username = "u" },
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
        var currentUserService = new CurrentUserService(new HttpContextAccessor());
        _context = await SetUpFixture.NewGrifballContext(new Database.Interceptors.AuditInterceptor(currentUserService));
        return new ServiceCollection()
            .AddSingleton<GrifballContext>(_context)
            .AddSingleton<ICurrentUserService>(currentUserService)
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
        var services = new ServiceCollection().BuildServiceProvider();

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

        await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(new ExecutionExceptionResult(exception), Button("signup:1"), null, _logger, null!);

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

        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new ExecutionExceptionResult(exception), SlashCommand("setgamertag"), null, _logger, null!);

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
            await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(new ExecutionExceptionResult(new Exception()), Button(), null, _logger, null!));
        Assert.That(thrown?.Error?.Code, Is.EqualTo(10062));
        Assert.That(_rest.Requests, Is.Empty);
    }

    [Test]
    public async Task Result_NonExceptionFailure_RespondsWithNetCordMessage()
    {
        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new PreconditionFailResult("Required permissions"), SlashCommand(), null, _logger, null!);

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

        await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(success, Button(), null, _logger, null!);

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
}
