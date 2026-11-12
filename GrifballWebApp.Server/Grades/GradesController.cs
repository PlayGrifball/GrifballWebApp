using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Grades;

namespace GrifballWebApp.Server.Grades;

[ApiController]
public class GradesController : ControllerBase
{
    private readonly GradesService _gradesService;

    public GradesController(GradesService gradesService)
    {
        _gradesService = gradesService;
    }

    /// <summary>A season's player grades.</summary>
    [AllowAnonymous]
    [HttpGet("seasons/{seasonID:int}/grades", Name = "GetGrades")]
    [ProducesResponseType<GradesDto>(StatusCodes.Status200OK)]
    public Task<GradesDto> GetGrades([FromRoute] int seasonID, CancellationToken ct)
    {
        return _gradesService.GetGrades(seasonID, ct);
    }
}
