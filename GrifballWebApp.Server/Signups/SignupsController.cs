using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GrifballWebApp.Application.Signups;

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

    // TODO: Can this be removed? I don't see it being called
    [HttpGet(Name = "GetSignupDateInfo")]
    public async Task<IActionResult> GetSignupDateInfo(CancellationToken ct)
    {
        var personID = int.TryParse(User.FindFirstValue("PersonID"), out var id) ? id : 0;
        return Ok(await _signupsService.GetSignupDateInfo(personID, ct));
    }

    [HttpGet("{seasonID:int}", Name = "GetSignups")]
    public Task<List<SignupResponseDto>> GetSignups([FromRoute] int seasonID, CancellationToken ct)
    {
        return _signupsService.GetSignups(seasonID: seasonID, ct);
    }

    [HttpGet("{seasonID:int}", Name = "GetTimeslots")]
    public async Task<IActionResult> GetTimeslots([FromRoute] int seasonID, [FromQuery] int offset, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return Unauthorized("You must be logged in to get timeslots");

        return Ok(await _signupsService.GetTimeslots(seasonID: seasonID, offset: offset, userID: userID, ct));
    }

    [Authorize(Roles = "Player,Sysadmin")]
    [HttpGet("{seasonID:int}", Name = "GetSignup")]
    public async Task<IActionResult> GetSignup([FromRoute] int seasonID, [FromQuery] int offset, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return Unauthorized("You must be logged in to get signup data");

        return Ok(await _signupsService.GetSignup(seasonID: seasonID, offset: offset, userID: userID, ct));
    }

    [Authorize(Roles = "Player,Sysadmin")]
    [HttpPost(Name = "UpsertSignup")]
    public async Task<IActionResult> UpsertSignup([FromBody] SignupRequestDto signupDto, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return Unauthorized("You must be logged in to sign up for a season");

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
        catch (SignupNeedsGamertagException e)
        {
            return BadRequest(e.Message);
        }
        catch (SignupTeamNameTooLongException e)
        {
            return BadRequest(e.Message);
        }
    }

    private int GetUserID()
    {
        var userIDStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIDStr == null)
            return 0;
        return int.TryParse(userIDStr, out var userID) ? userID : 0;
    }

}
