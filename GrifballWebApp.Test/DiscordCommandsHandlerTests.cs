using DiscordInterface.Generated;
using DiscordInterfaces;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Services;
using Microsoft.Extensions.Logging;
using NetCord;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using User = GrifballWebApp.Database.Models.User;

namespace GrifballWebApp.Test;

/// <summary>Tests for /matches (<see cref="DiscordCommandsHandler.RecentMatches"/>), called with a substitute context.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordCommandsHandlerTests
{
    private const ulong DiscordId = 31337;
    private const long MyXboxId = 555;
    private GrifballContext _context = null!;
    private IDataPullService _dataPullService = null!;
    private IGetsertXboxUserService _getsertXboxUserService = null!;
    private RecordingLogger<DiscordCommandsHandler> _logger = null!;
    private DiscordCommandsHandler _handler = null!;
    private IDiscordApplicationCommandContext _discordContext = null!;
    private readonly List<IDiscordMessageOptions> _modifications = [];

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _dataPullService = Substitute.For<IDataPullService>();
        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();
        _logger = new RecordingLogger<DiscordCommandsHandler>();
        _handler = new DiscordCommandsHandler(_context, _logger, _dataPullService, _getsertXboxUserService);

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
        _context.Users.Add(new User
        {
            UserName = "me",
            XboxUser = new XboxUser { XboxUserID = MyXboxId, Gamertag = "MyGT" },
            DiscordUser = new Database.Models.DiscordUser { DiscordUserID = (long)DiscordId, DiscordUsername = "me" },
        });
        await _context.SaveChangesAsync();
    }

    [Test]
    public async Task RecentMatches_UnknownGamertag_RepliesWithTheLookupMessage()
    {
        _getsertXboxUserService.GetsertXboxUserByGamertag("Nobody", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(XboxUser?, string?)>((null, "Gamertag not found")));

        await _handler.RecentMatches(_discordContext, "Nobody");

        await AssertDeferred();
        Assert.That(ModifiedContent(), Is.EqualTo("Gamertag not found"));
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task RecentMatches_NoGamertagAndNotLinked_AsksForAGamertag()
    {
        await _handler.RecentMatches(_discordContext, null);

        await AssertDeferred();
        Assert.That(ModifiedContent(), Is.EqualTo("You must set your gamertag first, or provide someone else's gamertag to search for"));
        await _getsertXboxUserService.DidNotReceiveWithAnyArgs().GetsertXboxUserByGamertag(default!);
        await _dataPullService.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
    }

    [Test]
    public async Task RecentMatches_DataPullFails_LogsAndReplies()
    {
        await LinkMe();
        _dataPullService.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("boom"));

        await _handler.RecentMatches(_discordContext, null);

        Assert.That(ModifiedContent(), Is.EqualTo("Failed to fetch recent matches, if this continues to occur contact sysadmin"));
        var entry = _logger.Entries.Single();
        Assert.Multiple(() =>
        {
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(entry.Message, Does.Contain("MyGT"));
        });
    }

    [Test]
    public async Task RecentMatches_NoMatches_SaysSo()
    {
        _getsertXboxUserService.GetsertXboxUserByGamertag("Someone", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<(XboxUser?, string?)>((new XboxUser { XboxUserID = 777, Gamertag = "Someone" }, null)));

        await _handler.RecentMatches(_discordContext, "Someone");

        await AssertDeferred();
        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(x => x.SequenceEqual(new long[] { 777 })), 0, 0, 5, Arg.Any<CancellationToken>());
        Assert.That(ModifiedContent(), Is.EqualTo("I found no matches for Someone"));
    }

    [Test]
    public async Task RecentMatches_Matches_RepliesWithAnEmbedPerMatch_NewestFirst()
    {
        await LinkMe();
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 556, Gamertag = "OtherGT" });
        var now = DateTime.UtcNow;
        Outcomes[] myOutcomes = [Outcomes.Won, Outcomes.Lost, Outcomes.Tie, Outcomes.DidNotFinish];
        var matchIds = new List<Guid>();
        for (var i = 0; i < myOutcomes.Length; i++)
        {
            var match = new Match
            {
                MatchID = Guid.NewGuid(),
                StartTime = now.AddHours(-i),
                EndTime = now.AddHours(-i).AddMinutes(10),
                Duration = TimeSpan.FromMinutes(10),
            };
            match.MatchTeams =
            [
                new MatchTeam { Match = match, TeamID = 0, Score = 50, Outcome = myOutcomes[i], MatchParticipants = [new MatchParticipant { XboxUserID = MyXboxId }] },
                new MatchTeam { Match = match, TeamID = 1, Score = 40, Outcome = Outcomes.Lost, MatchParticipants = [new MatchParticipant { XboxUserID = 556 }] },
            ];
            _context.Matches.Add(match);
            matchIds.Add(match.MatchID);
        }
        await _context.SaveChangesAsync();

        await _handler.RecentMatches(_discordContext, null);

        await AssertDeferred();
        await _dataPullService.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(x => x.SequenceEqual(new long[] { MyXboxId })), 0, 0, 5, Arg.Any<CancellationToken>());
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
        Assert.Multiple(() =>
        {
            Assert.That(newest.Title, Is.EqualTo("Halo Infinite Match"));
            Assert.That(fields[0].Name, Is.EqualTo("Match ID"));
            Assert.That(fields[0].Value, Is.EqualTo($"[{matchIds[0]}](https://www.halowaypoint.com/halo-infinite/players/MyGT/matches/{matchIds[0]})"));
            Assert.That(fields.Select(f => f.Name), Is.EqualTo(new[]
            {
                "Match ID", "Start Time", "Duration",
                "Team", "Score", "Outcome", "Players",
                "**-------------------------**",
                "Team", "Score", "Outcome", "Players",
            }));
            Assert.That(fields.Where(f => f.Name == "Players").Select(f => f.Value), Is.EquivalentTo(new[] { "MyGT", "OtherGT" }));
            Assert.That(fields.Select(f => f.Value), Has.All.Not.Null.And.Not.Empty, "Discord rejects an embed field without a value");
            Assert.That(fields.Single(f => f.Name.StartsWith("**---")).Value, Is.EqualTo("\u200B"), "The divider has a zero-width value");
        });
    }

    [Test]
    public async Task RecentMatches_TeamWithNoPlayers_StillHasAPlayersValue()
    {
        await LinkMe();
        var match = new Match { MatchID = Guid.NewGuid(), StartTime = DateTime.UtcNow, EndTime = DateTime.UtcNow, Duration = TimeSpan.FromMinutes(1) };
        match.MatchTeams =
        [
            new MatchTeam { Match = match, TeamID = 0, Score = 50, Outcome = Outcomes.Won, MatchParticipants = [new MatchParticipant { XboxUserID = MyXboxId }] },
            new MatchTeam { Match = match, TeamID = 1, Score = 0, Outcome = Outcomes.Lost },
        ];
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();

        await _handler.RecentMatches(_discordContext, null);

        var players = ModifiedEmbeds().Single().Fields!.Where(f => f.Name == "Players").Select(f => f.Value).ToList();
        Assert.That(players, Is.EqualTo(new[] { "MyGT", "\u200B" }));
    }
}
