using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.UserManagement;

/// <summary>The audit trail of user merges, and undoing one: sysadmin only, like merging.</summary>
[Authorize(Roles = "Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ApiController]
public class UserMergesController : ControllerBase
{
    private readonly UserMergeAuditService _merges;

    public UserMergesController(UserMergeAuditService merges)
    {
        _merges = merges;
    }

    /// <summary>Merges, newest first.</summary>
    [HttpGet("user-merges", Name = "GetUserMerges")]
    [ProducesResponseType<PaginationResult<UserMergeDto>>(StatusCodes.Status200OK)]
    public Task<PaginationResult<UserMergeDto>> List([FromQuery] PaginationFilter filter, CancellationToken ct)
        => _merges.List(filter, ct);

    /// <summary>A merge's detail: what it changed.</summary>
    [HttpGet("user-merges/{mergeID:int}", Name = "GetUserMerge")]
    [ProducesResponseType<UserMergeDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserMergeDetailDto>> Get([FromRoute] int mergeID, CancellationToken ct)
        => await _merges.Get(mergeID, ct) is { } merge ? merge : this.NotFoundProblem("There is no such merge");

    /// <summary>Puts back what the merge changed. 409 with the reason when it can't be done.</summary>
    [HttpPost("user-merges/{mergeID:int}/undo", Name = "UndoUserMerge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Undo([FromRoute] int mergeID, [FromBody] UndoMergeRequest? request, CancellationToken ct)
    {
        await _merges.Undo(mergeID, request?.Note, ct);
        return NoContent();
    }
}
