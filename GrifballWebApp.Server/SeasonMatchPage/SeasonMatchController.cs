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

    [HttpGet("{seasonMatchID:int}/{matchID:Guid}", Name = "ReportMatch")]
    public async Task<IActionResult?> ReportMatch([FromRoute] int seasonMatchID, [FromRoute] Guid matchID, CancellationToken ct)
    {
        if (matchID == default)
            return BadRequest("Provide valid Guid");

        await _seasonMatchService.ReportMatch(seasonMatchID, matchID, ct);

        return Ok("Did thing");
        //return _seasonMatchService.ReportMatch(seasonMatchID, matchID, ct);
    }

    [HttpGet("{seasonMatchID:int}", Name = "GetPossibleMatches")]
    public Task<List<PossibleMatchDto>> GetPossibleMatches([FromRoute] int seasonMatchID, CancellationToken ct)
    {
        return _seasonMatchService.GetPossibleMatches(seasonMatchID, ct);
    }
}
