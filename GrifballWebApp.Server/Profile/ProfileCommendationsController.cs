using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Profile;

/// <summary>A profile's commendations, beside <see cref="ProfileController"/>'s actions under the same route.</summary>
[Route("Profile/[action]")]
[ApiController]
public class ProfileCommendationsController : ControllerBase
{
    private readonly ProfileCommendationsService _commendations;

    public ProfileCommendationsController(ProfileCommendationsService commendations)
    {
        _commendations = commendations;
    }

    [HttpGet("{userID:int}", Name = "GetCommendations")]
    public async Task<ActionResult<ProfileCommendationsDto>> GetCommendations([FromRoute] int userID, [FromQuery] int top = ProfileCommendationsService.DefaultTop, CancellationToken ct = default)
    {
        var commendations = await _commendations.GetAsync(userID, top, ct);
        if (commendations is null)
            return NotFound();
        return commendations;
    }
}
