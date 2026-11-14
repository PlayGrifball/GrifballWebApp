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
    public async Task HandleAsync_ReplyToNonBotMessage_DoesNothing()
    {
        var referenced = Substitute.For<IDiscordRestMessage>();
        referenced.Author.Id.Returns(99ul);
        _discordClient.GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(referenced);

        await _handler.HandleAsync(Message(5, reference: (40, 30)));

        await _discordClient.Received(1).GetMessageAsync(40ul, 30ul, Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
        AssertNoResponse();
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
