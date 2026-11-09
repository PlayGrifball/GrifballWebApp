using GrifballWebApp.Database;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GrifballWebApp.Server.Profile;

[Route("[controller]/[action]")]
[ApiController]
public class ProfileController : ControllerBase
{
    private readonly GrifballContext _context;
    private readonly ISetGamertagService _setGamertagService;
    private readonly ProfileStatsService _profiles;
    private readonly ProfileCommendationsService _commendations;

    public ProfileController(GrifballContext context, ISetGamertagService setGamertagService, ProfileStatsService profiles, ProfileCommendationsService commendations)
    {
        _context = context;
        _setGamertagService = setGamertagService;
        _profiles = profiles;
        _commendations = commendations;
    }

    [HttpGet("{userID:int}", Name = "GetGamertag")]
    public Task<ProfileDto?> GetGamertag([FromRoute] int userID, CancellationToken ct)
    {
        return _context.Users
            .Where(x => x.Id == userID)
            .Select(x => new ProfileDto()
            {
                Gamertag = x.XboxUser.Gamertag
            })
            .FirstOrDefaultAsync(ct);
    }

    [HttpGet("{userID:int}", Name = "GetProfileStats")]
    public async Task<ActionResult<ProfileStatsDto>> GetProfileStats([FromRoute] int userID, CancellationToken ct)
    {
        var profile = await _profiles.GetAsync(userID, ct);
        if (profile is null)
            return NotFound();
        return profile;
    }

    /// <summary>The player's most earned Halo Infinite medals; empty for an unknown user or one with no Xbox account.</summary>
    [HttpGet("{userID:int}", Name = "GetCommendations")]
    public Task<ProfileCommendationsDto> GetCommendations([FromRoute] int userID, [FromQuery] int top = ProfileCommendationsService.DefaultTop, CancellationToken ct = default)
    {
        return _commendations.GetAsync(userID, top, ct);
    }

    [Authorize(Roles = "Player")]
    [HttpPost("{userID:int}", Name = "SetGamertag")]
    public async Task<IActionResult> SetGamertag([FromRoute] int userID, [FromQuery] string gamertag, CancellationToken ct)
    {
        var id = GetUserID();
        if (id != userID)
            return Forbid();

        var errorMessage = await _setGamertagService.SetGamertag(userID, gamertag, ct);

        if (errorMessage is not null)
            return BadRequest(errorMessage);

        return Ok();
    }

    private int? GetUserID()
    {
        var stringName = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (stringName == null)
            return null;
        var parsed = int.TryParse(stringName, out var id);
        return parsed ? id : null;
    }
}
