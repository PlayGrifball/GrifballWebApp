using DiscordInterface.Generated;
using GrifballWebApp.Application.Discord;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Infrastructure.Matchmaking;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Server.Matchmaking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// UpdateDisplayHandler, the queue display's refresh: QueueService's pass on a temp-file SQLite GrifballContext (an
/// empty queue), with Discord substituted. The queue channel has no display message yet, so one is posted.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UpdateDisplayHandlerUnitTests : SqliteGrifballContextFixture
{
    [Test]
    public async Task Handle_PostsTheQueueDisplay_ToTheQueueChannel()
    {
        var discord = Substitute.For<IDiscordRestClient>();
        discord.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IDiscordCurrentUser>());
        discord.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        var queue = new QueueService(NullLogger<QueueService>.Instance, Options.Create(new DiscordOptions { QueueChannel = 55 }), new QueueRepository(Db), discord, Db,
            Substitute.For<IDataPullService>(), new ContextOutbox(Db), Substitute.For<IQueueNotifier>());

        await new UpdateDisplayHandler(queue).Handle(new UpdateDisplayNotification(), CancellationToken.None);

        await discord.Received(1).SendMessageAsync(55ul, Arg.Is<MessageProperties>(m => m.Content == "Late League Queue"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }
}
