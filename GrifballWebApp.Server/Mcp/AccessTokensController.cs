using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Mcp;

/// <summary>
/// The signed-in user's personal access tokens, for an MCP client to act as them (<see cref="AccessTokenService"/>). Managed
/// from the site only: an access token doesn't sign in here (the default scheme doesn't know it), so one token can't make more.
/// </summary>
[ApiController]
[Authorize]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
public class AccessTokensController : ControllerBase
{
    private const string ProblemJson = "application/problem+json";

    private readonly AccessTokenService _tokens;

    public AccessTokensController(AccessTokenService tokens)
    {
        _tokens = tokens;
    }

    private int UserID => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Your access tokens, newest first: names and dates, never the tokens themselves.</summary>
    [HttpGet("identity/access-tokens", Name = "GetAccessTokens")]
    [ProducesResponseType<List<AccessTokenDto>>(StatusCodes.Status200OK)]
    public async Task<List<AccessTokenDto>> GetAccessTokens(CancellationToken ct)
        => await _tokens.List(UserID, ct);

    /// <summary>Makes an access token. The answer holds the token itself, shown this once: only its hash is kept.</summary>
    [HttpPost("identity/access-tokens", Name = "CreateAccessToken")]
    [ProducesResponseType<CreatedAccessTokenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    public async Task<ActionResult<CreatedAccessTokenDto>> CreateAccessToken([FromBody] CreateAccessTokenRequest request, CancellationToken ct)
    {
        var (created, refusal) = await _tokens.Create(UserID, request, ct);
        return created is not null ? created : this.BadRequestProblem(refusal!);
    }

    /// <summary>Revokes one of your access tokens: it stops working at once.</summary>
    [HttpPost("identity/access-tokens/{id}/revoke", Name = "RevokeAccessToken")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<IActionResult> RevokeAccessToken([FromRoute] string id, CancellationToken ct)
        => await _tokens.Revoke(UserID, id, ct) ? NoContent() : this.NotFoundProblem("You have no such access token");
}
