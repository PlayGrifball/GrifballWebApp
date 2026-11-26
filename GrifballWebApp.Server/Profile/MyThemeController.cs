using System.Security.Claims;
using GrifballWebApp.Application.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Profile;

/// <summary>The theme (colours and font) a signed-in user chose, kept with their account so every device shows it.</summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public class MyThemeController : ControllerBase
{
    private readonly UserThemeService _themes;

    public MyThemeController(UserThemeService themes)
    {
        _themes = themes;
    }

    /// <summary>The signed-in user's saved theme, or 204 No Content when they have not chosen one.</summary>
    [HttpGet("me/theme", Name = "GetMyTheme")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<UserThemeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<UserThemeDto>> Get(CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        var theme = await _themes.Get(userId, ct);
        if (theme is null)
            return NoContent();
        return theme;
    }

    /// <summary>Saves the signed-in user's theme.</summary>
    [HttpPut("me/theme", Name = "SetMyTheme")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Set([FromBody] UserThemeDto request, CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return this.UnauthorizedProblem(NotSignedIn);
        return await _themes.Set(userId, request, ct) switch
        {
            SetThemeResult.Invalid => this.BadRequestProblem("That is not a theme the editor can make"),
            SetThemeResult.NoSuchUser => this.NotFoundProblem(ApiProblems.NoSuchUser),
            _ => NoContent(),
        };
    }

    private const string NotSignedIn = "You must be signed in";

    private int? GetUserID()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
