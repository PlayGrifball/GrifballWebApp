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
    private readonly XuidLookupService _lookups;
    private readonly XuidLookupSignal _signal;

    public GamertagClaimsController(GamertagClaimService claims, XuidLookupService lookups, XuidLookupSignal signal)
    {
        _claims = claims;
        _lookups = lookups;
        _signal = signal;
    }

    /// <summary>Looks up the XUIDs of import placeholders again (those not found or failed), in the background: the
    /// worker role runs them, throttled.</summary>
    [HttpPost(Name = "LookUpXuids")]
    public async Task<ActionResult<XuidLookupStatusDto>> LookUpXuids(CancellationToken ct)
    {
        await _lookups.LookUpAgainAsync(ct);
        _signal.Notify();
        return Accepted(await _lookups.StatusAsync(ct));
    }

    [HttpGet(Name = "GetXuidLookupStatus")]
    public Task<XuidLookupStatusDto> XuidLookupStatus(CancellationToken ct) => _lookups.StatusAsync(ct);

    /// <summary>An Xbox account's past gamertags, by XUID or by its user's ID.</summary>
    [HttpGet(Name = "GetPastGamertags")]
    public async Task<ActionResult<PastGamertagsDto>> PastGamertags([FromQuery] long? xboxUserID, [FromQuery] int? userID, CancellationToken ct)
    {
        var result = await _claims.PastGamertags(xboxUserID, userID, ct);
        return result is null ? NotFound() : result;
    }

    [HttpDelete("{historyId:int}", Name = "RemoveGamertagHistory")]
    public async Task<IActionResult> RemoveGamertagHistory([FromRoute] int historyId, CancellationToken ct)
    {
        await _claims.RemoveHistory(historyId, ct);
        return NoContent();
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
