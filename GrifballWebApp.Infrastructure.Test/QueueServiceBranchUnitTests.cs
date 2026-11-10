using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;
using User = GrifballWebApp.Domain.Players.User;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Infrastructure.Matchmaking;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>QueueServiceBranchTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueueServiceBranchUnitTests
{
    private static readonly string[] ExpectedOptions = ["xbox", "disc", "display", "4"];
    private static readonly string[] ExpectedButtons = ["Home Team Won", "Away Team Won", "Cancel"];

    [Test]
    public void CreateVoteMessage_UsesBestAvailableName()
    {
        var users = new[]
        {
            User.Seed(id: 1, xboxUser: XboxUser.FromXbox(0, "xbox"), discordUser: DiscordUser.FromDiscord(0, "disc")),
            User.Seed(id: 2, discordUser: DiscordUser.FromDiscord(0, "disc")),
            User.Seed(id: 3, displayName: "display"),
            User.Seed(id: 4),
        };

        var message = QueueService.CreateVoteMessage(5, users);

        var menu = message.Components!.OfType<StringMenuProperties>().Single();
        var buttons = message.Components!.OfType<ActionRowProperties>().Single().Components.OfType<ButtonProperties>().ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(menu.Options.Select(o => o.Label), Is.EqualTo(ExpectedOptions));
            Assert.That(menu.Placeholder, Is.EqualTo("Vote to kick"));
            Assert.That(buttons.Select(b => b.Label), Is.EqualTo(ExpectedButtons));
            Assert.That(buttons.Select(b => b.CustomId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(buttons.All(b => b.CustomId.Contains('5')), Is.True, "Match id is encoded in the custom id");
        }
    }
}
