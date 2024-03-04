using GrifballWebApp.Database;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Controllers;

[Route("api/[controller]/[action]")]
[ApiController]
public class AccountController : ControllerBase
{
    private readonly ILogger<AccountController> _logger;
    private readonly AccountService _accountService;

    public AccountController(ILogger<AccountController> logger, AccountService accountService)
    {
        _logger = logger;
        _accountService = accountService;
    }

    [HttpPost(Name = "Login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(loginDto?.Username))
            return BadRequest("Username is required");

        if (string.IsNullOrEmpty(loginDto?.Password))
            return BadRequest("Password is required");

        await _accountService.Login(username: loginDto.Username, password: loginDto.Password, ct);

        return Ok();
    }

    [HttpPost(Name = "Register")]
    public async Task<IActionResult> Register([FromBody] LoginDto loginDto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(loginDto?.Username))
            return BadRequest("Username is required");

        if (string.IsNullOrEmpty(loginDto?.Password))
            return BadRequest("Password is required");

        await _accountService.Register(username: loginDto.Username, password: loginDto.Password, ct);

        return Ok();
    }
}
