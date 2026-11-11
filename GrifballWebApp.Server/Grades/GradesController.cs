using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Grades;

namespace GrifballWebApp.Server.Grades;

[Route("[controller]/[action]")]
[ApiController]
public class GradesController : ControllerBase
{
    private readonly GradesService _gradesService;

    public GradesController(GradesService gradesService)
    {
        _gradesService = gradesService;
    }

    [AllowAnonymous]
    [HttpGet("{seasonID:int}", Name = "GetGrades")]
    public Task<GradesDto> GetGrades([FromRoute] int seasonID, CancellationToken ct)
    {
        return _gradesService.GetGrades(seasonID, ct);
    }
}
