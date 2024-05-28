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
    [HttpGet("{seasonMatchID:int}", Name = "HomeForfeit")]
    public async Task<IActionResult?> HomeForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        await _seasonMatchService.HomeForfeit(seasonMatchID, ct);

        return Ok();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpGet("{seasonMatchID:int}", Name = "AwayForfeit")]
    public async Task<IActionResult?> AwayForfeit([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        await _seasonMatchService.AwayForfeit(seasonMatchID, ct);

        return Ok();
    }

    // TODO: Dual forfeits gets complicated because then we gotta possibily walk up the bracket in multiple directions and make bye matches

    [HttpGet("{seasonMatchID:int}", Name = "GetPossibleMatches")]
    public Task<List<PossibleMatchDto>> GetPossibleMatches([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return _seasonMatchService.GetPossibleMatches(seasonMatchID, ct);
    }
}
