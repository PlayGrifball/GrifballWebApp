using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Historical;

public class GamertagClaimDto
{
    public int GamertagClaimID { get; set; }
    public required string Kind { get; set; }
    public required string Status { get; set; }
    /// <summary>Null for a user's or Discord claim whose user has no Xbox account linked.</summary>
    public long? XboxUserID { get; set; }
    /// <summary>The Xbox account's gamertag now.</summary>
    public string? XboxGamertag { get; set; }
    /// <summary>The user the placeholder is merged into on approval: the claimant, or the Xbox account's user.</summary>
    public int? UserID { get; set; }
    /// <summary>A user's or Discord claim: the user it is about.</summary>
    public int? ClaimantUserID { get; set; }
    public string? ClaimantName { get; set; }
    /// <summary>A user's claim: when they say they had the gamertag.</summary>
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
    public string? RejectionReason { get; set; }
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
    public List<GrifballWebApp.Application.Seasons.SeasonRefDto> PlaceholderSeasons { get; set; } = [];
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
    private readonly IApplicationDbContext _context;
    private readonly IUserMergeService _mergeService;
    private readonly ICurrentUserService? _currentUser;
    private readonly TimeProvider _time;

    public GamertagClaimService(IApplicationDbContext context, IUserMergeService mergeService, ICurrentUserService? currentUser = null, TimeProvider? time = null)
    {
        _context = context;
        _mergeService = mergeService;
        _currentUser = currentUser;
        _time = time ?? TimeProvider.System;
    }

    public async Task<List<GamertagClaimDto>> List(GamertagClaimStatus? status, CancellationToken ct = default)
    {
        var claims = await Project(_context.GamertagClaims.AsNoTracking()
                .Where(c => status == null || c.Status == status)
                .OrderBy(c => c.GamertagClaimID))
            .ToListAsync(ct);
        await AddPlaceholderSeasons(claims, ct);
        return claims;
    }

    internal static IQueryable<GamertagClaimDto> Project(IQueryable<GamertagClaim> claims) => claims.Select(c => new GamertagClaimDto
    {
        GamertagClaimID = c.GamertagClaimID,
        Kind = c.Kind.ToString(),
        Status = c.Status.ToString(),
        XboxUserID = c.XboxUserID,
        XboxGamertag = c.XboxUser != null ? c.XboxUser.Gamertag : null,
        UserID = c.ClaimantUserID ?? (c.XboxUser != null && c.XboxUser.User != null ? (int?)c.XboxUser.User.Id : null),
        ClaimantUserID = c.ClaimantUserID,
        ClaimantName = c.ClaimantUser != null ? (c.ClaimantUser.DisplayName ?? c.ClaimantUser.UserName) : null,
        FirstSeen = c.FirstSeen,
        LastSeen = c.LastSeen,
        RejectionReason = c.RejectionReason,
        Gamertag = c.Gamertag,
        PlaceholderUserID = c.PlaceholderUserID,
        SeasonID = c.SeasonID,
        SeasonName = c.Season != null ? c.Season.SeasonName : null,
        TeamID = c.TeamID,
        TeamName = c.Team != null ? c.Team.TeamName : null,
        CurrentGamertag = c.CurrentGamertag,
        LookedUpAt = c.LookedUpAt,
        CreatedAt = c.CreatedAt,
    });

    private async Task AddPlaceholderSeasons(List<GamertagClaimDto> claims, CancellationToken ct)
    {
        // The seasons each placeholder played: who used the gamertag then
        var placeholders = claims.Where(c => c.PlaceholderUserID != null).Select(c => c.PlaceholderUserID!.Value).Distinct().ToList();
        var played = await GrifballWebApp.Application.Seasons.SeasonRefDto.PlayedByAsync(_context, placeholders, ct);
        foreach (var claim in claims.Where(c => c.PlaceholderUserID != null))
            claim.PlaceholderSeasons = played.GetValueOrDefault(claim.PlaceholderUserID!.Value) ?? [];
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
            var claim = await _context.GamertagClaims.Include(c => c.Season).Include(c => c.XboxUser).ThenInclude(x => x!.User)
                .Include(c => c.ClaimantUser)
                .FirstOrDefaultAsync(c => c.GamertagClaimID == claimId, ct)
                ?? throw new GamertagClaimException($"There is no claim {claimId}");
            // Decided first: a claim decided already changes nothing (and the rest rolls back if the merge fails)
            var (decidedBy, decidedAt) = Decider();
            ThrowIfDecided(claim.Approve(decidedBy, decidedAt), claimId);

            // A user's or Discord claim is about its claimant: the placeholder is merged into them, and the gamertag goes
            // into their Xbox account's history if they have one
            // (the claimant's foreign key keeps them there: a merge moves their claims, nothing else deletes users)
            var aboutUser = claim.ClaimantUserID is not null;
            var xuid = aboutUser ? claim.XboxUserID ?? claim.ClaimantUser!.XboxUserID : claim.XboxUserID;

            // An XUID lookup found the account by the gamertag it has now: no past gamertag to record
            if (claim.Kind != GamertagClaimKind.XuidLookup && xuid is not null)
                await RecordPastGamertag(claim, xuid.Value, ct);
            var placeholderId = claim.PlaceholderUserID;
            if (placeholderId is not null)
            {
                var others = await _context.GamertagClaims
                    .Where(c => c.GamertagClaimID != claimId && c.Status == GamertagClaimStatus.Pending && c.PlaceholderUserID == placeholderId)
                    .ToListAsync(ct);
                others.ForEach(c => c.Reject(null, decidedBy, decidedAt));
            }
            await _context.SaveChangesAsync(ct);

            var owner = aboutUser ? claim.ClaimantUser : claim.XboxUser?.User;
            if (placeholderId is not null)
                await SettlePlaceholder(claim, owner, placeholderId.Value, ct);

            await _context.CommitTransactionAsync(ct);
        }
        catch
        {
            await _context.RollbackTransactionAsync(CancellationToken.None);
            _context.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>The claimed gamertag into the Xbox account's history: dated by the claimant's sightings, else by the claim's season.</summary>
    private async Task RecordPastGamertag(GamertagClaim claim, long xuid, CancellationToken ct)
    {
        var source = claim.Kind == GamertagClaimKind.RosterMatch ? GamertagHistorySource.RosterMatch : GamertagHistorySource.Manual;
        var (from, to) = claim.Kind == GamertagClaimKind.UserClaim
            ? (claim.FirstSeen, claim.LastSeen)
            : (claim.Season?.SeasonStart, claim.Season?.SeasonEnd);
        var account = await _context.XboxUsers.WithPastGamertags().FirstAsync(x => x.XboxUserID == xuid, ct);
        ThrowIfNotRecorded(account.RecordPastGamertag(claim.Gamertag, from, to, source));
    }

    /// <summary>
    /// The claim's placeholder becomes the account's user when nobody holds the account, else is merged into its owner
    /// (nothing when the owner is the placeholder).
    /// </summary>
    private async Task SettlePlaceholder(GamertagClaim claim, User? owner, int placeholderId, CancellationToken ct)
    {
        var claimId = claim.GamertagClaimID;
        if (owner?.Id != placeholderId)
        {
            // Only an import placeholder is linked or merged on a claim: not a real account, nor one tied to an Xbox
            // account since the claim was made
            // When nobody holds the account the placeholder becomes its user: an import placeholder is tied to none, so
            // that tie (User.TieToXbox) isn't refused either
            var placeholder = await _context.Users.FirstAsync(u => u.Id == placeholderId, ct);
            if (!placeholder.IsImportPlaceholder
                || (owner is null && claim.XboxUserID is long claimXuid && placeholder.TieToXbox(claimXuid) is not XboxTie.Tied))
                throw new GamertagClaimConflictException($"User {placeholderId} is no longer an unlinked placeholder (historical:<gamertag>); reject the claim or merge by hand.");
        }
        if (owner is null)
        {
            // Nobody holds the account: the placeholder was tied to it above
            await _context.SaveChangesAsync(ct);
        }
        else if (owner.Id != placeholderId)
        {
            // An Xbox account nobody signed up with holds its own placeholder: the import's one joins it
            var trigger = owner.IsDummyUser ? MergeTrigger.XboxPlaceholder : TriggerOf(claim.Kind);
            await _mergeService.Merge(owner.Id, placeholderId, MergeOptions.For(trigger, $"claim {claimId}"), ct);
        }
    }

    private static MergeTrigger TriggerOf(GamertagClaimKind kind) => kind switch
    {
        GamertagClaimKind.XuidLookup => MergeTrigger.XuidLookup,
        GamertagClaimKind.UserClaim => MergeTrigger.UserClaim,
        GamertagClaimKind.DiscordMatch => MergeTrigger.DiscordMatch,
        _ => MergeTrigger.ClaimApproval,
    };

    /// <summary>Rejects a pending claim; the reason, if given, is shown to the claimant.</summary>
    public async Task Reject(int claimId, string? reason = null, CancellationToken ct = default)
    {
        var claim = await _context.GamertagClaims.FirstOrDefaultAsync(c => c.GamertagClaimID == claimId, ct)
            ?? throw new GamertagClaimException($"There is no claim {claimId}");
        var (decidedBy, decidedAt) = Decider();
        ThrowIfDecided(claim.Reject(reason, decidedBy, decidedAt), claimId);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Who decides a claim now, and when: the signed-in sysadmin.</summary>
    private (int? By, DateTime At) Decider() => (_currentUser?.GetCurrentUserId(), _time.GetUtcNow().UtcDateTime);

    /// <summary>A claim decided already can't be decided again (400).</summary>
    private static void ThrowIfDecided(GamertagClaimDecision decision, int claimId)
    {
        if (decision is GamertagClaimDecision.AlreadyApproved or GamertagClaimDecision.AlreadyRejected)
            throw new GamertagClaimException($"Claim {claimId} was already {(decision is GamertagClaimDecision.AlreadyApproved ? "approved" : "rejected")}");
    }

    /// <summary>A past gamertag the account refused (400), as the request's checks said it.</summary>
    private static void ThrowIfNotRecorded(RecordPastGamertagResult result)
    {
        switch (result)
        {
            case RecordPastGamertagResult.NoGamertag:
                throw new GamertagClaimException("Give the gamertag");
            case RecordPastGamertagResult.DatesOutOfOrder:
                throw new GamertagClaimException("FirstSeen is after LastSeen");
        }
    }

    /// <summary>A gamertag a sysadmin knows the Xbox account had (Source Manual).</summary>
    public async Task AddHistory(AddGamertagHistoryRequest request, CancellationToken ct = default)
    {
        var account = await _context.XboxUsers.WithPastGamertags().FirstOrDefaultAsync(x => x.XboxUserID == request.XboxUserID, ct)
            ?? throw new GamertagClaimException($"There is no Xbox account {request.XboxUserID}; set it as someone's gamertag first");
        ThrowIfNotRecorded(account.RecordPastGamertag(request.Gamertag, request.FirstSeen, request.LastSeen, GamertagHistorySource.Manual, request.Note));
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
        var rows = await _context.XboxUsers.PastGamertags().Where(h => h.XboxUserID == xbox.XboxUserID)
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
                CanRemove = h.IsRemovable,
            }).ToList(),
        };
    }

    /// <summary>Removes a past gamertag a sysadmin added (Source Manual); the ones the app saw stay.</summary>
    public async Task RemoveHistory(int historyId, CancellationToken ct = default)
    {
        var account = await _context.XboxUsers.WithPastGamertags()
            .FirstOrDefaultAsync(x => x.PastGamertags.Any(h => h.XboxGamertagHistoryID == historyId), ct);
        switch (account?.RemovePastGamertag(historyId) ?? RemovePastGamertagResult.NotFound)
        {
            case RemovePastGamertagResult.NotFound:
                throw new GamertagClaimException($"There is no past gamertag {historyId}");
            case RemovePastGamertagResult.NotManual:
                throw new GamertagClaimException("Only a past gamertag added by hand can be removed");
        }
        await _context.SaveChangesAsync(ct);
    }
}
