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
}
