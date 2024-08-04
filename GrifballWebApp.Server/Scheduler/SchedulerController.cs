using GrifballWebApp.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace GrifballWebApp.Server.Scheduler;

[Route("[controller]/[action]")]
[ApiController]
public class SchedulerController : ControllerBase
{
    private readonly GrifballContext _context;
    private readonly ScheduleService _scheduleService;

    public SchedulerController(GrifballContext context, ScheduleService scheduleService)
    {
        _context = context;
        _scheduleService = scheduleService;
    }

    [HttpGet(Name = "GetTimeRecommendations")]
    public Task<List<SeasonMatchGene>> GetTimeRecommendations(CancellationToken ct)
    {
        return _scheduleService.GetTimeRecommendations(ct);
    }
}
