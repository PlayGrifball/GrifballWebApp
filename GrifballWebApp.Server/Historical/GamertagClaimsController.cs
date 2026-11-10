using GrifballWebApp.Domain.Archive;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Historical;

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
    private readonly DuplicateSuggestionService? _duplicates;

    public GamertagClaimsController(GamertagClaimService claims, XuidLookupService lookups, XuidLookupSignal signal, DuplicateSuggestionService? duplicates = null)
    {
        _claims = claims;
        _lookups = lookups;
        _signal = signal;
        _duplicates = duplicates;
    }

    /// <summary>Placeholders that may be the same player as another user (close gamertags, seasons that don't conflict).</summary>
    [HttpGet(Name = "GetDuplicateSuggestions")]
    public Task<List<DuplicateSuggestionDto>> DuplicateSuggestions([FromQuery] int max = 50, CancellationToken ct = default)
        => _duplicates!.List(max, ct);

    /// <summary>Merges a suggested placeholder into the other user (audited).</summary>
    [HttpPost(Name = "MergeDuplicate")]
    public async Task<IActionResult> MergeDuplicate([FromBody] DuplicatePairRequest request, CancellationToken ct)
    {
        await _duplicates!.Merge(request.SourceUserID, request.TargetUserID, ct);
        return NoContent();
    }

    /// <summary>The pair are different players: not suggested again.</summary>
    [HttpPost(Name = "DismissDuplicate")]
    public async Task<IActionResult> DismissDuplicate([FromBody] DuplicatePairRequest request, CancellationToken ct)
    {
        await _duplicates!.Dismiss(request.SourceUserID, request.TargetUserID, ct);
        return NoContent();
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
    public async Task<IActionResult> RejectClaim([FromRoute] int claimId, [FromBody] RejectGamertagClaimRequest? request, CancellationToken ct)
    {
        await _claims.Reject(claimId, request?.Reason, ct);
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

public class RejectGamertagClaimRequest
{
    /// <summary>Shown to the claimant.</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Reason { get; set; }
}
