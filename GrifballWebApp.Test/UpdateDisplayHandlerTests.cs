using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Events;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using NetCord.Rest;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class UpdateDisplayHandlerTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task Handle_RefreshesQueueDisplay()
    {
        var discordClient = Substitute.For<IDiscordRestClient>();
        discordClient.GetCurrentUserAsync(Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>()).Returns(Substitute.For<IDiscordCurrentUser>());
        discordClient.GetMessagesAsync(Arg.Any<ulong>(), Arg.Any<PaginationProperties<ulong>>(), Arg.Any<RestRequestProperties>())
            .Returns(_ => AsyncEnumerable.Empty<IDiscordRestMessage>());
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), Options.Create(new DiscordOptions { QueueChannel = 55 }),
            new QueueRepository(_context), discordClient, _context, Substitute.For<IDataPullService>());

        await new UpdateDisplayHandler(queueService).Handle(new UpdateDisplayNotification(), CancellationToken.None);

        await discordClient.Received(1).SendMessageAsync(55ul, Arg.Is<MessageProperties>(m => m.Content == "Matchmaking Queue"), Arg.Any<RestRequestProperties>(), Arg.Any<CancellationToken>());
    }
}
