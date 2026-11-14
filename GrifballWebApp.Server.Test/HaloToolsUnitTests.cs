using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// The Halo tools on a temp-file SQLite database: get_recent_games (<see cref="HaloTools"/>) reads the saved games, open to
/// anyone; pull_season_match_games (<see cref="HaloPullTools"/>) queues the season match page's pull, for the roles it allows.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class HaloToolsUnitTests : SqliteGrifballContextFixture
{
    private static readonly Guid MatchID = Guid.Parse("6f0c1c7e-1a52-4c11-9d7e-3f1c2b9a0a01");
    private static readonly string[] BothPlayers = ["Grunt Padre", "Other"];
    private static readonly string[] PlayerRole = ["Player"];

    private readonly ISeasonMatchPulls _pulls = Substitute.For<ISeasonMatchPulls>();

    private HaloTools Tools() => new(Db);

    private HaloPullTools PullTools(ToolCaller caller) => new(caller,
        new SeasonMatchService(Db, Substitute.For<IDataPullService>(), new BracketService(Db, new TeamStandingsService(Db), new ChampionshipService(Db)), new ChampionshipService(Db)),
        _pulls);

    private async Task AddGames()
    {
        Db.XboxUsers.AddRange(XboxUser.FromXbox(77, "Grunt Padre"), XboxUser.FromXbox(78, "Other"));
        var match = HaloMatch.Seed(MatchID, startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc));
        match.AddTeam(0, 50, Outcomes.Won);
        match.AddTeam(1, 20, Outcomes.Lost);
        match.AddParticipant(0, 77, new MatchParticipantStats());
        match.AddParticipant(1, 78, new MatchParticipantStats());
        Db.Matches.Add(match);
        // Someone else's game: not theirs
        var notTheirs = HaloMatch.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc));
        notTheirs.AddTeam(0, 1, Outcomes.Won);
        notTheirs.AddParticipant(0, 78, new MatchParticipantStats());
        Db.Matches.Add(notTheirs);
        await Db.SaveChangesAsync();
    }

    [Test]
    public async Task GetRecentGames_GivesThePlayersSavedGames_WithWaypointLinks()
    {
        await AddGames();

        var games = await Tools().GetRecentGames("  Grunt Padre ", 3);

        var game = games.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.MatchID, Is.EqualTo(MatchID));
            Assert.That(game.WaypointUrl, Is.EqualTo($"https://www.halowaypoint.com/halo-infinite/players/Grunt%20Padre/matches/{MatchID}"));
            Assert.That(game.Teams.SelectMany(t => t.Players), Is.EquivalentTo(BothPlayers));
            Assert.That(game.Teams.Single(t => t.TeamID == 0).Outcome, Is.EqualTo(Outcomes.Won));
        }
    }

    [Test]
    public async Task GetRecentGames_RefusesAGamertagTheSiteDoesntKnow()
    {
        // Read only: an unknown gamertag isn't looked up in Halo (nor saved)
        var refused = await Assert.ThrowsAsync<ToolException>(() => Tools().GetRecentGames("Nobody"));

        Assert.That(refused!.Message, Does.Contain("no games saved for the gamertag Nobody"));
    }

    [Test]
    public async Task GetRecentGames_ForAGamertagTwoAccountsHave_IsTheOneThatPlayedLast()
    {
        // A stale row still has the gamertag its account renamed away from; the account that played last holds it now
        Db.XboxUsers.AddRange(XboxUser.FromXbox(80, "Shared"), XboxUser.FromXbox(81, "Shared"));
        var old = HaloMatch.Seed(Guid.NewGuid(), startTime: new DateTime(2025, 1, 1, 20, 0, 0, DateTimeKind.Utc));
        old.AddTeam(0, 1, Outcomes.Won);
        old.AddParticipant(0, 80, new MatchParticipantStats());
        var recent = HaloMatch.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc));
        recent.AddTeam(0, 1, Outcomes.Won);
        recent.AddParticipant(0, 81, new MatchParticipantStats());
        Db.Matches.AddRange(old, recent);
        await Db.SaveChangesAsync();

        var games = await Tools().GetRecentGames("Shared");

        Assert.That(games.Single().MatchID, Is.EqualTo(recent.MatchID));
    }

    [Test]
    public async Task GetRecentGames_FindsAPlayerByAPastGamertag()
    {
        await AddGames();
        var renamed = await Db.XboxUsers.SingleAsync(x => x.XboxUserID == 77);
        renamed.RecordPastGamertag("Old Padre", null, null, GamertagHistorySource.RenameDetected);
        await Db.SaveChangesAsync();

        var games = await Tools().GetRecentGames("old padre");

        Assert.That(games.Single().MatchID, Is.EqualTo(MatchID));
    }

    [Test]
    public void PullSeasonMatchGames_IsForTheRolesTheSitesPullIs()
    {
        // One list: the tool's [ToolRoles], its own check and the season match endpoint's [Authorize]
        var endpoint = typeof(GrifballWebApp.Server.SeasonMatchPage.SeasonMatchController).GetMethod(nameof(GrifballWebApp.Server.SeasonMatchPage.SeasonMatchController.PullMatches))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Roles!;

        Assert.That(GrifTools.RolesByName["pull_season_match_games"], Is.EquivalentTo(endpoint.Split(',')));
    }

    [TestCase(0)]
    [TestCase(100)]
    public async Task GetRecentGames_KeepsTheCountInRange(int asked)
    {
        await AddGames();

        Assert.That(await Tools().GetRecentGames("Grunt Padre", asked), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task PullSeasonMatchGames_QueuesTheMatchesPull_AsThePlayerWhoAsked()
    {
        var season = Season.Seed(seasonName: "Spring");
        Db.Seasons.Add(season);
        await Db.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        Db.Teams.AddRange(home, away);
        await Db.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
        Db.SeasonMatches.Add(match);
        await Db.SaveChangesAsync();
        var queued = new OnDemandJobStatusDto { State = OnDemandJobState.Queued };
        _pulls.Queue(match.SeasonMatchID, 21, true, Arg.Any<CancellationToken>()).Returns(queued);

        var status = await PullTools(new ToolCaller(21, PlayerRole)).PullSeasonMatchGames(match.SeasonMatchID, backToSeasonStart: true);

        Assert.That(status, Is.SameAs(queued));
    }

    [Test]
    public async Task PullSeasonMatchGames_RefusesAnUnknownMatch_AndACallerWithoutTheRole()
    {
        var unknown = await Assert.ThrowsAsync<ToolException>(() => PullTools(new ToolCaller(21, PlayerRole)).PullSeasonMatchGames(4242));
        var anonymous = await Assert.ThrowsAsync<ToolException>(() => PullTools(ToolCaller.Anonymous).PullSeasonMatchGames(4242));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unknown!.Message, Does.Contain("no season match 4242"));
            Assert.That(anonymous!.Message, Is.EqualTo("Only a player or commissioner or sysadmin can do that."));
        }
        await _pulls.DidNotReceiveWithAnyArgs().Queue(default, default, default);
    }
}
