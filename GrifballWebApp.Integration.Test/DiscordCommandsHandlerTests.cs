using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using NetCord;
using NetCord.Rest;
using NSubstitute;
using User = GrifballWebApp.Domain.Players.User;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// /matches (<see cref="DiscordCommandsHandler.RecentMatches"/>), called with a substitute context: the player's saved games, as
/// the site and the agents' get_recent_games have them. It pulls nothing from Halo and saves no Xbox account.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCommandsHandlerTests
{
    private const ulong DiscordId = 31337;
    private const long MyXboxId = 555;
    private GrifballContext _context = null!;
    private DiscordCommandsHandler _handler = null!;
    private IDiscordApplicationCommandContext _discordContext = null!;
    private readonly List<IDiscordMessageOptions> _modifications = [];
    private static readonly string[] expected = new[]
            {
                "Match ID", "Start Time", "Duration",
                "Team", "Score", "Outcome", "Players",
                "**-------------------------**",
                "Team", "Score", "Outcome", "Players",
            };
    private static readonly string[] expectedArray = new[] { "MyGT", "OtherGT" };
    private static readonly string[] expectedArray0 = new[] { "MyGT", "\u200B" };

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        _handler = new DiscordCommandsHandler(new GrifballWebApp.Server.Commands.CommandLookups(_context, null!, new XboxAccountService(_context)), new HaloTools(_context, new XboxAccountService(_context)));

        _discordContext = Substitute.For<IDiscordApplicationCommandContext>();
        _discordContext.User.Id.Returns(DiscordId);
        // Run each edit of the deferred response against a substitute so the test can see what it set
        _discordContext.Interaction
            .When(x => x.ModifyResponseAsync(Arg.Any<Action<IDiscordMessageOptions>>(), Arg.Any<RestRequestProperties?>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var options = Substitute.For<IDiscordMessageOptions>();
                call.Arg<Action<IDiscordMessageOptions>>()(options);
                _modifications.Add(options);
            });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private object? OnlyModification(string method)
        => _modifications.Single().ReceivedCalls().Single(c => c.GetMethodInfo().Name == method).GetArguments()[0];

    private string? ModifiedContent() => (string?)OnlyModification(nameof(IDiscordMessageOptions.WithContent));

    private List<EmbedProperties> ModifiedEmbeds() => ((IEnumerable<EmbedProperties>)OnlyModification(nameof(IDiscordMessageOptions.WithEmbeds))!).ToList();

    private async Task AssertDeferred()
    {
        await _discordContext.Interaction.Received(1).SendResponseAsync(Arg.Is<InteractionCallbackProperties>(cb => cb.Type == InteractionCallbackType.DeferredMessage));
    }

    private async Task LinkMe()
    {
        _context.Users.Add(User.Seed(
            userName: "me",
            xboxUser: XboxUser.FromXbox(MyXboxId, "MyGT"),
            discordUser: Domain.Players.DiscordUser.FromDiscord((long)DiscordId, "me")));
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task RecentMatches_AGamertagTheSiteDoesntKnow_FindsNoAccount_AndLooksNothingUp()
    {
        await _handler.RecentMatches(_discordContext, "Nobody");

        await AssertDeferred();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ModifiedContent(), Is.EqualTo(GrifballWebApp.Server.Commands.CommandLookups.NoSuchXboxAccount));
            Assert.That(_context.XboxUsers.Any(), Is.False, "no Xbox account is saved for it");
        }
    }

    [Test]
    public async Task RecentMatches_NoGamertagAndNotLinked_AsksForAGamertag()
    {
        await _handler.RecentMatches(_discordContext, null);

        await AssertDeferred();
        Assert.That(ModifiedContent(), Is.EqualTo(DiscordCommandsHandler.NoGamertag));
    }

    [Test]
    public async Task RecentMatches_NoSavedGames_SaysSo()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(777, "Someone"));
        await _context.SaveChangesAsync();

        await _handler.RecentMatches(_discordContext, "someone");

        await AssertDeferred();
        Assert.That(ModifiedContent(), Is.EqualTo("The site has no games saved for Someone."));
    }

    [Test]
    public async Task RecentMatches_Matches_RepliesWithAnEmbedPerMatch_NewestFirst()
    {
        await LinkMe();
        _context.XboxUsers.Add(XboxUser.FromXbox(556, "OtherGT"));
        var now = DateTime.UtcNow;
        Outcomes[] myOutcomes = [Outcomes.Won, Outcomes.Lost, Outcomes.Tie, Outcomes.DidNotFinish];
        var matchIds = new List<Guid>();
        for (var i = 0; i < myOutcomes.Length; i++)
        {
            var match = Match.Seed(Guid.NewGuid(), startTime: now.AddHours(-i), endTime: now.AddHours(-i).AddMinutes(10), duration: TimeSpan.FromMinutes(10));
            match.AddTeam(0, 50, myOutcomes[i]);
            match.AddTeam(1, 40, Outcomes.Lost);
            match.AddParticipant(0, MyXboxId, new MatchParticipantStats());
            match.AddParticipant(1, 556, new MatchParticipantStats());
            _context.Matches.Add(match);
            matchIds.Add(match.MatchID);
        }
        await _context.SaveChangesAsync();

        await _handler.RecentMatches(_discordContext, null);

        await AssertDeferred();
        var embeds = ModifiedEmbeds();
        Assert.That(embeds, Has.Count.EqualTo(4));
        Assert.That(embeds.Select(e => e.Color), Is.EqualTo(new[]
        {
            new Color(0, 255, 0), // Won
            new Color(255, 0, 0), // Lost
            new Color(255, 255, 0), // Tie
            new Color(255, 255, 255), // anything else
        }));

        var newest = embeds[0];
        var fields = newest.Fields!.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(newest.Title, Is.EqualTo("Halo Infinite Match"));
            Assert.That(fields[0].Name, Is.EqualTo("Match ID"));
            Assert.That(fields[0].Value, Is.EqualTo($"[{matchIds[0]}](https://www.halowaypoint.com/halo-infinite/players/MyGT/matches/{matchIds[0]})"));
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(expected));
            Assert.That(fields.Where(f => f.Name == "Players").Select(f => f.Value), Is.EquivalentTo(expectedArray));
            Assert.That(fields.Select(f => f.Value), Has.All.Not.Null.And.Not.Empty, "Discord rejects an embed field without a value");
            Assert.That(fields.Single(f => f.Name is { } name && name.StartsWith("**---")).Value, Is.EqualTo("\u200B"), "The divider has a zero-width value");
        }
    }

    [Test]
    public async Task RecentMatches_TeamWithNoPlayers_StillHasAPlayersValue()
    {
        await LinkMe();
        var match = Match.Seed(Guid.NewGuid(), startTime: DateTime.UtcNow, endTime: DateTime.UtcNow, duration: TimeSpan.FromMinutes(1));
        match.AddTeam(0, 50, Outcomes.Won);
        match.AddTeam(1, 0, Outcomes.Lost);
        match.AddParticipant(0, MyXboxId, new MatchParticipantStats());
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();

        await _handler.RecentMatches(_discordContext, null);

        var players = ModifiedEmbeds().Single().Fields!.Where(f => f.Name == "Players").Select(f => f.Value).ToList();
        Assert.That(players, Is.EqualTo(expectedArray0));
    }
}
