using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using GrifballWebApp.Server.SeasonMatchPage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using HaloMatch = GrifballWebApp.Domain.Halo.Match;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SeasonMatchController's actions done, past the validation SeasonMatchControllerUnitTests covers: a regular season
/// match between two teams on a temp-file SQLite GrifballContext with the real SeasonMatchService (the Halo pull and the
/// bracket substituted), its forfeits and undo, the service's refusals as 400s, and its games' pull queued on an
/// in-memory Quartz (<see cref="InMemoryQuartz"/>).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchControllerActionsUnitTests : SqliteGrifballContextFixture
{
    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private InMemoryQuartz? _quartz;

    [TearDown]
    public async Task TearDownQuartz()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
    }

    private SeasonMatchController Controller() => new SeasonMatchController(
            new SeasonMatchService(Db, _dataPull, Substitute.For<IBracketService>(), new ChampionshipService(Db)))
        .WithUser("7", "commish", "Commissioner");

    private async Task<SeasonMatch> AMatch()
    {
        var season = await AddSeason("Winter");
        return await AddSeasonMatch(season, await AddTeam(season, "Home"), await AddTeam(season, "Away"));
    }

    private async Task<(SeasonMatchResult? Home, SeasonMatchResult? Away)> Results(int seasonMatchID)
    {
        Db.ChangeTracker.Clear();
        var match = await Db.SeasonMatches.SingleAsync(x => x.SeasonMatchID == seasonMatchID);
        return (match.HomeTeamResult, match.AwayTeamResult);
    }

    [Test]
    public async Task HomeForfeit_TheHomeTeamLoses()
    {
        var match = await AMatch();

        var result = await Controller().HomeForfeit(match.SeasonMatchID, CancellationToken.None);

        var results = await Results(match.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(results, Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Forfeit, (SeasonMatchResult?)SeasonMatchResult.Won)));
        }
    }

    [Test]
    public async Task AwayForfeit_TheAwayTeamLoses()
    {
        var match = await AMatch();

        var result = await Controller().AwayForfeit(match.SeasonMatchID, CancellationToken.None);

        var results = await Results(match.SeasonMatchID);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That(results, Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Won, (SeasonMatchResult?)SeasonMatchResult.Forfeit)));
        }
    }

    [Test]
    public async Task DoubleForfeit_BothLose_ThenUndoReopensIt()
    {
        var match = await AMatch();
        var controller = Controller();

        var forfeited = await controller.DoubleForfeit(match.SeasonMatchID, CancellationToken.None);
        var both = await Results(match.SeasonMatchID);
        var undone = await controller.UndoResult(match.SeasonMatchID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forfeited, Is.InstanceOf<NoContentResult>());
            Assert.That(undone, Is.InstanceOf<NoContentResult>());
            Assert.That(both, Is.EqualTo(((SeasonMatchResult?)SeasonMatchResult.Forfeit, (SeasonMatchResult?)SeasonMatchResult.Forfeit)));
            Assert.That(await Results(match.SeasonMatchID), Is.EqualTo(((SeasonMatchResult?)null, (SeasonMatchResult?)null)));
        }
    }

    [Test]
    public async Task UndoResult_WithNothingToUndo_IsABadRequestSayingSo()
    {
        var match = await AMatch();

        var result = await Controller().UndoResult(match.SeasonMatchID, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "This match has no result to undo");
    }

    [Test]
    public async Task ReportMatch_AGameNobodyWon_IsABadRequestSayingSo()
    {
        var match = await AMatch();
        var gameID = Guid.Parse("4b1e7c5e-0d7e-4a8e-9a4f-2f6d8e1c3b77");
        var game = HaloMatch.Seed(gameID);
        game.AddTeam(0, 3, GrifballWebApp.Domain.Halo.Outcomes.Tie);
        Db.Matches.Add(game);
        await Db.SaveChangesAsync();

        var result = await Controller().ReportMatch(match.SeasonMatchID, gameID, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "There must be a winner for the match you are reporting");
        await _dataPull.Received(1).GetAndSaveMatch(gameID);
    }

    [Test]
    public async Task AddManualGame_ToADecidedMatch_IsABadRequestSayingSo()
    {
        var match = await AMatch();
        var controller = Controller();
        await controller.HomeForfeit(match.SeasonMatchID, CancellationToken.None);

        var result = await controller.AddManualGame(match.SeasonMatchID, new ManualGameRequestDto { GameVersionID = 1, HomeTeamScore = 3, AwayTeamScore = 1 }, CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status400BadRequest, "Results for this match have already been decided: to correct a game, replace it");
    }

    [Test]
    public async Task EditManualGame_NotOneOfTheMatchsGames_IsABadRequestSayingSo()
    {
        var match = await AMatch();

        var result = await Controller().EditManualGame(match.SeasonMatchID, 999, new ManualGameRequestDto { GameVersionID = 1, HomeTeamScore = 3, AwayTeamScore = 1 }, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "That game is not one of this match's games");
    }

    [Test]
    public async Task RemoveGame_NotOneOfTheMatchsGames_IsABadRequestSayingSo()
    {
        var match = await AMatch();

        var result = await Controller().RemoveGame(match.SeasonMatchID, 999, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "That game is not one of this match's games");
    }

    [Test]
    public async Task PullMatches_QueuesThePull_ThenItsStatusIsQueued()
    {
        var match = await AMatch();
        _quartz = await InMemoryQuartz.Create();
        var controller = Controller();

        var idle = await controller.MatchPullStatus(match.SeasonMatchID, new SeasonMatchPulls(_quartz.OnDemand), CancellationToken.None);
        var pulled = await controller.PullMatches(match.SeasonMatchID, new SeasonMatchPulls(_quartz.OnDemand), CancellationToken.None, backToSeasonStart: true);
        var queued = await controller.MatchPullStatus(match.SeasonMatchID, new SeasonMatchPulls(_quartz.OnDemand), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(idle.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(((OnDemandJobStatusDto)((AcceptedResult)pulled.Result!).Value!).State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(queued.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(await _quartz.Scheduler.GetTriggerState(OnDemandJobs.TriggerKeyOf<SeasonMatchPullJob>(SeasonMatchPullJob.RunName(match.SeasonMatchID))),
                Is.EqualTo(Quartz.TriggerState.Normal));
        }
    }

    [Test]
    public async Task PullMatches_AnUnknownMatch_IsNotFound()
    {
        _quartz = await InMemoryQuartz.Create();

        var result = await Controller().PullMatches(999, new SeasonMatchPulls(_quartz.OnDemand), CancellationToken.None);

        result.Result.AssertProblem(StatusCodes.Status404NotFound, "There is no such season match");
    }
}
