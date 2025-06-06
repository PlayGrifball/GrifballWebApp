using GrifballWebApp.Database;
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
    private readonly IProfileService _profileSevice;

    public ProfileController(GrifballContext context, IProfileService profileService)
    {
        _context = context;
        _profileSevice = profileService;
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

    [Authorize(Roles = "Player")]
    [HttpGet("{userID:int}", Name = "SetGamertag")]
    public Task SetGamertag([FromRoute] int userID, [FromQuery] string gamertag, CancellationToken ct)
    {
        var id = GetUserID();
        if (id != userID)
            throw new Exception("Cannot set another user's gamertag");

        return _profileSevice.SetGamertag(userID, gamertag, ct);
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
