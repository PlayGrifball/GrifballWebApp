using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.UserManagement;
using GrifballWebApp.Application.Dtos;

namespace GrifballWebApp.Server.UserManagement;

[Authorize(Roles = "Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
[ApiController]
public class UserManagementController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly IUserMergeService _userMergeService;

    public UserManagementController(IUserManagementService userManagementService, IUserMergeService userMergeService)
    {
        _userManagementService = userManagementService;
        _userMergeService = userMergeService;
    }

    /// <summary>Users, filtered, sorted and paged. Sysadmins only.</summary>
    [HttpGet("users", Name = "GetUsers")]
    [ProducesResponseType<PaginationResult<UserResponseDto>>(StatusCodes.Status200OK)]
    public Task<PaginationResult<UserResponseDto>> GetUsers([FromQuery] PaginationFilter filter, [FromQuery] string? search, CancellationToken ct)
    {
        search = search?.Trim() ?? "";
        return _userManagementService.GetUsers(filter, search, ct);
    }

    /// <summary>A user's account details. Sysadmins only.</summary>
    [HttpGet("users/{userID:int}", Name = "GetUser")]
    [ProducesResponseType<UserResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserResponseDto>> GetUser([FromRoute] int userID, CancellationToken ct)
    {
        return await _userManagementService.GetUser(userID, ct) is { } user ? user : this.NotFoundProblem("There is no such user");
    }

    /// <summary>Creates a user (204: the service answers no ID to point a Location at).</summary>
    [HttpPost("users", Name = "CreateUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto createUserRequestDto, CancellationToken ct)
    {
        var result = await _userManagementService.CreateUser(createUserRequestDto, ct);
        if (result is null)
            return NoContent();
        else
            return this.BadRequestProblem(result);
    }

    /// <summary>Edits a user's account details. Sysadmins only.</summary>
    [HttpPost("users/{userID:int}", Name = "EditUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> EditUser([FromRoute] int userID, [FromBody] UserResponseDto editUser, CancellationToken ct)
    {
        // The route names the user; the body's ID is ignored
        editUser.UserID = userID;
        var result = await _userManagementService.EditUser(editUser, ct);
        if (result is null)
            return NoContent();
        else
            return this.BadRequestProblem(result);
    }

    /// <summary>Merges one user into another, moving the source user's data onto the target and deleting it. Sysadmins only.</summary>
    [HttpPost("user-merges", Name = "MergeUser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> MergeUser([FromBody] MergeRequest mergeRequest, CancellationToken ct)
    {
        try
        {
            await _userMergeService.Merge(mergeRequest.MergeToId, mergeRequest.MergeFromId, mergeRequest.MergeOptions.ToOptions(), ct);
        }
        catch (UserNotFoundException e)
        {
            // Typically a repeated request: the first merge already deleted the source user.
            return this.NotFoundProblem($"{e.Message}. It may already have been merged or deleted.");
        }
        catch (ArgumentException e)
        {
            return this.BadRequestProblem(e.Message);
        }
        catch (MergeConflictException e)
        {
            return this.ConflictProblem(e.Message);
        }
        catch (DbUpdateException)
        {
            // Data the merge rules don't cover: nothing was changed (the transaction is not committed).
            return this.ConflictProblem("The users could not be merged because their data conflicts. Nothing was changed.");
        }
        return NoContent();
    }
}


public class MergeRequest
{
    public required int MergeToId { get; set; }
    public required int MergeFromId { get; set; }
    public required MergeTransfers MergeOptions { get; set; }
}

/// <summary>
/// What the merge page chooses to move (<see cref="MergeOptions"/>'s transfers). The rest is the server's: the merge is a
/// manual one by the signed-in sysadmin, and season stats and championships always move.
/// </summary>
public class MergeTransfers
{
    public required bool TransferDiscord { get; set; }
    public required bool TransferLogins { get; set; }
    public required bool TransferXbox { get; set; }
    public required bool TransferTeamPlayers { get; set; }
    public required bool TransferExperiences { get; set; }
    public required bool TransferSignups { get; set; }
    public required bool DeleteMergeFrom { get; set; }

    /// <summary>The options of a manual merge with these transfers (who did it: the signed-in user).</summary>
    public MergeOptions ToOptions() => new()
    {
        TransferDiscord = TransferDiscord,
        TransferLogins = TransferLogins,
        TransferXbox = TransferXbox,
        TransferTeamPlayers = TransferTeamPlayers,
        TransferExperiences = TransferExperiences,
        TransferSignups = TransferSignups,
        DeleteMergeFrom = DeleteMergeFrom,
        Trigger = Domain.Players.MergeTrigger.Manual,
        TriggeredByUserID = null,
    };
}