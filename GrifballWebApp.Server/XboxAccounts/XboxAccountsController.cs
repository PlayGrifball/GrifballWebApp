using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.XboxAccounts;
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
}
