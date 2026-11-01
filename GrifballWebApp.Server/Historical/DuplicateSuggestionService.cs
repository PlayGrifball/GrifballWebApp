using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Database.Services;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

/// <summary>A placeholder that may be the same player as another user: merge it, or dismiss the pair.</summary>
public class DuplicateSuggestionDto
{
    /// <summary>The placeholder, merged on "Merge".</summary>
    public int SourceUserID { get; set; }
    public required string SourceGamertag { get; set; }
    /// <summary>The user it would be merged into: a signed-up user, or another placeholder.</summary>
    public int TargetUserID { get; set; }
    public required string TargetGamertag { get; set; }
    public bool TargetIsPlaceholder { get; set; }
    /// <summary>Edits between the two gamertags compared loosely (case, spaces, punctuation and a "#1234" suffix aside).</summary>
    public int Distance { get; set; }
    public List<GrifballWebApp.Server.Seasons.SeasonRefDto> SourceSeasons { get; set; } = [];
    public List<GrifballWebApp.Server.Seasons.SeasonRefDto> TargetSeasons { get; set; } = [];
}

public class DuplicatePairRequest
{
    public int SourceUserID { get; set; }
    public int TargetUserID { get; set; }
}

/// <summary>
/// "Possible duplicate" pairs for the claims page: a placeholder and another user (signed-up, or a placeholder) whose
/// gamertags are close (<see cref="HistoricalKeys.LooseKey"/> equal, e.g. a "#1234" suffix, or a small edit distance
/// scaled to the gamertag's length) and whose seasons don't conflict (<see cref="UserMergeService.ConflictAsync"/>).
/// Never merged automatically: a sysadmin merges (audited, DuplicateSuggestion) or dismisses (remembered).
/// </summary>
public class DuplicateSuggestionService
{
    private readonly GrifballContext _context;
    private readonly IUserMergeService _merges;
    private readonly TimeProvider _time;
    private readonly ICurrentUserService? _currentUser;

    public DuplicateSuggestionService(GrifballContext context, IUserMergeService merges, TimeProvider time, ICurrentUserService? currentUser = null)
    {
        _context = context;
        _merges = merges;
        _time = time;
        _currentUser = currentUser;
    }

    /// <summary>Edits allowed between two loose keys of this length: none for short gamertags (one letter changes a
    /// short name into another's), one up to 7, two from 8.</summary>
    public static int Allowed(int length) => length <= 4 ? 0 : length <= 7 ? 1 : 2;

    private sealed record Candidate(int Id, bool IsPlaceholder, string Gamertag, string Key, bool HasXbox);

    public async Task<List<DuplicateSuggestionDto>> List(int max = 50, CancellationToken ct = default)
    {
        max = Math.Clamp(max, 1, 200);
        var users = await _context.Users.AsNoTracking()
            .Where(u => u.IsDummyUser || u.XboxUserID != null)
            .Select(u => new { u.Id, u.IsDummyUser, u.UserName, u.DisplayName, u.XboxUserID, Gamertag = u.XboxUser != null ? u.XboxUser.Gamertag : null })
            .ToListAsync(ct);
        var candidates = users
            .Select(u => new { u.Id, u.IsDummyUser, HasXbox = u.XboxUserID != null, Gamertag = u.IsDummyUser ? PlaceholderGamertag(u.UserName, u.DisplayName) : u.Gamertag })
            .Where(u => !string.IsNullOrWhiteSpace(u.Gamertag))
            .Select(u => new Candidate(u.Id, u.IsDummyUser, u.Gamertag!, HistoricalKeys.LooseKey(u.Gamertag!), u.HasXbox))
            .Where(c => c.Key.Length > 0)
            .ToList();
        var dismissed = (await _context.DuplicateDismissals.AsNoTracking().Select(d => new { d.LowUserID, d.HighUserID }).ToListAsync(ct))
            .Select(d => (d.LowUserID, d.HighUserID)).ToHashSet();

        // Bucketed by key length: a pair more than two edits apart in length can't be close
        var byLength = candidates.GroupBy(c => c.Key.Length).ToDictionary(g => g.Key, g => g.ToList());
        var pairs = new List<(Candidate Source, Candidate Target, int Distance)>();
        var seen = new HashSet<(int, int)>();
        foreach (var source in candidates.Where(c => c.IsPlaceholder))
        {
            var allowed = Allowed(source.Key.Length);
            for (var length = source.Key.Length - allowed; length <= source.Key.Length + allowed; length++)
            {
                if (!byLength.TryGetValue(length, out var bucket))
                    continue;
                foreach (var other in bucket)
                {
                    // Two Xbox accounts are two players (a merge would also drop one XUID)
                    if (other.Id == source.Id || (other.HasXbox && source.HasXbox))
                        continue;
                    var pair = (Math.Min(source.Id, other.Id), Math.Max(source.Id, other.Id));
                    if (dismissed.Contains(pair) || seen.Contains(pair))
                        continue;
                    var distance = Distance(source.Key, other.Key, Math.Min(allowed, Allowed(other.Key.Length)));
                    if (distance is null)
                        continue;
                    seen.Add(pair);
                    // Into the signed-up user; of two placeholders, the newer goes into the older
                    var (from, to) = other.IsPlaceholder && other.Id > source.Id ? (other, source) : (source, other);
                    pairs.Add((from, to, distance.Value));
                }
            }
        }

        var checker = new UserMergeService(_context);
        var result = new List<DuplicateSuggestionDto>();
        // Each pair's seasons are checked as a merge would be: a cap keeps the page quick
        foreach (var (from, to, distance) in pairs.OrderBy(p => p.Distance).ThenBy(p => p.Target.IsPlaceholder).ThenBy(p => p.Source.Gamertag, StringComparer.OrdinalIgnoreCase).Take(max * 4))
        {
            if (result.Count >= max)
                break;
            if (await checker.ConflictAsync(to.Id, from.Id, ct) is not null)
                continue;
            result.Add(new DuplicateSuggestionDto
            {
                SourceUserID = from.Id,
                SourceGamertag = from.Gamertag,
                TargetUserID = to.Id,
                TargetGamertag = to.Gamertag,
                TargetIsPlaceholder = to.IsPlaceholder,
                Distance = distance,
            });
        }

        var ids = result.SelectMany(r => new[] { r.SourceUserID, r.TargetUserID }).Distinct().ToList();
        var played = await GrifballWebApp.Server.Seasons.SeasonRefDto.PlayedByAsync(_context, ids, ct);
        foreach (var r in result)
        {
            r.SourceSeasons = played.GetValueOrDefault(r.SourceUserID) ?? [];
            r.TargetSeasons = played.GetValueOrDefault(r.TargetUserID) ?? [];
        }
        return result;
    }

    /// <summary>Merges the placeholder into the other user (audited, DuplicateSuggestion).</summary>
    public async Task Merge(int sourceUserId, int targetUserId, CancellationToken ct = default)
    {
        var source = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == sourceUserId, ct)
            ?? throw new UserNotFoundException(sourceUserId);
        if (!source.IsDummyUser)
            throw new GamertagClaimException("Only a placeholder is merged from a suggestion; merge signed-up users on the merge page");
        await _merges.Merge(targetUserId, sourceUserId, MergeOptions.For(MergeTrigger.DuplicateSuggestion, "possible duplicate"), ct);
    }

    /// <summary>Remembers that the two are different players: the pair isn't suggested again.</summary>
    public async Task Dismiss(int userId, int otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId)
            throw new GamertagClaimException("A user is no duplicate of itself");
        var (low, high) = (Math.Min(userId, otherUserId), Math.Max(userId, otherUserId));
        if (await _context.DuplicateDismissals.AnyAsync(d => d.LowUserID == low && d.HighUserID == high, ct))
            return;
        _context.DuplicateDismissals.Add(new DuplicateDismissal
        {
            LowUserID = low,
            HighUserID = high,
            DismissedAt = _time.GetUtcNow().UtcDateTime,
            DismissedByUserID = _currentUser?.GetCurrentUserId(),
        });
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>A placeholder's gamertag: its user name without "historical:" (and an XUID after it), else its display name.</summary>
    public static string? PlaceholderGamertag(string? userName, string? displayName)
    {
        if (userName is not null && userName.StartsWith(HistoricalKeys.DummyUserNamePrefix, StringComparison.OrdinalIgnoreCase))
            return userName[HistoricalKeys.DummyUserNamePrefix.Length..].Split(':')[0];
        return displayName;
    }

    /// <summary>Levenshtein distance when it is at most <paramref name="max"/>, else null; stops early once it can't be.</summary>
    public static int? Distance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max)
            return null;
        if (a == b)
            return 0;
        if (max == 0)
            return null;
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (rowMin > max)
                return null;
            (previous, current) = (current, previous);
        }
        return previous[b.Length] <= max ? previous[b.Length] : null;
    }
}
