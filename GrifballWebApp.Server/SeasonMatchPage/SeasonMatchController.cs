using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [HttpGet("{seasonMatchID:int}", Name = "GetSeasonMatchPage")]
    public Task<SeasonMatchPageDto?> GetSeasonMatchPage([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return _seasonMatchService.GetSeasonMatchPage(seasonMatchID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpGet("{seasonMatchID:int}/{matchID:Guid}", Name = "ReportMatch")]
    public async Task<IActionResult?> ReportMatch([FromRoute] int seasonMatchID, [FromRoute] Guid matchID, CancellationToken ct)
    {
        if (matchID == default)
            return BadRequest("Provide valid Guid");

        await _seasonMatchService.ReportMatch(seasonMatchID, matchID, ct);

        return Ok();
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

    [HttpGet("{seasonMatchID:int}", Name = "GetPossibleMatches")]
    public Task<List<PossibleMatchDto>> GetPossibleMatches([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return _seasonMatchService.GetPossibleMatches(seasonMatchID, ct);
    }
}
