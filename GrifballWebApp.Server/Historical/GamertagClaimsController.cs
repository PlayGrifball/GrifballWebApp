using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Historical;

/// <summary>
/// Gamertag claims (roster matches the import suggests, placeholders users claimed by setting a gamertag) and the
/// gamertag history they write: sysadmin only, like merging users (approving a claim merges a placeholder).
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
[Authorize(Roles = "Sysadmin")]
public class GamertagClaimsController : ControllerBase
{
    private readonly GamertagClaimService _claims;

    public GamertagClaimsController(GamertagClaimService claims)
    {
        _claims = claims;
    }

    /// <summary>Gamertag claims, pending by default.</summary>
    [HttpGet(Name = "GetGamertagClaims")]
    public Task<List<GamertagClaimDto>> GetClaims([FromQuery] GamertagClaimStatus? status = GamertagClaimStatus.Pending, CancellationToken ct = default)
        => _claims.List(status, ct);

    /// <summary>Records the gamertag as the Xbox account's and merges the placeholder into the account's user.</summary>
    [HttpPost("{claimId:int}", Name = "ApproveGamertagClaim")]
    public async Task<IActionResult> ApproveClaim([FromRoute] int claimId, CancellationToken ct)
    {
        await _claims.Approve(claimId, ct);
        return NoContent();
    }

    [HttpPost("{claimId:int}", Name = "RejectGamertagClaim")]
    public async Task<IActionResult> RejectClaim([FromRoute] int claimId, CancellationToken ct)
    {
        await _claims.Reject(claimId, ct);
        return NoContent();
    }

    /// <summary>Adds a gamertag an Xbox account is known to have had (a rename the app didn't see).</summary>
    [HttpPost(Name = "AddGamertagHistory")]
    public async Task<IActionResult> AddGamertagHistory([FromBody] AddGamertagHistoryRequest request, CancellationToken ct)
    {
        await _claims.AddHistory(request, ct);
        return NoContent();
    }
}
