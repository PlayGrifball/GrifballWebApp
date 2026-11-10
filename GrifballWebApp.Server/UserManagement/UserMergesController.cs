using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Server.UserManagement;

/// <summary>The audit trail of user merges, and undoing one: sysadmin only, like merging.</summary>
[Authorize(Roles = "Sysadmin")]
[Route("[controller]/[action]")]
[ApiController]
public class UserMergesController : ControllerBase
{
    private readonly UserMergeAuditService _merges;

    public UserMergesController(UserMergeAuditService merges)
    {
        _merges = merges;
    }

    /// <summary>Merges, newest first.</summary>
    [HttpGet(Name = "GetUserMerges")]
    public Task<PaginationResult<UserMergeDto>> List([FromQuery] PaginationFilter filter, CancellationToken ct)
        => _merges.List(filter, ct);

    [HttpGet("{mergeId:int}", Name = "GetUserMerge")]
    public async Task<ActionResult<UserMergeDetailDto>> Get([FromRoute] int mergeId, CancellationToken ct)
        => await _merges.Get(mergeId, ct) is { } merge ? merge : NotFound();

    /// <summary>Puts back what the merge changed. 409 with the reason when it can't be done.</summary>
    [HttpPost("{mergeId:int}", Name = "UndoUserMerge")]
    public async Task<IActionResult> Undo([FromRoute] int mergeId, [FromBody] UndoMergeRequest? request, CancellationToken ct)
    {
        await _merges.Undo(mergeId, request?.Note, ct);
        return NoContent();
    }
}
