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
    public int? TeamID { get; set; }
    public string? TeamName { get; set; }
    /// <summary>An XUID lookup: the account's gamertag now, and when it was looked up.</summary>
    public string? CurrentGamertag { get; set; }
    public DateTime? LookedUpAt { get; set; }
    /// <summary>The seasons the placeholder played (its stats and titles), "Name (year)", oldest first: who used the
    /// gamertag then, to weigh against who holds it now.</summary>
    public List<string> PlaceholderSeasons { get; set; } = [];
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
    /// <summary>Why, e.g. "known as this in Winter League 2019".</summary>
    [System.ComponentModel.DataAnnotations.MaxLength(200)]
    public string? Note { get; set; }
}

/// <summary>A past gamertag of an Xbox account, as the sysadmin page lists it.</summary>
public class PastGamertagDto
{
    public int XboxGamertagHistoryID { get; set; }
    public long XboxUserID { get; set; }
    public required string Gamertag { get; set; }
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public required string Source { get; set; }
    public string? Note { get; set; }
    /// <summary>Only a gamertag a sysadmin added can be removed: the others are facts the app saw.</summary>
    public bool CanRemove { get; set; }
}

/// <summary>An Xbox account and its past gamertags.</summary>
public class PastGamertagsDto
{
    public long XboxUserID { get; set; }
    public required string CurrentGamertag { get; set; }
    public int? UserID { get; set; }
    public List<PastGamertagDto> PastGamertags { get; set; } = [];
}

/// <summary>A gamertag claim or history request that can't be done as asked; the message is for the user (400).</summary>
public class GamertagClaimException(string message) : Exception(message);

/// <summary>A claim whose placeholder changed since it was made (409).</summary>
public class GamertagClaimConflictException(string message) : Exception(message);

/// <summary>
/// Sysadmins' decisions on GamertagClaims (roster matches the import suggests, and placeholders a user claimed by
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

    public async Task<List<GamertagClaimDto>> List(GamertagClaimStatus? status, CancellationToken ct = default)
    {
        var claims = await _context.GamertagClaims.AsNoTracking()
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
                TeamID = c.TeamID,
                TeamName = c.Team != null ? c.Team.TeamName : null,
                CurrentGamertag = c.CurrentGamertag,
                LookedUpAt = c.LookedUpAt,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(ct);
        // The seasons each placeholder played: who used the gamertag then
        var placeholders = claims.Where(c => c.PlaceholderUserID != null).Select(c => c.PlaceholderUserID!.Value).Distinct().ToList();
        var played = await _context.PlayerSeasonStats.AsNoTracking().Where(s => placeholders.Contains(s.UserID))
            .Select(s => new { s.UserID, s.Season.SeasonName, s.Season.SeasonStart })
            .Union(_context.PlayerChampionships.AsNoTracking().Where(c => placeholders.Contains(c.UserID))
                .Select(c => new { c.UserID, c.Season.SeasonName, c.Season.SeasonStart }))
            .ToListAsync(ct);
        foreach (var claim in claims.Where(c => c.PlaceholderUserID != null))
            claim.PlaceholderSeasons = played.Where(p => p.UserID == claim.PlaceholderUserID).OrderBy(p => p.SeasonStart)
                .Select(p => $"{p.SeasonName} ({p.SeasonStart.Year})").Distinct().ToList();
        return claims;
    }

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

            // An XUID lookup found the account by the gamertag it has now: no past gamertag to record
            if (claim.Kind != GamertagClaimKind.XuidLookup)
            {
                var source = claim.Kind == GamertagClaimKind.RosterMatch ? GamertagHistorySource.RosterMatch : GamertagHistorySource.Manual;
                await _context.UpsertGamertagHistoryAsync(claim.XboxUserID, claim.Gamertag, claim.Season?.SeasonStart, claim.Season?.SeasonEnd, source, ct);
            }
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
            if (placeholderId is not null && owner?.Id != placeholderId)
            {
                // Only an import placeholder is linked or merged on a claim: not a real account, nor one tied to an Xbox
                // account since the claim was made
                var placeholder = await _context.Users.FirstAsync(u => u.Id == placeholderId, ct);
                if (!placeholder.IsDummyUser || placeholder.UserName?.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.OrdinalIgnoreCase) != true || placeholder.XboxUserID is not null)
                    throw new GamertagClaimConflictException($"User {placeholderId} is no longer an unlinked placeholder (historical:<gamertag>); reject the claim or merge by hand.");
            }
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
        var row = await _context.UpsertGamertagHistoryAsync(request.XboxUserID, request.Gamertag, request.FirstSeen, request.LastSeen, GamertagHistorySource.Manual, ct);
        if (!string.IsNullOrWhiteSpace(request.Note))
            row.Note = request.Note.Trim();
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>The past gamertags of an Xbox account, by its XUID or its user's ID. Null when there is no such account.</summary>
    public async Task<PastGamertagsDto?> PastGamertags(long? xboxUserId, int? userId, CancellationToken ct = default)
    {
        var xbox = await _context.XboxUsers.AsNoTracking()
            .Where(x => (xboxUserId != null && x.XboxUserID == xboxUserId) || (userId != null && x.User != null && x.User.Id == userId))
            .Select(x => new { x.XboxUserID, x.Gamertag, UserID = x.User != null ? (int?)x.User.Id : null })
            .FirstOrDefaultAsync(ct);
        if (xbox is null)
            return null;
        var rows = await _context.XboxGamertagHistory.AsNoTracking().Where(h => h.XboxUserID == xbox.XboxUserID)
            .OrderBy(h => h.FirstSeen ?? h.LastSeen).ThenBy(h => h.XboxGamertagHistoryID).ToListAsync(ct);
        return new PastGamertagsDto
        {
            XboxUserID = xbox.XboxUserID,
            CurrentGamertag = xbox.Gamertag,
            UserID = xbox.UserID,
            PastGamertags = rows.Select(h => new PastGamertagDto
            {
                XboxGamertagHistoryID = h.XboxGamertagHistoryID,
                XboxUserID = h.XboxUserID,
                Gamertag = h.Gamertag,
                FirstSeen = h.FirstSeen,
                LastSeen = h.LastSeen,
                Source = h.Source.ToString(),
                Note = h.Note,
                CanRemove = h.Source == GamertagHistorySource.Manual,
            }).ToList(),
        };
    }

    /// <summary>Removes a past gamertag a sysadmin added (Source Manual); the ones the app saw stay.</summary>
    public async Task RemoveHistory(int historyId, CancellationToken ct = default)
    {
        var row = await _context.XboxGamertagHistory.FirstOrDefaultAsync(h => h.XboxGamertagHistoryID == historyId, ct)
            ?? throw new GamertagClaimException($"There is no past gamertag {historyId}");
        if (row.Source != GamertagHistorySource.Manual)
            throw new GamertagClaimException("Only a past gamertag added by hand can be removed");
        _context.XboxGamertagHistory.Remove(row);
        await _context.SaveChangesAsync(ct);
    }
}
