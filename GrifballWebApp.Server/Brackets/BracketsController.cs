using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Dtos;

namespace GrifballWebApp.Server.Brackets;

/// <summary>A season's playoff bracket: made by the organizers, seeded, and shown with brackets-viewer.</summary>
[ApiController]
public class BracketsController : ControllerBase
{
    private readonly IBracketService _bracketService;

    public BracketsController(IBracketService bracketService)
    {
        _bracketService = bracketService;
    }

    /// <summary>Makes the season's bracket for this many teams, single or double elimination, best of this many.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/bracket", Name = "CreateBracket")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> CreateBracket([FromRoute] int seasonID, [FromQuery] int participantsCount, [FromQuery] bool doubleElimination, [FromQuery] int bestOf, CancellationToken ct)
    {
        if (participantsCount <= 0)
            return this.BadRequestProblem("Please provide participantsCount");

        await _bracketService.CreateBracket(participantsCount, seasonID, doubleElimination, bestOf, ct);
        return CreatedAtAction(nameof(GetBracket), new { seasonID }, null);
    }

    /// <summary>A season's bracket.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/bracket", Name = "GetBracket")]
    [ProducesResponseType<BracketDto>(StatusCodes.Status200OK)]
    public Task<BracketDto> GetBracket([FromRoute] int seasonID, CancellationToken ct)
    {
        return _bracketService.GetBracketsAsync(seasonID, ct);
    }

    /// <summary>The bracket as brackets-viewer draws it.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/bracket/viewer-data", Name = "GetViewerData")]
    [ProducesResponseType<ViewerDataDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ViewerDataDto>> GetViewerData([FromRoute] int seasonID, CancellationToken ct)
    {
        return await _bracketService.GetViewerDataAsync(seasonID, ct) is { } viewerData ? viewerData : this.NotFoundProblem("Season does not exist");
    }

    /// <summary>Seeds the bracket by the season's standings.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/bracket/seeds/default", Name = "SetSeeds")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> SetSeeds([FromRoute] int seasonID, CancellationToken ct)
    {
        await _bracketService.SetSeeds(seasonID, null, ct);
        return NoContent();
    }

    /// <summary>Seeds the bracket in the organizer's order.</summary>
    [Authorize(Roles = "Commissioner")]
    [HttpPost("seasons/{seasonID:int}/bracket/seeds", Name = "SetCustomSeeds")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<IActionResult> SetCustomSeeds([FromRoute] int seasonID, [FromBody] CustomSeedDto[] customSeeds, CancellationToken ct)
    {
        await _bracketService.SetSeeds(seasonID, customSeeds, ct);
        return NoContent();
    }
}
