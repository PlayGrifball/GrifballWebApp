using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The slash commands and the agents' tools share their methods: the season a command or tool means when it names none, and
/// /match pull and /player pull, which run the pull tools as the caller.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CommandsAndToolsShareUnitTests : SqliteGrifballContextFixture
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 14, 12, 0, 0, TimeSpan.Zero));
    private readonly ISeasonMatchPulls _seasonPulls = Substitute.For<ISeasonMatchPulls>();
    private readonly IXboxAccountPulls _accountPulls = Substitute.For<IXboxAccountPulls>();

    private SeasonService Seasons() => new(Db, new SeasonStatsService(Db), _time);

    private SeasonTools SeasonTools() => new(Seasons(), new TeamStandingsService(Db), new MatchPlannerService(Db), new GradesService(Db),
        new SignupsService(Db, _time), Substitute.For<IBracketService>());

    private SeasonMatchService SeasonMatches() => new(Db, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(Db));

    /// <summary>The pull tools made as the commands make them (GrifTools.Factory), from these services.</summary>
    private PullToolsFor Pulls() => new(new ServiceCollection()
        .AddSingleton(SeasonMatches()).AddSingleton(_seasonPulls).AddSingleton(new XboxAccountService(Db)).AddSingleton(_accountPulls)
        .BuildServiceProvider());

    private async Task<Season> SeasonEnded(string name, int daysAgo)
    {
        var end = _time.GetUtcNow().UtcDateTime.AddDays(-daysAgo);
        var season = Season.Seed(seasonName: name).WithDates(publicAt: end.AddDays(-90), signupsOpen: end.AddDays(-90), signupsClose: end.AddDays(-80),
            draftStart: end.AddDays(-75), seasonStart: end.AddDays(-60), seasonEnd: end);
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        return season;
    }

    [Test]
    public async Task BetweenSeasons_ACommandAndATool_BothMeanTheLatest()
    {
        await SeasonEnded("Older", daysAgo: 200);
        var latest = await SeasonEnded("Latest", daysAgo: 10);

        var command = await new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time).Season(null);
        var tool = await SeasonTools().GetSeason();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.EqualTo(latest.SeasonID));
            Assert.That(tool.SeasonName, Is.EqualTo("Latest"));
        }
    }

    [Test]
    public async Task BetweenSeasons_ANextSeasonNotAnnouncedYet_IsNotTheDefault_ForACommandOrATool()
    {
        var ended = await SeasonEnded("Ended", daysAgo: 10);
        // Made already (a higher ID), starting in 40 days and announced in 10: nobody signed out sees it yet
        await SeasonEnded("Next", daysAgo: -100);

        var command = await new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time).Season(null);
        var tool = await SeasonTools().GetSeason();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.EqualTo(ended.SeasonID));
            Assert.That(tool.SeasonName, Is.EqualTo("Ended"));
        }
    }

    [Test]
    public async Task BetweenSeasons_ANextSeasonAnnounced_IsTheDefault_ForACommandOrATool()
    {
        await SeasonEnded("Ended", daysAgo: 10);
        // Starts in 20 days, announced 10 days ago: public, its signups open
        var next = await SeasonEnded("Next", daysAgo: -80);

        var command = await new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time).Season(null);
        var tool = await SeasonTools().GetSeason();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.EqualTo(next.SeasonID));
            Assert.That(tool.SeasonName, Is.EqualTo("Next"));
        }
    }

    [Test]
    public async Task BetweenSeasons_TwoAnnounced_TheNextToStart_IsTheDefault()
    {
        await SeasonEnded("Ended", daysAgo: 10);
        // Both public: the one starting in 20 days, not the one starting in 120
        var next = await SeasonEnded("Next", daysAgo: -80);
        Db.Seasons.Add(Season.Seed(seasonName: "Later").WithDates(publicAt: _time.GetUtcNow().UtcDateTime.AddDays(-1),
            signupsOpen: _time.GetUtcNow().UtcDateTime.AddDays(90), signupsClose: _time.GetUtcNow().UtcDateTime.AddDays(100),
            draftStart: _time.GetUtcNow().UtcDateTime.AddDays(110), seasonStart: _time.GetUtcNow().UtcDateTime.AddDays(120), seasonEnd: _time.GetUtcNow().UtcDateTime.AddDays(180)));
        await Db.SaveChangesAsync();

        var command = await new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time).Season(null);
        var tool = await SeasonTools().GetSeason();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.EqualTo(next.SeasonID));
            Assert.That(tool.SeasonName, Is.EqualTo("Next"));
        }
    }

    [Test]
    public async Task WithNoSeasons_ACommandFindsNone_AndAToolSaysToNameOne()
    {
        var command = await new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time).Season(null);
        var tool = await Assert.ThrowsAsync<ToolException>(() => SeasonTools().GetSeason());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command, Is.Null);
            Assert.That(tool!.Message, Is.EqualTo(GrifballWebApp.Application.Tools.SeasonTools.NoSeasonYet));
        }
    }

    [Test]
    public async Task MatchPull_QueuesTheMatchsPull_AsTheCaller_ThroughThePullTool()
    {
        var caller = await AddUser(5, "Puller", "Player");
        var season = await AddSeason("Winter");
        var match = await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
        _seasonPulls.Queue(match.SeasonMatchID, caller.Id, true, Arg.Any<CancellationToken>()).Returns(new OnDemandJobStatusDto { State = OnDemandJobState.Queued });
        var handler = new MatchCommandsHandler(new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time), SeasonMatches(), new MatchDetailsService(Db), Urls(), new DiscordCaller(Db), Pulls());
        var discord = new CommandContextRecorder(discordUserId: 5);

        await handler.Pull(discord.Context, match.SeasonMatchID.ToString(), backToSeasonStart: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.False, "only the caller sees it");
            Assert.That(discord.Content(), Does.Contain("is queued").And.Contain($"https://grif.test/seasonmatch/{match.SeasonMatchID}"));
        }
        await _seasonPulls.Received(1).Queue(match.SeasonMatchID, caller.Id, true, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PlayerPull_QueuesTheCallersOwnAccount_WhenNoGamertagIsGiven()
    {
        var caller = await AddUser(5, "Puller", "Player");
        _accountPulls.Queue(5, caller.Id, Arg.Any<CancellationToken>()).Returns(new OnDemandJobStatusDto { State = OnDemandJobState.Running });
        var handler = new PlayerCommandsHandler(new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time), new DiscordCaller(Db), null!, null!, null!, null!, Urls(), Pulls());
        var discord = new CommandContextRecorder(discordUserId: 5);

        await handler.Pull(discord.Context, null);

        Assert.That(discord.Content(), Does.Contain("The pull of Puller's recent games is running").And.Contain("https://grif.test/xbox/5"));
        await _accountPulls.Received(1).Queue(5, caller.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThePullCommands_RefuseACallerWithoutTheRole_AndAnAccountTheSiteDoesntKnow()
    {
        await AddUser(5, "NoRole");
        await AddUser(6, "Puller", "Player");
        var handler = new PlayerCommandsHandler(new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time), new DiscordCaller(Db), null!, null!, null!, null!, Urls(), Pulls());
        var noRole = new CommandContextRecorder(discordUserId: 5);
        var unknown = new CommandContextRecorder(discordUserId: 6);

        await handler.Pull(noRole.Context, null);
        await handler.Pull(unknown.Context, "Nobody");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noRole.Content(), Is.EqualTo(DiscordCaller.MissingRole(GrifTools.RolesByName["pull_xbox_account_games"])));
            Assert.That(unknown.Content(), Is.EqualTo(CommandLookups.NoSuchXboxAccount));
        }
        await _accountPulls.DidNotReceiveWithAnyArgs().Queue(default, default);
    }

    [Test]
    public async Task PlayerPull_FindsNoAccount_ByAPastGamertagTyped_ButPullsIt_PickedFromTheList()
    {
        var caller = await AddUser(6, "Puller", "Player");
        var renamed = XboxUser.FromXbox(2001, "New Name");
        renamed.RecordPastGamertag("Old Name", null, null, GamertagHistorySource.Manual);
        Db.XboxUsers.Add(renamed);
        await Db.SaveChangesAsync();
        _accountPulls.Queue(2001, caller.Id, Arg.Any<CancellationToken>()).Returns(new OnDemandJobStatusDto { State = OnDemandJobState.Queued });
        var handler = new PlayerCommandsHandler(new CommandLookups(Db, Seasons(), new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db), _time), new DiscordCaller(Db), null!, null!, null!, null!, Urls(), Pulls());
        var typed = new CommandContextRecorder(discordUserId: 6);
        var picked = new CommandContextRecorder(discordUserId: 6);

        await handler.Pull(typed.Context, "Old Name");
        await handler.Pull(picked.Context, CommandLookups.ChoiceValue(2001L));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(typed.Content(), Is.EqualTo(CommandLookups.NoSuchXboxAccount));
            Assert.That(picked.Content(), Does.Contain("The pull of New Name's recent games is queued").And.Contain("https://grif.test/xbox/2001"));
        }
        await _accountPulls.Received(1).Queue(2001, caller.Id, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MatchPull_RefusesACallerWithoutTheRole()
    {
        await AddUser(5, "NoRole");
        var handler = new MatchCommandsHandler(new CommandLookups(Db, Seasons(), new XboxAccountService(Db), _time), SeasonMatches(), new MatchDetailsService(Db), Urls(), new DiscordCaller(Db), Pulls());
        var discord = new CommandContextRecorder(discordUserId: 5);

        await handler.Pull(discord.Context, "1", backToSeasonStart: false);

        Assert.That(discord.Content(), Is.EqualTo(DiscordCaller.MissingRole(GrifTools.RolesByName["pull_season_match_games"])));
        await _seasonPulls.DidNotReceiveWithAnyArgs().Queue(default, default, default);
    }

    [Test]
    public async Task MatchPull_SaysSo_ForAMatchItCantFind()
    {
        await AddUser(5, "Puller", "Player");
        var handler = new MatchCommandsHandler(new CommandLookups(Db, Seasons(), new XboxAccountService(Db), _time), SeasonMatches(), new MatchDetailsService(Db), Urls(), new DiscordCaller(Db), Pulls());
        var discord = new CommandContextRecorder(discordUserId: 5);

        await handler.Pull(discord.Context, "Nobody", backToSeasonStart: false);

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchMatch));
        await _seasonPulls.DidNotReceiveWithAnyArgs().Queue(default, default, default);
    }

    [Test]
    public async Task PlayerPull_RefusesAPickedAccountTheSiteDoesntKnow()
    {
        await AddUser(6, "Puller", "Player");
        var handler = new PlayerCommandsHandler(new CommandLookups(Db, Seasons(), new XboxAccountService(Db), _time), new DiscordCaller(Db), null!, null!, null!, null!, Urls(), Pulls());
        var discord = new CommandContextRecorder(discordUserId: 6);

        await handler.Pull(discord.Context, CommandLookups.ChoiceValue(999L));

        Assert.That(discord.Content(), Is.EqualTo(CommandLookups.NoSuchXboxAccount));
        await _accountPulls.DidNotReceiveWithAnyArgs().Queue(default, default);
    }

    [Test]
    public async Task ThePullByXuid_RefusesAnAccountTheSiteDoesntKnow()
    {
        var caller = await AddUser(6, "Puller", "Player");
        var tools = Pulls().As(new CallerUser(caller.Id, 6, "Puller", ["Player"]));

        var refused = await Assert.ThrowsAsync<ToolException>(() => tools.PullXboxAccountGamesOf(999, "Nobody"));

        Assert.That(refused!.Message, Does.Contain("knows no Xbox account 999"));
        await _accountPulls.DidNotReceiveWithAnyArgs().Queue(default, default);
    }

    [TestCase(OnDemandJobState.Queued, "is queued: it starts once any Halo pull ahead of it finishes")]
    [TestCase(OnDemandJobState.Running, "is running")]
    [TestCase(OnDemandJobState.Idle, "has finished")]
    public void APullsReply_SaysWhereItIs(OnDemandJobState state, string expected)
        => Assert.That(PullReplies.State(new OnDemandJobStatusDto { State = state }), Is.EqualTo(expected));

    [Test]
    public async Task TheGamertagList_WithNothingTyped_ListsAccountsByTheirCurrentGamertag()
    {
        var renamed = XboxUser.FromXbox(2001, "New Name");
        renamed.RecordPastGamertag("Old Name", null, null, GamertagHistorySource.Manual);
        Db.XboxUsers.Add(renamed);
        await Db.SaveChangesAsync();

        var choices = await new CommandLookups(Db, Seasons(), new XboxAccountService(Db), _time).XboxAccountChoices(null);

        Assert.That(choices.Select(c => (c.Name, c.StringValue)), Is.EqualTo(new[] { ("New Name", "id:2001") }));
    }
}
