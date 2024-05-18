using GrifballWebApp.Server.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Brackets;

[Route("[controller]/[action]")]
[ApiController]
public class BracketsController : ControllerBase
{
    private readonly ILogger<BracketsController> _logger;
    private readonly BracketService _bracketService;

    public BracketsController(ILogger<BracketsController> logger, BracketService bracketService)
    {
        _logger = logger;
        _bracketService = bracketService;
    }

    [Authorize(Roles = "Commissioner")]
    [HttpGet(Name = "CreateBracket")]
    public async Task<ActionResult> CreateBracket([FromQuery] int participantsCount, [FromQuery] int seasonID, [FromQuery] bool doubleElimination)
    {
        if (participantsCount <= 0)
            return BadRequest("Please provide participantsCount");
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        await _bracketService.CreateBracket(participantsCount, seasonID, doubleElimination);
        return Ok();
    }

    [HttpGet(Name = "GetBracket")]
    public async Task<ActionResult<BracketDto>> GetBracket([FromQuery] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var bracketDto = await _bracketService.GetBracketsAsync(seasonID);
        return Ok(bracketDto);
    }

    [HttpGet(Name = "GetViewerData")]
    public async Task<ActionResult<ViewerDataDto>> GetViewerData([FromQuery] int seasonID)
    {
        if (seasonID <= 0)
            return BadRequest("Please provide seasonID");

        var bracketDto = await _bracketService.GetViewerDataAsync(seasonID);
        return Ok(bracketDto);
    }
}
