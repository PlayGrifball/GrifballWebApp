using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GrifballWebApp.Server.Signups;

[Route("[controller]/[action]")]
[ApiController]
public class SignupsController : ControllerBase
{
    private readonly SignupsService _signupsService;

    public SignupsController(SignupsService signupsService)
    {
        _signupsService = signupsService;
    }

    [HttpGet(Name = "GetSignupDateInfo")]
    public async Task<IActionResult> GetSignupDateInfo(CancellationToken ct)
    {
        int.TryParse(User.FindFirstValue("PersonID"), out var personID);
        return Ok(await _signupsService.GetSignupDateInfo(personID, ct));
    }

    [HttpGet("{seasonID:int}", Name = "GetSignups")]
    public Task<List<SignupResponseDto>> GetSignups([FromRoute] int seasonID, CancellationToken ct)
    {
        return _signupsService.GetSignups(seasonID: seasonID, ct);
    }

    [Authorize(Roles = "Player,Sysadmin")]
    [HttpGet("{seasonID:int}", Name = "GetSignup")]
    public async Task<IActionResult> GetSignup([FromRoute] int seasonID, [FromQuery] int offset, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return Forbid("You must be logged to get signup data");

        return Ok(await _signupsService.GetSignup(seasonID: seasonID, offset: offset, userID: userID, ct));
    }

    [Authorize(Roles = "Player,Sysadmin")]
    [HttpPost(Name = "UpsertSignup")]
    public async Task<IActionResult> UpsertSignup([FromBody] SignupRequestDto signupDto, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return Forbid("You must be logged in to signup for a season");

        signupDto.UserID = userID;
        try
        {
            await _signupsService.UpsertSignup(signupDto, ct);
            return Ok();
        }
        catch (SignupsClosedException)
        {
            return BadRequest("Signups are closed for this season");
        }
    }

    private int GetUserID()
    {
        var userIDStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIDStr == null)
            return 0;
        int.TryParse(userIDStr, out var userID);
        return userID;
    }

}
