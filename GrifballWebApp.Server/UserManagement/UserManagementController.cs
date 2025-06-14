using GrifballWebApp.Server.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.UserManagement;

[Authorize(Roles = "Sysadmin")]
[Route("[controller]/[action]")]
[ApiController]
public class UserManagementController : ControllerBase
{
    private readonly UserManagementService _userManagementService;

    public UserManagementController(UserManagementService userManagementService)
    {
        _userManagementService = userManagementService;
    }

    [HttpGet(Name = "GetUsers")]
    public Task<PaginationResult<UserResponseDto>> GetUsers([FromQuery] PaginationFilter filter, CancellationToken ct)
    {
        return _userManagementService.GetUsers(filter, ct);
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
}
