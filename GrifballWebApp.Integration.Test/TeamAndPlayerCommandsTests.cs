using System.Net;
using GrifballWebApp.Application;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetCord;
using NSubstitute;
using Team = GrifballWebApp.Domain.League.Team;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Integration.Test;

/// <summary>/team (<see cref="TeamCommandsHandler"/>) and /player (<see cref="PlayerCommandsHandler"/>), called with a substitute context.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamAndPlayerCommandsTests
{
    private GrifballContext _context = null!;
    private TeamCommandsHandler _teams = null!;
    private PlayerCommandsHandler _players = null!;
    private Season _season = null!;
    private Team _team = null!;
    private User _captain = null!;
    private User _player = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
        var urls = new UrlService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = "https://grif.test" }).Build());
        var lookups = new CommandLookups(_context, new SeasonService(_context, new SeasonStatsService(_context)));
        var vods = new VodService(_context, new YouTubeClient(new HttpClient(new FakeSourcesHandler(_ => FakeSourcesHandler.Status(HttpStatusCode.NotFound)))),
            Options.Create(new YouTubeOptions()), NullLogger<VodService>.Instance);
        var recentMatches = new DiscordCommandsHandler(_context, NullLogger<DiscordCommandsHandler>.Instance, Substitute.For<IDataPullService>(),
            Substitute.For<IGetsertXboxUserService>());
        _teams = new TeamCommandsHandler(lookups, new TeamPageService(_context), new TeamHistoryService(_context), vods, urls);
        _players = new PlayerCommandsHandler(lookups, new DiscordCaller(_context), new ProfileStatsService(_context, new SeasonStatsService(_context), new MatchmakingStatsService(_context)), new ProfileCommendationsService(_context),
            new DeploymentHistoryService(_context), vods, recentMatches, urls);

        _season = await _context.AddSeason("Spring");
        _captain = await _context.AddUser("cap", gamertag: "CaptainGT", discordName: "cap");
        _player = await _context.AddUser("pick", gamertag: "PickGT");
        _team = await _context.AddTeam(_season, "Alpha", _captain, 1, _player);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task TeamInfo_Should_ShowTheSeasonRecordAndCaptain()
    {
        var discord = new CommandContextRecorder();

        await _teams.Info(discord.Context, "alpha");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(embed.Title, Is.EqualTo("Alpha"));
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/season/{_season.SeasonID}/team/{_team.TeamID}"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Captain").Value, Is.EqualTo("CaptainGT"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Record").Value, Is.EqualTo("0-0"));
        }
    }

    [Test]
    public async Task TeamRoster_Should_ListTheCaptainFirst_ThenPicksByRound()
    {
        var discord = new CommandContextRecorder();

        await _teams.Roster(discord.Context, _team.TeamID.ToString());

        Assert.That(discord.Embed().Description, Is.EqualTo(
            $"[CaptainGT](https://grif.test/profile/{_captain.Id}) (captain)\n[PickGT](https://grif.test/profile/{_player.Id}) · round 1"));
    }

    [Test]
    public async Task TeamCommands_Should_SayThereIsNoSuchTeam_When_NoneMatches()
    {
        var discord = new CommandContextRecorder();

        await _teams.History(discord.Context, "nobody");

        Assert.That(discord.Content(), Is.EqualTo(TeamCommandsHandler.NoSuchTeam));
    }

    [Test]
    public async Task TeamMatches_Should_ListUnplayedMatchesAgainstTheOtherTeam()
    {
        var other = await _context.AddTeamWithCaptain(_season, "OtherGT", 9_000_001);
        _context.SeasonMatches.Add(SeasonMatch.Seed(_season.SeasonID, _team.TeamID, other.TeamID));
        await _context.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await _teams.Matches(discord.Context, _team.TeamID.ToString());

        Assert.That(discord.Embed().Description, Does.StartWith($"Unscheduled · [vs {other.TeamName}](https://grif.test/seasonmatch/"));
    }

    [Test]
    public async Task TeamHistoryAndVods_Should_LinkTheirPages()
    {
        var history = new CommandContextRecorder();
        var vods = new CommandContextRecorder();

        await _teams.History(history.Context, _team.TeamID.ToString());
        await _teams.Vods(vods.Context, _team.TeamID.ToString());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(history.Embed().Url, Is.EqualTo($"https://grif.test/history/team/{_team.TeamID}"));
            Assert.That(history.Embed().Description, Does.Contain("Spring"));
            Assert.That(vods.Embed().Description, Is.EqualTo("No VODs yet."));
        }
    }

    [Test]
    public async Task PlayerProfile_Should_BeTheCallersOwn_When_NoPlayerIsNamed()
    {
        var discord = new CommandContextRecorder((ulong)_captain.DiscordUserID!.Value);

        await _players.Profile(discord.Context, null);

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("CaptainGT"));
            Assert.That(embed.Url, Is.EqualTo($"https://grif.test/profile/{_captain.Id}"));
            Assert.That(embed.Fields!.Single(f => f.Name == "Region").Value, Is.EqualTo("Not set"));
        }
    }

    [Test]
    public async Task PlayerProfile_Should_OfferTheSetGamertagButton_When_TheCallerHasNoAccount_AndNamedNoPlayer()
    {
        var discord = new CommandContextRecorder(discordUserId: 777);

        await _players.Profile(discord.Context, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.Message().Content, Is.EqualTo(DiscordCaller.NoGamertag));
            Assert.That(discord.Edits, Is.Empty);
        }
    }

    [Test]
    public async Task PlayerCommands_Should_NeedNoAccount_When_APlayerIsNamed()
    {
        var stats = new CommandContextRecorder(discordUserId: 777);
        var commendations = new CommandContextRecorder(discordUserId: 777);
        var history = new CommandContextRecorder(discordUserId: 777);
        var vods = new CommandContextRecorder(discordUserId: 777);

        await _players.Stats(stats.Context, "pickgt");
        await _players.Commendations(commendations.Context, "PickGT");
        await _players.History(history.Context, _player.Id.ToString());
        await _players.Vods(vods.Context, "PickGT");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stats.Embed().Title, Is.EqualTo("PickGT stats"));
            Assert.That(commendations.Embed().Description, Does.StartWith("0 medals, 0 kinds"));
            Assert.That(history.Embed().Description, Does.Contain($"[Alpha](https://grif.test/season/{_season.SeasonID}/team/{_team.TeamID})"));
            Assert.That(vods.Embed().Description, Is.EqualTo("No VODs yet."));
        }
    }

    [Test]
    public async Task PlayerCommands_Should_SayThereIsNoSuchPlayer_Privately()
    {
        var profile = new CommandContextRecorder();
        var matches = new CommandContextRecorder();

        await _players.Profile(profile.Context, "nobody-at-all");
        await _players.Matches(matches.Context, "nobody-at-all");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(profile.Message().Content, Is.EqualTo(PlayerCommandsHandler.NoSuchPlayer));
            Assert.That(profile.Message().Flags, Is.EqualTo(MessageFlags.Ephemeral));
            Assert.That(matches.Message().Content, Is.EqualTo(PlayerCommandsHandler.NoSuchPlayer));
        }
    }

    [Test]
    public async Task TeamMatches_Should_ShowAPlayedMatchsResult()
    {
        var other = await _context.AddTeamWithCaptain(_season, "LoserGT", 9_000_002);
        var match = SeasonMatch.Seed(_season.SeasonID, _team.TeamID, other.TeamID);
        _context.SeasonMatches.Add(match);
        await _context.SaveChangesAsync();
        await new SeasonMatchService(_context, Substitute.For<IDataPullService>(),
            new BracketService(_context, new TeamStandingsService(_context), new ChampionshipService(_context)), new ChampionshipService(_context)).AwayForfeit(match.SeasonMatchID);
        var discord = new CommandContextRecorder();

        await _teams.Matches(discord.Context, _team.TeamID.ToString());

        Assert.That(discord.Embed().Description, Does.Contain(" · Won"));
    }

    [Test]
    public async Task PlayerMatches_Should_BeTheNamedPlayersRecentMatches()
    {
        var discord = new CommandContextRecorder();

        await _players.Matches(discord.Context, "PickGT");

        // /matches by the player's gamertag: it defers, as /matches does
        Assert.That(discord.DeferredPublicly(), Is.True);
    }
}
