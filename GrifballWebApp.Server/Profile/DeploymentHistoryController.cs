using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Profile;

/// <summary>A profile's deployment history (the seasons and teams a player played for).</summary>
[Route("[controller]/[action]")]
[ApiController]
public class DeploymentHistoryController : ControllerBase
{
    private readonly DeploymentHistoryService _history;

    public DeploymentHistoryController(DeploymentHistoryService history)
    {
        _history = history;
    }

    [HttpGet("{userID:int}", Name = "GetDeploymentHistory")]
    public async Task<ActionResult<List<DeploymentHistoryDto>>> GetDeploymentHistory([FromRoute] int userID, CancellationToken ct)
    {
        var history = await _history.GetAsync(userID, ct);
        if (history is null)
            return NotFound();
        return history;
    }
}
