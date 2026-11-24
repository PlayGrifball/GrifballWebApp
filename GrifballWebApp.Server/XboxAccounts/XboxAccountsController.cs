using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Server.XboxAccounts;

[ApiController]
public class XboxAccountsController : ControllerBase
{
    private const string ProblemJson = "application/problem+json";

    private readonly XboxAccountService _accounts;

    public XboxAccountsController(XboxAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>An Xbox account and a page of the Halo games the site saved that it played in, newest first; 404 for an account the site doesn't know.</summary>
    [AllowAnonymous]
    [HttpGet("xbox-accounts/{xboxUserID:long}", Name = "GetXboxAccount")]
    [ProducesResponseType<XboxAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<XboxAccountDto>> GetXboxAccount([FromRoute] long xboxUserID, CancellationToken ct, [FromQuery] int page = 1)
    {
        var account = await _accounts.GetAsync(xboxUserID, page, ct);
        return account is null ? this.NotFoundProblem("The site has no such Xbox account") : account;
    }

    /// <summary>Where the pull of the account's recent games is: queued, running with its progress, or idle with its last run.</summary>
    [AllowAnonymous]
    [HttpGet("xbox-accounts/{xboxUserID:long}/pull", Name = "GetXboxAccountPullStatus")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status200OK)]
    public Task<OnDemandJobStatusDto> GetXboxAccountPullStatus([FromRoute] long xboxUserID, [FromServices] IXboxAccountPulls pulls, CancellationToken ct)
        => pulls.Status(xboxUserID, ct);

    /// <summary>Queues a pull of the account's recent games from Halo Infinite: a 202 with where it is.</summary>
    // Any player may ask, as staff may (as for a season match's pull): one pull per account is queued or running at a time
    [Authorize(Roles = SiteRoles.Pullers)]
    [HttpPost("xbox-accounts/{xboxUserID:long}/pull", Name = "PullXboxAccount")]
    [ProducesResponseType<OnDemandJobStatusDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    public async Task<ActionResult<OnDemandJobStatusDto>> PullXboxAccount([FromRoute] long xboxUserID, [FromServices] IXboxAccountPulls pulls, CancellationToken ct)
    {
        if (!await _accounts.Exists(xboxUserID, ct))
            return this.NotFoundProblem("The site has no such Xbox account");
        return Accepted(await pulls.Queue(xboxUserID, JobUser.IdOf(User), ct));
    }

    /// <summary>The account's gamer picture, as the site stored it; 404 when none is stored yet.</summary>
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    [HttpGet("xbox-accounts/{xboxUserID:long}/gamer-picture", Name = "GetXboxAccountGamerPicture")]
    public Task<IActionResult> GetGamerPicture([FromRoute] long xboxUserID, [FromServices] XboxImageService images, CancellationToken ct)
        => Image(images, xboxUserID, XboxUserImageKind.GamerPicture, ct);

    /// <summary>The account's Halo Infinite emblem, as the site stored it; 404 when none is stored yet.</summary>
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    [HttpGet("xbox-accounts/{xboxUserID:long}/halo-emblem", Name = "GetXboxAccountHaloEmblem")]
    public Task<IActionResult> GetHaloEmblem([FromRoute] long xboxUserID, [FromServices] XboxImageService images, CancellationToken ct)
        => Image(images, xboxUserID, XboxUserImageKind.HaloEmblem, ct);

    private static Microsoft.Net.Http.Headers.EntityTagHeaderValue ETagOf(DateTime checkedAt)
        => new($"\"{DateTime.SpecifyKind(checkedAt, DateTimeKind.Utc).Ticks}\"");

    private async Task<IActionResult> Image(XboxImageService images, long xboxUserID, XboxUserImageKind kind, CancellationToken ct)
    {
        if (await images.GetCheckedAtAsync(xboxUserID, kind, ct) is not { } version)
            return NotFound();
        // A day: a picture or an emblem changes rarely, and the job checks again every couple of weeks
        Response.Headers.CacheControl = "public, max-age=86400";
        var request = Request.GetTypedHeaders();
        var etag = ETagOf(version);
        if (request.IfNoneMatch.Any(t => t.Compare(etag, useStrongComparison: false)))
        {
            Response.Headers.ETag = etag.ToString();
            return StatusCode(StatusCodes.Status304NotModified);
        }
        if (await images.GetAsync(xboxUserID, kind, ct) is not { } image)
            return NotFound();
        var checkedAt = DateTime.SpecifyKind(image.CheckedAt, DateTimeKind.Utc);
        return File(image.Content, image.ContentType, new DateTimeOffset(checkedAt), ETagOf(image.CheckedAt));
    }
}
