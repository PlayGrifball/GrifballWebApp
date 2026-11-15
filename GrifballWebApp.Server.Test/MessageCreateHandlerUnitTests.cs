using DiscordInterface.Generated;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>MessageCreateHandlerTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
public class MessageCreateHandlerUnitTests : MessageCreateHandlerFixture
{
    private static readonly string[] AnsweredSpans = [MessageCreateHandler.MessageSpanName];

    [Test]
    public void Message_IsBuiltFromJsonModel()
    {
        var message = Message(5, [7], (40, 30));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Author.Id, Is.EqualTo(5ul));
            Assert.That(message.MentionedUsers.Select(x => x.Id), Is.EqualTo(MentionedSeven));
            Assert.That(message.MessageReference!.ChannelId, Is.EqualTo(40ul));
            Assert.That(message.MessageReference!.MessageId, Is.EqualTo(30ul));
        }
    }

    [Test]
    public async Task HandleAsync_IgnoresBotsOwnMessages_EvenIfTheyMentionTheBot()
    {
        await _handler.HandleAsync(Message(BotId, [BotId], (40, 30)));

        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
        AssertNoResponse();
    }

    [Test]
    public async Task HandleAsync_PlainMessageNotMentioningBot_DoesNothing()
    {
        await _handler.HandleAsync(Message(5, [6]));

        Assert.That(_discordClient.ReceivedCalls(), Is.Empty);
        AssertNoResponse();
    }

    [Test]
    public async Task HandleAsync_ADirectMessage_IsAnswered_WithNoMention()
    {
        await _handler.HandleAsync(GatewayMessages.Build(5, direct: true));

        // Answered: its scope is asked for (the test's has no database, so the answer fails and is logged, not thrown)
        Assert.That(_scopeFactory.ReceivedCalls(), Is.Not.Empty);
    }

    [Test]
    public async Task HandleAsync_ReplyToNonBotMessage_DoesNothing()
    {
        var referenced = Substitute.For<IDiscordRestMessage>();
        referenced.Author.Id.Returns(99ul);
        _discordClient.GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(referenced);

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        await _discordClient.Received(1).GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        AssertNoResponse();
    }

    [TestCase(99ul, false)]
    [TestCase(BotId, true)]
    public async Task HandleAsync_AReply_IsExportedOnlyWhenTheBotAnswers_ItsLookupWithinItsSpan(ulong repliedTo, bool answered)
    {
        using var exported = new ExportedSpans();
        System.Diagnostics.Activity? message = null;
        var referenced = Substitute.For<IDiscordRestMessage>();
        referenced.Author.Id.Returns(repliedTo);
        _discordClient.GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            message = System.Diagnostics.Activity.Current;
            // The lookup's own call (the HTTP client's span)
            ExportedSpans.Call.StartActivity("GET message")?.Dispose();
            return referenced;
        });

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        List<System.Diagnostics.Activity> spans = message is null ? [] : exported.Of(message.TraceId);
        using (Assert.EnterMultipleScope())
        {
            // The lookup is within the message's span, the trace's root, which covers its time
            Assert.That(message?.OperationName, Is.EqualTo(MessageCreateHandler.MessageSpanName));
            Assert.That(message?.Parent, Is.Null);
            Assert.That(message?.GetTagItem("discord.answered"), Is.EqualTo(answered));
            // A reply between others, which the bot never answers, leaves nothing; the lookup's call, made before it was known
            // whether the bot answers, is never exported
            Assert.That(spans.Select(s => s.OperationName), Is.EqualTo(answered ? AnsweredSpans : Array.Empty<string>()));
        }
    }

    [Test]
    public async Task HandleAsync_AReplyWhoseLookupFails_IsLogged_AndItsSpanExportedAsAnError()
    {
        using var exported = new ExportedSpans();
        System.Diagnostics.Activity? message = null;
        _discordClient.GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns<IDiscordRestMessage>(_ =>
        {
            message = System.Diagnostics.Activity.Current;
            // As Discord's REST client fails for a message deleted, or one the bot can't read
            throw new InvalidOperationException("Missing Access to SecretChannel");
        });

        Assert.That(async () => await _handler.HandleAsync(Message(5, reference: (40, 30))), Throws.Nothing,
            "one failing message mustn't fault the gateway handler");

        var span = message is null ? null : exported.Of(message.TraceId).SingleOrDefault();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(span?.Status, Is.EqualTo(System.Diagnostics.ActivityStatusCode.Error));
            Assert.That(span?.StatusDescription, Is.EqualTo(nameof(InvalidOperationException)), "the exception's type: its message may name what a user can't see");
            Assert.That(_logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
        }
    }

    [Test]
    public async Task HandleAsync_ReplyWhereReferencedMessageIsMissing_DoesNothing()
    {
        _discordClient.GetMessageAsync(Arg.Any<ulong>(), Arg.Any<ulong>(), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>())
            .Returns((IDiscordRestMessage)null!);

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        await _discordClient.Received(1).GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        AssertNoResponse();
    }

    [Test]
    public async Task HandleAsync_WhenHandlingTheMessageFails_LogsInsteadOfThrowing()
    {
        // Stands in for any failure (a down Ollama, a DB error, ...)
        _scopeFactory.CreateScope().Returns(_ => throw new InvalidOperationException("boom"));
        var message = Message(5, [BotId]);

        await _handler.HandleAsync(message);

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(entry.Message, Does.Contain("Failed to handle message").And.Contain(message.Author.Id.ToString()));
        }
    }

    [Test]
    public async Task HandleAsync_WhenHandlingTheMessageFails_ItsSpanIsAnError()
    {
        using var spans = new ChatSpans();
        System.Diagnostics.Activity? span = null;
        _scopeFactory.CreateScope().Returns(_ =>
        {
            span = System.Diagnostics.Activity.Current;
            throw new InvalidOperationException("boom");
        });

        await _handler.HandleAsync(Message(5, [BotId]));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(span?.OperationName, Is.EqualTo(MessageCreateHandler.MessageSpanName));
            Assert.That(span?.Status, Is.EqualTo(System.Diagnostics.ActivityStatusCode.Error));
            Assert.That(span?.StatusDescription, Is.EqualTo(nameof(InvalidOperationException)), "the exception's type: its message may quote a user");
        }
    }

    [Test]
    public async Task HandleAsync_ReplyToTheBotsMessage_IsAnswered()
    {
        var referenced = Substitute.For<IDiscordRestMessage>();
        referenced.Author.Id.Returns(BotId);
        _discordClient.GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(referenced);

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        // Answered: its scope is asked for (the test's has no database, so the answer fails and is logged, not thrown)
        Assert.That(_scopeFactory.ReceivedCalls(), Is.Not.Empty);
    }
}
