using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Server.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GruntUser = Surprenant.Grunt.Models.User;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class MatchmakingButtonVoteTests
{
    private GrifballContext _context;
    private IDiscordButtonInteractionContext _discordContext;
    private ButtonInteractions _buttons;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var discordClient = Substitute.For<IDiscordRestClient>();
        var options = Options.Create(new DiscordOptions { QueueChannel = 1 });
        var repo = Substitute.For<IQueueRepository>();
        var queueService = new QueueService(Substitute.For<ILogger<QueueService>>(), options, repo, discordClient, _context, Substitute.For<IDataPullService>());
        _buttons = new ButtonInteractions(repo, Substitute.For<IPublisher>(), _context, discordClient, options, queueService);
        _discordContext = Substitute.For<IDiscordButtonInteractionContext>();
        typeof(ButtonInteractions).GetField("_discordContext", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(_buttons, _discordContext);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task VoteForWinner_UnparseableWinner_RepliesWithExpectedValues()
    {
        _context.Users.Add(new User { UserName = "voter", XboxUser = new XboxUser { XboxUserID = 3, Gamertag = "gt" }, DiscordUser = new Database.Models.DiscordUser { DiscordUserID = 42, DiscordUsername = "voter" } });
        await _context.SaveChangesAsync();
        _discordContext.User.Id.Returns(42ul);

        await _buttons.VoteForWinner(1, "Nobody");

        await _discordContext.AssertSendResponse("I could not parse the value Nobody. Contact developer, expected values are Home,Away,Cancel");
        Assert.That(await _context.MatchedWinnerVotes.AnyAsync(), Is.False);
    }
}
