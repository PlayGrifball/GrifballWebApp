using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace GrifballWebApp.Server.Historical;

public class UserClaimOptions
{
    /// <summary>Claims of past gamertags one user may submit in a day.</summary>
    public int SubmissionsPerDay { get; set; } = 5;
}

/// <summary>A placeholder a gamertag claim would merge, and the seasons it played, before the claim is made.</summary>
public class ClaimPreviewDto
{
    public required string Gamertag { get; set; }
    /// <summary>Null when no placeholder has the gamertag: nothing to claim.</summary>
    public int? PlaceholderUserID { get; set; }
    public string? PlaceholderName { get; set; }
    /// <summary>"Name (year)", oldest first.</summary>
    public List<string> Seasons { get; set; } = [];
    /// <summary>Why it can't be claimed; null when it can.</summary>
    public string? Refusal { get; set; }
}

public class SubmitGamertagClaimRequest
{
    [Required]
    [MaxLength(50)]
    public string Gamertag { get; set; } = "";
    public DateTime? FirstSeen { get; set; }
    public DateTime? LastSeen { get; set; }
}

/// <summary>
/// A user's claims of their own past gamertags (owner decision E): the placeholder of a gamertag they used, which a
/// sysadmin approves on the claims page (GamertagClaimService.Approve merges it into them, audited as UserClaim). One
/// pending claim per user and gamertag, never a gamertag another real user's Xbox account has or had, and a few a day.
/// </summary>
public class UserGamertagClaimService
{
    private readonly GrifballContext _context;
    private readonly GamertagMatching _matching;
    private readonly TimeProvider _time;
    private readonly UserClaimOptions _options;

    public UserGamertagClaimService(GrifballContext context, GamertagMatching matching, TimeProvider time, IOptions<UserClaimOptions> options)
    {
        _context = context;
        _matching = matching;
        _time = time;
        _options = options.Value;
    }

    /// <summary>What claiming <paramref name="gamertag"/> would merge, with the reason it can't be claimed if any.</summary>
    public async Task<ClaimPreviewDto> Preview(int userId, string gamertag, CancellationToken ct = default)
    {
        var clean = HistoricalKeys.CleanGamertag(gamertag ?? "");
        var preview = new ClaimPreviewDto { Gamertag = clean };
        if (clean.Length == 0)
        {
            preview.Refusal = "Give the gamertag";
            return preview;
        }
        var placeholder = await _matching.PlaceholderAsync(clean, untiedOnly: true, ct);
        if (placeholder is not null)
        {
            preview.PlaceholderUserID = placeholder.Id;
            preview.PlaceholderName = placeholder.DisplayName ?? placeholder.UserName;
            preview.Seasons = await SeasonsOf(placeholder.Id, ct);
        }
        preview.Refusal = await RefusalAsync(userId, clean, placeholder, ct);
        return preview;
    }

    /// <summary>Makes a pending <see cref="GamertagClaimKind.UserClaim"/>; throws <see cref="GamertagClaimException"/> with the reason when it can't.</summary>
    public async Task<GamertagClaimDto> Submit(int userId, SubmitGamertagClaimRequest request, CancellationToken ct = default)
    {
        var clean = HistoricalKeys.CleanGamertag(request.Gamertag ?? "");
        if (clean.Length == 0)
            throw new GamertagClaimException("Give the gamertag");
        if (request.FirstSeen is not null && request.LastSeen is not null && request.FirstSeen > request.LastSeen)
            throw new GamertagClaimException("The start of the period is after its end");

        var now = _time.GetUtcNow().UtcDateTime;
        var since = now.AddDays(-1);
        var today = await _context.GamertagClaims.CountAsync(c => c.ClaimantUserID == userId && c.Kind == GamertagClaimKind.UserClaim && c.CreatedAt > since, ct);
        if (today >= _options.SubmissionsPerDay)
            throw new GamertagClaimException($"You can make {_options.SubmissionsPerDay} claims a day; try again tomorrow");

        var placeholder = await _matching.PlaceholderAsync(clean, untiedOnly: true, ct);
        if (await RefusalAsync(userId, clean, placeholder, ct) is { } refusal)
            throw new GamertagClaimException(refusal);

        var claimant = await _context.Users.FirstAsync(u => u.Id == userId, ct);
        var claim = new GamertagClaim
        {
            Kind = GamertagClaimKind.UserClaim,
            ClaimantUserID = userId,
            XboxUserID = claimant.XboxUserID,
            Gamertag = clean,
            NormalizedGamertag = HistoricalKeys.Normalize(clean),
            PlaceholderUserID = placeholder!.Id,
            FirstSeen = request.FirstSeen,
            LastSeen = request.LastSeen,
            // The audit interceptor stamps it too; set here so the daily limit counts it wherever it is saved
            CreatedAt = now,
        };
        _context.GamertagClaims.Add(claim);
        await _context.SaveChangesAsync(ct);
        return (await ClaimsOf(userId, ct)).First(c => c.GamertagClaimID == claim.GamertagClaimID);
    }

    /// <summary>The user's claims of past gamertags, newest first, with each status and rejection reason.</summary>
    public async Task<List<GamertagClaimDto>> ClaimsOf(int userId, CancellationToken ct = default)
        => await GamertagClaimService.Project(_context.GamertagClaims.AsNoTracking()
                .Where(c => c.ClaimantUserID == userId && c.Kind == GamertagClaimKind.UserClaim)
                .OrderByDescending(c => c.GamertagClaimID))
            .ToListAsync(ct);

    /// <summary>Why the user can't claim the gamertag, or null.</summary>
    private async Task<string?> RefusalAsync(int userId, string gamertag, User? placeholder, CancellationToken ct)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || user.IsDummyUser)
            return "Only a signed-up user can claim a gamertag";
        var normalized = HistoricalKeys.Normalize(gamertag);
        var compact = gamertag.Replace(" ", "");

        // Another real user's Xbox account has it now, or had it
        var current = (await _context.XboxUsers.AsNoTracking()
                .Where(x => x.Gamertag.Replace(" ", "") == compact)
                .Select(x => new { x.Gamertag, Holder = _context.Users.Where(u => u.XboxUserID == x.XboxUserID).Select(u => new { u.Id, u.IsDummyUser }).FirstOrDefault() })
                .ToListAsync(ct))
            .Where(x => HistoricalKeys.Normalize(x.Gamertag) == normalized);
        if (current.Any(x => x.Holder is not null && !x.Holder.IsDummyUser && x.Holder.Id != userId))
            return "That gamertag belongs to another user's Xbox account. Contact a sysadmin if you believe this is wrong.";
        var past = await _context.XboxGamertagHistory.AsNoTracking()
            .Where(h => h.NormalizedGamertag == normalized)
            .Select(h => _context.Users.Where(u => u.XboxUserID == h.XboxUserID).Select(u => new { u.Id, u.IsDummyUser }).FirstOrDefault())
            .ToListAsync(ct);
        if (past.Any(h => h is not null && !h.IsDummyUser && h.Id != userId))
            return "That gamertag was another user's Xbox account's. Contact a sysadmin if you believe this is wrong.";

        if (await _context.GamertagClaims.AnyAsync(c => c.ClaimantUserID == userId && c.Kind == GamertagClaimKind.UserClaim
                && c.NormalizedGamertag == normalized && c.Status != GamertagClaimStatus.Rejected, ct))
            return "You have claimed that gamertag already";
        if (placeholder is null)
            return "No past records use that gamertag";
        return null;
    }

    private async Task<List<string>> SeasonsOf(int placeholderId, CancellationToken ct)
        => (await _context.PlayerSeasonStats.AsNoTracking().Where(s => s.UserID == placeholderId)
                .Select(s => new { s.Season.SeasonName, s.Season.SeasonStart })
                .Union(_context.PlayerChampionships.AsNoTracking().Where(c => c.UserID == placeholderId)
                    .Select(c => new { c.Season.SeasonName, c.Season.SeasonStart }))
                .ToListAsync(ct))
            .OrderBy(s => s.SeasonStart).Select(s => $"{s.SeasonName} ({s.SeasonStart.Year})").Distinct().ToList();
}
