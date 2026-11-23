using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Server.Mcp;

namespace GrifballWebApp.Server.StatFlags;

/// <summary>Stat flags (#548): plain-rule findings about game stats, for commissioners and sysadmins to look at and
/// dismiss. A flag never changes the game it's about.</summary>
[ApiController]
[Authorize(Roles = "Commissioner,Sysadmin")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public class StaffStatFlagsController : ControllerBase
{
    private readonly StaffStatFlagService _statFlags;

    public StaffStatFlagsController(StaffStatFlagService statFlags)
    {
        _statFlags = statFlags;
    }

    /// <summary>Every open flag, newest first. Commissioners and sysadmins only.</summary>
    [AllowAccessToken]
    [HttpGet("stat-flags", Name = "GetOpenStatFlags")]
    [ProducesResponseType<List<StatFlagDto>>(StatusCodes.Status200OK)]
    public Task<List<StatFlagDto>> List(CancellationToken ct) => _statFlags.OpenFlagsAsync(ct);

    /// <summary>The open flags of every game of a season match's series, for its marker on the match page. Commissioners
    /// and sysadmins only.</summary>
    [AllowAccessToken]
    [HttpGet("season-matches/{seasonMatchID:int}/stat-flags", Name = "GetSeasonMatchStatFlags")]
    [ProducesResponseType<List<StatFlagDto>>(StatusCodes.Status200OK)]
    public Task<List<StatFlagDto>> ForSeasonMatch([FromRoute] int seasonMatchID, CancellationToken ct) => _statFlags.OpenFlagsForSeasonMatchAsync(seasonMatchID, ct);

    /// <summary>Staff looked at it: not wrong, or not worth fixing. 404 when it doesn't exist. Commissioners and
    /// sysadmins only. A normal staff write, like VodController's own Dismiss: it only sets this flag's own dismissed
    /// columns, never a merge or anything about sign-in/roles/the account, so a token may call it too (#549/#562).</summary>
    [AllowAccessToken]
    [HttpPost("stat-flags/{statFlagID:int}/dismiss", Name = "DismissStatFlag")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Dismiss([FromRoute] int statFlagID, CancellationToken ct)
    {
        await _statFlags.Dismiss(statFlagID, ct);
        return NoContent();
    }
}
