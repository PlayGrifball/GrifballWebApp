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
    public Task<List<UserResponseDto>> GetUsers(CancellationToken ct)
    {
        return _userManagementService.GetUsers(ct);
    }
}
