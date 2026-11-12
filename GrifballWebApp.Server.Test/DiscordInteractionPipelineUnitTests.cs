using System.Net;
using GrifballWebApp.Infrastructure.Services;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

namespace GrifballWebApp.Server.Test;

/// <summary>DiscordInteractionPipelineTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordInteractionPipelineUnitTests : DiscordInteractionPipelineFixture
{
    [Test]
    public async Task PreExecution_LookupFails_LogsAndStillContinues()
    {
        // No GrifballContext registered, so the lookup throws
        var services = _ambientOnly;

        var result = await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleAsync(Button(), null, _logger, services);

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.SameAs(PreExecutionResult.Continue));
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo($"Failed to look up the app user for Discord user {DiscordId}"));
            Assert.That(entry.Exception, Is.InstanceOf<InvalidOperationException>());
        }
    }

    [Test]
    public async Task Result_ComponentException_LogsAndRespondsWithGenericMessage()
    {
        var exception = new InvalidOperationException("secret details");

        await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(new ExecutionExceptionResult(exception), Button("signup:1"), null, _logger, _ambientOnly);

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo($"Discord interaction signup:1 failed for Discord user {DiscordId}"));
            Assert.That(entry.Exception, Is.SameAs(exception));
            Assert.That(SentMessage().Content, Is.EqualTo(DiscordInteractionPipeline.GenericErrorMessage));
            Assert.That(SentMessage().Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(_rest.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task Result_Refusal_RespondsWithItsMessage_AsTheSiteDoes()
    {
        var exception = new GrifballWebApp.Application.Reschedule.RescheduleAlreadyActiveException();

        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new ExecutionExceptionResult(exception), SlashCommand("reschedule"), null, _logger, _ambientOnly);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_logger.Entries.Where(e => e.Level >= LogLevel.Warning), Is.Empty);
            Assert.That(SentMessage().Content, Is.EqualTo(exception.Message));
            Assert.That(SentMessage().Flags, Is.EqualTo(MessageFlags.Ephemeral));
        }
    }

    [Test]
    public async Task Result_SlashCommandException_AlreadyResponded_SendsFollowup()
    {
        _responseError = new RestException(HttpStatusCode.BadRequest, "Bad Request", new RestError(40060, "Interaction has already been acknowledged.", null));
        var exception = new InvalidOperationException("secret details");

        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new ExecutionExceptionResult(exception), SlashCommand("setgamertag"), null, _logger, _ambientOnly);

        var request = _rest.Requests.Single();
        var body = request.Json.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_logger.Entries.Single().Message, Is.EqualTo($"Discord interaction setgamertag failed for Discord user {DiscordId}"));
            Assert.That(_callbacks, Is.Empty);
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Path, Does.EndWith("/webhooks/2/tok"));
            Assert.That(body.GetProperty("content").GetString(), Is.EqualTo(DiscordInteractionPipeline.GenericErrorMessage));
            Assert.That(body.GetProperty("flags").GetInt32(), Is.EqualTo((int)MessageFlags.Ephemeral));
            Assert.That(request.Body, Does.Not.Contain("secret details"));
        }
    }

    [Test]
    public async Task Result_OtherRestError_IsNotSwallowed()
    {
        _responseError = new RestException(HttpStatusCode.NotFound, "Not Found", new RestError(10062, "Unknown interaction", null));

        var thrown = await Assert.ThrowsAsync<RestException>(async () =>
            await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(new ExecutionExceptionResult(new Exception()), Button(), null, _logger, _ambientOnly));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(thrown?.Error?.Code, Is.EqualTo(10062));
            Assert.That(_rest.Requests, Is.Empty);
        }
    }

    [Test]
    public async Task Result_NonExceptionFailure_RespondsWithNetCordMessage()
    {
        await new ApplicationCommandPipeline<ApplicationCommandContext>().HandleResultAsync(new PreconditionFailResult("Required permissions"), SlashCommand(), null, _logger, _ambientOnly);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SentMessage().Content, Is.EqualTo("Required permissions"));
            Assert.That(_logger.Entries.Single().Level, Is.EqualTo(LogLevel.Debug));
        }
    }

    [Test]
    public async Task Result_Success_SendsNothing()
    {
        var success = NSubstitute.Substitute.For<IExecutionResult>();

        await new ComponentInteractionPipeline<ButtonInteractionContext>().HandleResultAsync(success, Button(), null, _logger, _ambientOnly);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_callbacks, Is.Empty);
            Assert.That(_rest.Requests, Is.Empty);
            Assert.That(_logger.Entries, Is.Empty);
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(commands.PreExecutionHandler, Is.InstanceOf<ApplicationCommandPipeline<ApplicationCommandContext>>().And.SameAs(commands.ResultHandler));
            Assert.That(buttons.PreExecutionHandler, Is.InstanceOf<ComponentInteractionPipeline<ButtonInteractionContext>>().And.SameAs(buttons.ResultHandler));
            Assert.That(menus.PreExecutionHandler, Is.InstanceOf<ComponentInteractionPipeline<StringMenuInteractionContext>>().And.SameAs(menus.ResultHandler));
            Assert.That(modals.PreExecutionHandler, Is.InstanceOf<ComponentInteractionPipeline<ModalInteractionContext>>().And.SameAs(modals.ResultHandler));
        }
    }
}
