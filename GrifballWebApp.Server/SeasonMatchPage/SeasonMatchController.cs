using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;

namespace GrifballWebApp.Server.SeasonMatchPage;

/// <summary>
/// A season match's page and its results: the games reported from Halo Infinite or entered by hand, forfeits, undoing a
/// result, and the pull of the players' recent games that the reporting picks from.
/// </summary>
[ApiController]
public class SeasonMatchController : ControllerBase
{
    private const string ProblemJson = "application/problem+json";

    private readonly SeasonMatchService _seasonMatchService;

    public SeasonMatchController(SeasonMatchService seasonMatchService)
    {
        _seasonMatchService = seasonMatchService;
    }

    /// <summary>A season match's page: its games, results and whether the signed-in user may ask to reschedule it.</summary>
    [AllowAnonymous]
    [HttpGet("season-matches/{seasonMatchID:int}", Name = "GetSeasonMatchPage")]
    [ProducesResponseType<SeasonMatchPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<SeasonMatchPageDto>> GetSeasonMatchPage([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        // Signed in: whether this user may ask to reschedule the match. A visitor gets the page without.
        int? userID = int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        return await _seasonMatchService.GetSeasonMatchPage(seasonMatchID, userID, ct) is { } page ? page : this.NotFoundProblem("There is no such season match");
    }

    /// <summary>Reports a Halo Infinite match as a game of the series (replacing one, when given).</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/games/{matchID:guid}/report", Name = "ReportMatch")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<IActionResult> ReportMatch([FromRoute] int seasonMatchID, [FromRoute] Guid matchID, CancellationToken ct, [FromQuery] int? replaces = null)
    {
        if (matchID == default)
            return this.BadRequestProblem("Provide valid Guid");

        return await Run(() => _seasonMatchService.ReportMatch(seasonMatchID, matchID, replaces, ct));
    }

    /// <summary>Forfeits the match for the home team. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/forfeit/home", Name = "HomeForfeit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IActionResult> HomeForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.HomeForfeit(seasonMatchID, ct));
    }

    /// <summary>Forfeits the match for the away team. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/forfeit/away", Name = "AwayForfeit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IActionResult> AwayForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.AwayForfeit(seasonMatchID, ct));
    }

    /// <summary>Forfeits the match for both teams. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/forfeit/double", Name = "DoubleForfeit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IActionResult> DoubleForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.DoubleForfeit(seasonMatchID, ct));
    }

    /// <summary>Undoes a match's recorded result (a forfeit or a finished series), leaving it to be decided again. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/result/undo", Name = "UndoResult")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IActionResult> UndoResult([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.UndoResult(seasonMatchID, ct));
    }

    /// <summary>Adds a game entered by hand to the series: a 201 with its recorded game's ID, at the match's page.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/manual-games", Name = "AddManualGame")]
    [ProducesResponseType<int>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public async Task<ActionResult<int>> AddManualGame([FromRoute] int seasonMatchID, [FromBody] ManualGameRequestDto dto, CancellationToken ct)
    {
        try
        {
            var recordedGameID = await _seasonMatchService.AddManualGame(seasonMatchID, dto, ct);
            return CreatedAtAction(nameof(GetSeasonMatchPage), new { seasonMatchID }, recordedGameID);
        }
        catch (SeasonMatchActionNotAllowedException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }
    }

    /// <summary>Edits a game entered by hand. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/manual-games/{recordedGameID:int}", Name = "EditManualGame")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IActionResult> EditManualGame([FromRoute] int seasonMatchID, [FromRoute] int recordedGameID, [FromBody] ManualGameRequestDto dto, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.EditManualGame(seasonMatchID, recordedGameID, dto, ct));
    }

    /// <summary>Removes a game from the series. Commissioners only.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("season-matches/{seasonMatchID:int}/games/{seriesGameID:int}/remove", Name = "RemoveGame")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    public Task<IActionResult> RemoveGame([FromRoute] int seasonMatchID, [FromRoute] int seriesGameID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.RemoveGame(seasonMatchID, seriesGameID, ct));
    }

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (SeasonMatchActionNotAllowedException ex)
        {
            return this.BadRequestProblem(ex.Message);
        }
        return NoContent();
    }

    /// <summary>The players' recent Halo Infinite games that could be this match's (from the last pull).</summary>
    [AllowAnonymous]
    [HttpGet("season-matches/{seasonMatchID:int}/possible-matches", Name = "GetPossibleMatches")]
    [ProducesResponseType<List<PossibleMatchDto>>(StatusCodes.Status200OK)]
    public Task<List<PossibleMatchDto>> GetPossibleMatches([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return _seasonMatchService.GetPossibleMatches(seasonMatchID, ct);
    }

    /// <summary>Where the pull of the players' recent games is: queued, running with its progress, or idle with its last run.</summary>
    [AllowAnonymous]
    [HttpGet("season-matches/{seasonMatchID:int}/match-pull", Name = "GetMatchPullStatus")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status200OK)]
    public Task<OnDemandJobStatusDto> MatchPullStatus([FromRoute] int seasonMatchID, [FromServices] OnDemandJobs jobs, CancellationToken ct)
        => jobs.Status<SeasonMatchPullJob>(SeasonMatchPullJob.RunName(seasonMatchID), ct);

    /// <summary>Queues a pull of the players' recent games (back to the season's start, when asked): a 202 with where it is.</summary>
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [HttpPost("season-matches/{seasonMatchID:int}/match-pull", Name = "PullMatches")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<OnDemandJobStatusDto>> PullMatches([FromRoute] int seasonMatchID, [FromServices] OnDemandJobs jobs, CancellationToken ct,
        [FromQuery] bool backToSeasonStart = false)
    {
        if (!await _seasonMatchService.Exists(seasonMatchID, ct))
            return this.NotFoundProblem("There is no such season match");
        return Accepted(await jobs.Enqueue<SeasonMatchPullJob, SeasonMatchPull>(SeasonMatchPullJob.RunName(seasonMatchID), new SeasonMatchPull(seasonMatchID, JobUser.IdOf(User), backToSeasonStart),
            backToSeasonStart ? $"Season match {seasonMatchID}, back to its season's start" : $"Season match {seasonMatchID}", JobsServiceCollectionExtensions.HaloMatchPulls, ct));
    }
}
