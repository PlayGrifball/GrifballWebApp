using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.UserManagement;

[Authorize(Roles = "Sysadmin")]
[Route("[controller]/[action]")]
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

    [HttpGet(Name = "GetUsers")]
    public Task<PaginationResult<UserResponseDto>> GetUsers([FromQuery] PaginationFilter filter, [FromQuery] string? search, CancellationToken ct)
    {
        search = search?.Trim() ?? "";
        return _userManagementService.GetUsers(filter, search, ct);
    }

    [HttpGet("{userID:int}", Name = "GetUser")]
    public Task<UserResponseDto?> GetUser([FromRoute] int userID, CancellationToken ct)
    {
        return _userManagementService.GetUser(userID, ct);
    }

    [HttpPost(Name = "CreateUser")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequestDto createUserRequestDto, CancellationToken ct)
    {
        var result = await _userManagementService.CreateUser(createUserRequestDto, ct);
        if (result is null)
            return Ok();
        else
            return BadRequest(result);
    }

    [HttpPost(Name = "EditUser")]
    public async Task<IActionResult> EditUser([FromBody] UserResponseDto editUser, CancellationToken ct)
    {
        var result = await _userManagementService.EditUser(editUser, ct);
        if (result is null)
            return Ok();
        else
            return BadRequest(result);
    }

    [HttpPost(Name = "MergeUser")]
    public async Task<IActionResult> MergeUser([FromBody] MergeRequest mergeRequest, CancellationToken ct)
    {
        await _userMergeService.Merge(mergeRequest.MergeToId, mergeRequest.MergeFromId, mergeRequest.MergeOptions, ct);
        return Ok();
    }
}


public class MergeRequest
{
    public required int MergeToId { get; set; }
    public required int MergeFromId { get; set; }
    public required MergeOptions MergeOptions { get; set; }
}