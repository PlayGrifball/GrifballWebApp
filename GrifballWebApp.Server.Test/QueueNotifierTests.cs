using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GrifballWebApp.Server.Test;

[TestFixture]
public class QueueNotifierTests
{
    private static readonly string[] PlayerUserIDs = ["4", "12"];

    private IQueueHubClient _all;
    private IQueueHubClient _players;
    private IHubContext<QueueHub, IQueueHubClient> _hub;
    private RecordingLogger<QueueNotifier> _logger = null!;
    private QueueNotifier _notifier;

    [SetUp]
    public void SetUp()
    {
        _all = Substitute.For<IQueueHubClient>();
        _players = Substitute.For<IQueueHubClient>();
        _hub = Substitute.For<IHubContext<QueueHub, IQueueHubClient>>();
        _hub.Clients.All.Returns(_all);
        _hub.Clients.Users(Arg.Any<IReadOnlyList<string>>()).Returns(_players);
        _notifier = new QueueNotifier(_hub, _logger = new RecordingLogger<QueueNotifier>());
    }

    [Test]
    public async Task Send_TellsEveryone()
    {
        await _notifier.Send(3);
        await _notifier.Send(3);

        await _all.Received(2).QueueCount(3);
    }

    [Test]
    public async Task SendStatusChanged_TellsThosePlayers()
    {
        await _notifier.SendStatusChanged([4, 12]);

        await _players.Received(1).QueueStatusChanged();
        _hub.Clients.Received(1).Users(Arg.Is<IReadOnlyList<string>>(x => x.SequenceEqual(PlayerUserIDs)));
    }

    [Test]
    public async Task SendStatusChanged_ToNobody_SendsNothing()
    {
        await _notifier.SendStatusChanged([]);

        _hub.Clients.DidNotReceiveWithAnyArgs().Users(default(IReadOnlyList<string>)!);
    }

    [Test]
    public async Task SendStatusChanged_LogsFailures_InsteadOfThrowing()
    {
        _players.QueueStatusChanged().ThrowsAsync(new InvalidOperationException("backplane down"));

        await _notifier.SendStatusChanged([1]);

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
        }
    }

    [Test]
    public async Task Send_LogsFailures_InsteadOfThrowing()
    {
        _all.QueueCount(Arg.Any<int>()).ThrowsAsync(new InvalidOperationException("backplane down"));

        await _notifier.Send(1);

        var entry = _logger.Entries.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
        }
    }

    [Test]
    public void TheHub_HasNoMethodsToCall()
    {
        Assert.That(typeof(QueueHub).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly), Is.Empty);
    }
}
