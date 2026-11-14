using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Options;
using NSubstitute;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// /player history, and /match game, with something to show: on a temp-file SQLite GrifballContext with the
/// real profile and match services; the Xbox lookup and the Halo pull substituted.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class PlayerAndMatchCommandsActionsUnitTests : SqliteGrifballContextFixture
{
    private PlayerCommandsHandler Players() => new(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)), new DiscordCaller(Db),
        new ProfileStatsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db)),
        new ProfileCommendationsService(Db),
        new DeploymentHistoryService(Db),
        new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!),
        Urls(), null!);

    [Test]
    public async Task History_ListsTheTeamsThePlayerWasOn()
    {
        var player = await AddUser(5, "Grifter1");
        var season = await AddSeason("Winter");
        var team = await AddTeam(season, "Grifs");
        Db.Set<TeamPlayer>().Add(TeamPlayer.Seed(teamID: team.TeamID, userID: player.Id));
        await Db.SaveChangesAsync();
        var discord = new CommandContextRecorder();

        await Players().History(discord.Context, "Grifter1");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Grifter1 deployment history"));
            Assert.That(embed.Description, Does.StartWith($"Winter · [Grifs](https://grif.test/season/{season.SeasonID}/team/{team.TeamID}) · 0-0 · "));
        }
    }

    [Test]
    public async Task Game_ListsEachTeamsScoreAndPlayers()
    {
        Db.XboxUsers.AddRange(XboxUser.FromXbox(1, "Alpha"), XboxUser.FromXbox(2, "Bravo"));
        var matchID = Guid.Parse("00000000-0000-0000-0000-0000000000b1");
        var game = HaloMatch.Seed(matchID, startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc), duration: TimeSpan.FromMinutes(9));
        game.AddTeam(0, 50, Outcomes.Won);
        game.AddTeam(1, 20, Outcomes.Lost);
        game.AddParticipant(0, 1, new MatchParticipantStats { Kills = 10, Deaths = 2, Assists = 3 });
        game.AddParticipant(1, 2, new MatchParticipantStats { Kills = 4, Deaths = 9, Assists = 1 });
        Db.Matches.Add(game);
        await Db.SaveChangesAsync();
        var handler = new MatchCommandsHandler(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)),
            new SeasonMatchService(Db, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(Db)), new MatchDetailsService(Db), Urls(), null!, null!);
        var discord = new CommandContextRecorder();

        await handler.Game(discord.Context, matchID.ToString());

        var fields = discord.Embed().Fields!.Skip(2).Select(f => (f.Name, f.Value)).ToList();
        Assert.That(fields, Is.EqualTo(new[]
        {
            ("Team 0: 50 (Won)", "Alpha · 0 goals · 10/2/3"),
            ("Team 1: 20 (Lost)", "Bravo · 0 goals · 4/9/1"),
        }));
    }

    [Test]
    public async Task Info_ListsTheMatchsReportedGames_WithALinkToEach()
    {
        var season = await AddSeason("Winter");
        var match = await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
        var gameID = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
        var game = HaloMatch.Seed(gameID, startTime: new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc));
        game.AddTeam(0, 3, Outcomes.Won);
        game.AddTeam(1, 1, Outcomes.Lost);
        Db.Matches.Add(game);
        match.AddGame(game, 1);
        await Db.SaveChangesAsync();
        var handler = new MatchCommandsHandler(new CommandLookups(Db, null!, new GrifballWebApp.Application.XboxAccounts.XboxAccountService(Db)),
            new SeasonMatchService(Db, Substitute.For<IDataPullService>(), Substitute.For<IBracketService>(), new ChampionshipService(Db)), new MatchDetailsService(Db), Urls(), null!, null!);
        var discord = new CommandContextRecorder();

        await handler.Info(discord.Context, match.SeasonMatchID.ToString());

        var games = discord.Embed().Fields!.Single(f => f.Name == "Games").Value;
        Assert.That(games, Does.StartWith("Game 1: ").And.EndWith($" · [details](https://grif.test/match/{gameID})"));
    }
}
