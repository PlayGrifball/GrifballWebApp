using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.SeasonMatchPage;

namespace GrifballWebApp.Server.SeasonMatchPage;

[Route("[controller]/[action]")]
[ApiController]
public class SeasonMatchController : ControllerBase
{
    private readonly SeasonMatchService _seasonMatchService;

    public SeasonMatchController(SeasonMatchService seasonMatchService)
    {
        _seasonMatchService = seasonMatchService;
    }

    [AllowAnonymous]
    [HttpGet("{seasonMatchID:int}", Name = "GetSeasonMatchPage")]
    public Task<SeasonMatchPageDto?> GetSeasonMatchPage([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        // Signed in: whether this user may ask to reschedule the match. A visitor gets the page without.
        int? userID = int.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        return _seasonMatchService.GetSeasonMatchPage(seasonMatchID, userID, ct);
    }

    /// <summary>Reports a Waypoint game: a new game of the series, or replacing the game picked (<paramref name="replaces"/>, a SeriesGameID).</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}/{matchID:Guid}", Name = "ReportMatch")]
    public async Task<IActionResult?> ReportMatch([FromRoute] int seasonMatchID, [FromRoute] Guid matchID, CancellationToken ct, [FromQuery] int? replaces = null)
    {
        if (matchID == default)
            return BadRequest("Provide valid Guid");

        return await Run(() => _seasonMatchService.ReportMatch(seasonMatchID, matchID, replaces, ct));
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}", Name = "HomeForfeit")]
    public Task<IActionResult> HomeForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.HomeForfeit(seasonMatchID, ct));
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}", Name = "AwayForfeit")]
    public Task<IActionResult> AwayForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.AwayForfeit(seasonMatchID, ct));
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}", Name = "DoubleForfeit")]
    public Task<IActionResult> DoubleForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.DoubleForfeit(seasonMatchID, ct));
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}", Name = "UndoResult")]
    public Task<IActionResult> UndoResult([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.UndoResult(seasonMatchID, ct));
    }

    /// <summary>
    /// Enters a game of the series by hand (manual match entry): for a game the stat bot or Halo Infinite's stats
    /// missed, or one not played in Halo Infinite. Answers the new game's RecordedGameID.
    /// </summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}", Name = "AddManualGame")]
    public async Task<ActionResult<int>> AddManualGame([FromRoute] int seasonMatchID, [FromBody] ManualGameRequestDto dto, CancellationToken ct)
    {
        try
        {
            return await _seasonMatchService.AddManualGame(seasonMatchID, dto, ct);
        }
        catch (SeasonMatchActionNotAllowedException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>Replaces a game entered by hand.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}/{recordedGameID:int}", Name = "EditManualGame")]
    public Task<IActionResult> EditManualGame([FromRoute] int seasonMatchID, [FromRoute] int recordedGameID, [FromBody] ManualGameRequestDto dto, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.EditManualGame(seasonMatchID, recordedGameID, dto, ct));
    }

    /// <summary>Unreports one game of the series, at any point: a Waypoint game can be reported again, one entered by hand is deleted.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("{seasonMatchID:int}/{seriesGameID:int}", Name = "RemoveGame")]
    public Task<IActionResult> RemoveGame([FromRoute] int seasonMatchID, [FromRoute] int seriesGameID, CancellationToken ct)
    {
        return Run(() => _seasonMatchService.RemoveGame(seasonMatchID, seriesGameID, ct));
    }

    /// <summary>
    /// Runs a commissioner action, answering a rule it breaks with a 400.
    /// </summary>
    private async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (SeasonMatchActionNotAllowedException ex)
        {
            return BadRequest(ex.Message);
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("{seasonMatchID:int}", Name = "GetPossibleMatches")]
    public Task<List<PossibleMatchDto>> GetPossibleMatches([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return _seasonMatchService.GetPossibleMatches(seasonMatchID, ct);
    }

    /// <summary>Where the pull of this match's players' recent games from Halo Infinite is (<see cref="SeasonMatchPullJob"/>).</summary>
    [AllowAnonymous]
    [HttpGet("{seasonMatchID:int}", Name = "GetMatchPullStatus")]
    public Task<OnDemandJobStatusDto> MatchPullStatus([FromRoute] int seasonMatchID, [FromServices] OnDemandJobs jobs, CancellationToken ct)
        => jobs.Status<SeasonMatchPullJob>(SeasonMatchPullJob.RunName(seasonMatchID), ct);

    /// <summary>
    /// Queues a pull of this match's players' recent games from Halo Infinite, which the worker runs within a few seconds;
    /// nothing more while one is queued or running. <paramref name="backToSeasonStart"/>: back through their histories to the
    /// start of the match's season, not just the most recent 11 pages. 404 for a season match that isn't there.
    /// </summary>
    [Authorize(Roles = "Commissioner,Sysadmin")]
    [HttpPost("{seasonMatchID:int}", Name = "PullMatches")]
    public async Task<ActionResult<OnDemandJobStatusDto>> PullMatches([FromRoute] int seasonMatchID, [FromServices] OnDemandJobs jobs, CancellationToken ct,
        [FromQuery] bool backToSeasonStart = false)
    {
        if (!await _seasonMatchService.Exists(seasonMatchID, ct))
            return NotFound();
        return await jobs.Enqueue<SeasonMatchPullJob, SeasonMatchPull>(SeasonMatchPullJob.RunName(seasonMatchID), new SeasonMatchPull(seasonMatchID, JobUser.IdOf(User), backToSeasonStart),
            backToSeasonStart ? $"Season match {seasonMatchID}, back to its season's start" : $"Season match {seasonMatchID}", JobsServiceCollectionExtensions.HaloMatchPulls, ct);
    }
}
