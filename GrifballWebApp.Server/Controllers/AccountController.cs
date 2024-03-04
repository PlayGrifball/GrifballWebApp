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

        var jwt = await _accountService.Login(username: loginDto.Username, password: loginDto.Password, ct);

        return Ok(jwt);
    }

    [HttpPost(Name = "Register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto registerDto, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(registerDto?.Username))
            return BadRequest("Username is required");

        if (string.IsNullOrEmpty(registerDto?.Password))
            return BadRequest("Password is required");

        if (string.IsNullOrEmpty(registerDto?.Gamertag))
            return BadRequest("Gamertag is required");

        await _accountService.Register(username: registerDto.Username, password: registerDto.Password, gamertag: registerDto.Gamertag, ct);

        return Ok();
    }
}
