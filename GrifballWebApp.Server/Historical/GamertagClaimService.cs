using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

public class GamertagClaimDto
{
    public int GamertagClaimID { get; set; }
    public required string Kind { get; set; }
    public required string Status { get; set; }
    public long XboxUserID { get; set; }
    /// <summary>The Xbox account's gamertag now.</summary>
    public string? XboxGamertag { get; set; }
    /// <summary>The account's user, which the placeholder is merged into on approval.</summary>
    public int? UserID { get; set; }
    public required string Gamertag { get; set; }
    public int? PlaceholderUserID { get; set; }
    public int? SeasonID { get; set; }
    public string? SeasonName { get; set; }
    public string? TeamName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddGamertagHistoryRequest
{
    public long XboxUserID { get; set; }
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string Gamertag { get; set; } = "";
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
}

/// <summary>A gamertag claim or history request that can't be done as asked; the message is for the user (400).</summary>
public class GamertagClaimException(string message) : Exception(message);

/// <summary>
/// Commissioners' decisions on GamertagClaims (roster matches the import suggests, and placeholders a user claimed by
/// setting their gamertag), and sysadmins' additions to an Xbox account's gamertag history.
/// </summary>
public class GamertagClaimService
{
    private readonly GrifballContext _context;
    private readonly IUserMergeService _mergeService;

    public GamertagClaimService(GrifballContext context, IUserMergeService mergeService)
    {
        _context = context;
        _mergeService = mergeService;
    }

    public Task<List<GamertagClaimDto>> List(GamertagClaimStatus? status, CancellationToken ct = default)
        => _context.GamertagClaims.AsNoTracking()
            .Where(c => status == null || c.Status == status)
            .OrderBy(c => c.GamertagClaimID)
            .Select(c => new GamertagClaimDto
            {
                GamertagClaimID = c.GamertagClaimID,
                Kind = c.Kind.ToString(),
                Status = c.Status.ToString(),
                XboxUserID = c.XboxUserID,
                XboxGamertag = c.XboxUser.Gamertag,
                UserID = c.XboxUser.User != null ? c.XboxUser.User.Id : null,
                Gamertag = c.Gamertag,
                PlaceholderUserID = c.PlaceholderUserID,
                SeasonID = c.SeasonID,
                SeasonName = c.Season != null ? c.Season.SeasonName : null,
                TeamName = c.TeamName,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(ct);

    /// <summary>
    /// The gamertag goes into the Xbox account's history (dated by the claim's season, if any) and the placeholder is
    /// merged into the account's user, or becomes it when the account has none. Other pending claims on the placeholder
    /// are rejected. All in one transaction; a merge conflict changes nothing.
    /// </summary>
    public async Task Approve(int claimId, CancellationToken ct = default)
    {
        await _context.StartTransactionAsync(ct);
        try
        {
            var claim = await _context.GamertagClaims.Include(c => c.Season).Include(c => c.XboxUser).ThenInclude(x => x.User)
                .FirstOrDefaultAsync(c => c.GamertagClaimID == claimId, ct)
                ?? throw new GamertagClaimException($"There is no claim {claimId}");
            if (claim.Status != GamertagClaimStatus.Pending)
                throw new GamertagClaimException($"Claim {claimId} was already {claim.Status.ToString().ToLowerInvariant()}");

            var source = claim.Kind == GamertagClaimKind.RosterMatch ? GamertagHistorySource.RosterMatch : GamertagHistorySource.Manual;
            await _context.UpsertGamertagHistoryAsync(claim.XboxUserID, claim.Gamertag, claim.Season?.SeasonStart, claim.Season?.SeasonEnd, source, ct);
            claim.Status = GamertagClaimStatus.Approved;

            var placeholderId = claim.PlaceholderUserID;
            if (placeholderId is not null)
            {
                var others = await _context.GamertagClaims
                    .Where(c => c.GamertagClaimID != claimId && c.Status == GamertagClaimStatus.Pending && c.PlaceholderUserID == placeholderId)
                    .ToListAsync(ct);
                others.ForEach(c => c.Status = GamertagClaimStatus.Rejected);
            }
            await _context.SaveChangesAsync(ct);

            var owner = claim.XboxUser.User;
            if (placeholderId is not null && owner is null)
            {
                // Nobody holds the account: the placeholder becomes its user
                var placeholder = await _context.Users.FirstAsync(u => u.Id == placeholderId, ct);
                placeholder.XboxUserID = claim.XboxUserID;
                await _context.SaveChangesAsync(ct);
            }
            else if (placeholderId is not null && owner!.Id != placeholderId)
            {
                await _mergeService.Merge(owner.Id, placeholderId.Value, null, ct);
            }

            await _context.CommitTransactionAsync(ct);
        }
        catch
        {
            await _context.RollbackTransactionAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task Reject(int claimId, CancellationToken ct = default)
    {
        var claim = await _context.GamertagClaims.FirstOrDefaultAsync(c => c.GamertagClaimID == claimId, ct)
            ?? throw new GamertagClaimException($"There is no claim {claimId}");
        if (claim.Status != GamertagClaimStatus.Pending)
            throw new GamertagClaimException($"Claim {claimId} was already {claim.Status.ToString().ToLowerInvariant()}");
        claim.Status = GamertagClaimStatus.Rejected;
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>A gamertag a sysadmin knows the Xbox account had (Source Manual).</summary>
    public async Task AddHistory(AddGamertagHistoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Gamertag))
            throw new GamertagClaimException("Give the gamertag");
        if (request.FirstSeen is not null && request.LastSeen is not null && request.FirstSeen > request.LastSeen)
            throw new GamertagClaimException("FirstSeen is after LastSeen");
        if (!await _context.XboxUsers.AnyAsync(x => x.XboxUserID == request.XboxUserID, ct))
            throw new GamertagClaimException($"There is no Xbox account {request.XboxUserID}; set it as someone's gamertag first");
        await _context.UpsertGamertagHistoryAsync(request.XboxUserID, request.Gamertag, request.FirstSeen, request.LastSeen, GamertagHistorySource.Manual, ct);
        await _context.SaveChangesAsync(ct);
    }
}
