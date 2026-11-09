using GrifballWebApp.Domain.LateLeague;
using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Notifications;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchmakingButtonVoteTests
{
    private GrifballContext _context;
    private IDiscordButtonInteractionContext _discordContext;
    private MatchmakingButtonHandler _buttons;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var discordClient = Substitute.For<IDiscordRestClient>();
        var options = Options.Create(new DiscordOptions { QueueChannel = 1 });
        var repo = Substitute.For<IQueueRepository>();
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), options, repo, discordClient, _context, Substitute.For<IDataPullService>(), new ContextOutbox(_context), Substitute.For<IQueueNotifier>());
        _buttons = new MatchmakingButtonHandler(new QueueMembership(repo, _context, Substitute.For<IQueueNotifier>()), Substitute.For<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(), _context, discordClient, queueService, Substitute.For<ILogger<MatchmakingButtonHandler>>());
        _discordContext = Substitute.For<IDiscordButtonInteractionContext>();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task VoteForWinner_UnparseableWinner_RepliesWithExpectedValues()
    {
        _context.Users.Add(User.Seed(userName: "voter", xboxUser: XboxUser.FromXbox(3, "gt"), discordUser: Domain.Players.DiscordUser.FromDiscord(42, "voter")));
        await _context.SaveChangesAsync();
        _discordContext.User.Id.Returns(42ul);

        await _buttons.VoteForWinner(_discordContext, 1, "Nobody");

        await _discordContext.AssertSendResponse("I could not parse the value Nobody. Contact developer, expected values are Home,Away,Cancel");
        Assert.That(await _context.Set<MatchedWinnerVote>().AnyAsync(), Is.False);
    }
}
