using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.Profile;

/// <summary>A profile's deployment history (the seasons and teams a player played for).</summary>
[ApiController]
public class DeploymentHistoryController : ControllerBase
{
    private readonly DeploymentHistoryService _history;

    public DeploymentHistoryController(DeploymentHistoryService history)
    {
        _history = history;
    }

    /// <summary>A player's deployment history: the seasons and teams they played for.</summary>
    [AllowAnonymous]
    [HttpGet("users/{userID:int}/deployment-history", Name = "GetDeploymentHistory")]
    [ProducesResponseType<List<DeploymentHistoryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<List<DeploymentHistoryDto>>> GetDeploymentHistory([FromRoute] int userID, CancellationToken ct)
    {
        var history = await _history.GetAsync(userID, ct);
        if (history is null)
            return this.NotFoundProblem("There is no such user");
        return history;
    }
}
