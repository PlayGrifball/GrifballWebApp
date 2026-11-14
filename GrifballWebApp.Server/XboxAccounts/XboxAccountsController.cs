using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.XboxAccounts;

namespace GrifballWebApp.Server.XboxAccounts;

[ApiController]
public class XboxAccountsController : ControllerBase
{
    private readonly XboxAccountService _accounts;

    public XboxAccountsController(XboxAccountService accounts)
    {
        _accounts = accounts;
    }

    /// <summary>An Xbox account and a page of the Halo games the site saved that it played in, newest first; 404 for an account the site doesn't know.</summary>
    [AllowAnonymous]
    [HttpGet("xbox-accounts/{xboxUserID:long}", Name = "GetXboxAccount")]
    [ProducesResponseType<XboxAccountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<XboxAccountDto>> GetXboxAccount([FromRoute] long xboxUserID, CancellationToken ct, [FromQuery] int page = 1)
    {
        var account = await _accounts.GetAsync(xboxUserID, page, ct);
        return account is null ? this.NotFoundProblem("The site has no such Xbox account") : account;
    }
}
