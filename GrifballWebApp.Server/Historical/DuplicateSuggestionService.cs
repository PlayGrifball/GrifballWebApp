using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Domain.Services;
using GrifballWebApp.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

/// <summary>Why a pair was suggested, strongest first: the list is sorted by it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DuplicateMatch
{
    /// <summary>The same <see cref="HistoricalKeys.LooseKey"/>: case, spaces, punctuation or a "#1234" suffix apart.</summary>
    Exact,
    /// <summary>A few edits apart (<see cref="DuplicateSuggestionDto.Distance"/>, at most <see cref="DuplicateSuggestionService.Allowed"/>).</summary>
    Edits,
    /// <summary>The same name inside symmetric decoration (<see cref="HistoricalKeys.StripFraming"/>): "xX Name Xx" and "Name".</summary>
    Framing,
    /// <summary>One placeholder's whole name is a word of another placeholder's gamertag: "Flowerz" and "Its Rainin Flowerz".</summary>
    Token,
}

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
    public DuplicateMatch Match { get; set; }
    /// <summary>Edits between the two gamertags compared loosely (case, spaces, punctuation and a "#1234" suffix aside).
    /// Only <see cref="DuplicateMatch.Edits"/> is suggested for it; a framing or word match can be many edits apart.</summary>
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
/// gamertags are close (<see cref="DuplicateMatch"/>) and whose seasons don't conflict (<see cref="UserMergeService.ConflictAsync"/>).
/// Never merged automatically: a sysadmin merges (audited, DuplicateSuggestion) or dismisses (remembered).
/// </summary>
public partial class DuplicateSuggestionService
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

    /// <summary>The shortest word that anchors a <see cref="DuplicateMatch.Token"/> match: the first length
    /// <see cref="Allowed"/> gives any slack to, so a short name is never matched into a longer one.</summary>
    public const int MinAnchorLength = 5;

    /// <summary>Words too common in gamertags to say two players are one: never a word or framing anchor.</summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALPHA", "ANGEL", "AGENT", "ASSASSIN", "BEAST", "BLACK", "BLADE", "BLOOD", "BRUTE", "BULLET", "CAPTAIN", "CHAOS",
        "CHIEF", "CLUTCH", "CRAZY", "CRIMSON", "CYBER", "DARKNESS", "DEATH", "DELTA", "DEMON", "DRAGON", "EAGLE", "ELITE",
        "FALCON", "FLAME", "FROST", "GAMER", "GAMING", "GENERAL", "GHOST", "GOLDEN", "GREEN", "GRIFBALL", "GRUNT",
        "HAMMER", "HUNTER", "KILLER", "KNIGHT", "LEGEND", "LITTLE", "MAJOR", "MASTER", "MISTER", "MONKEY", "MONSTER",
        "NINJA", "OMEGA", "PHOENIX", "PIRATE", "PLAYER", "PRIME", "PRINCE", "QUEEN", "RANGER", "REAPER", "RIDER", "ROGUE",
        "SAVAGE", "SHADOW", "SIGMA", "SILENT", "SILVER", "SKULL", "SMOKE", "SNAKE", "SNIPER", "SOLDIER", "SPARTAN",
        "SPRING", "STORM", "STRIKER", "SUMMER", "SUPER", "SWORD", "THUNDER", "TIGER", "TITAN", "TOXIC", "ULTRA", "VENOM",
        "VIPER", "WARRIOR", "WHITE", "WINTER", "ZOMBIE",
    };

    /// <summary>Where a word ends inside one part: "MasterChief", "XMLName", "Spartan117" and "117Spartan" are two words each.</summary>
    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[a-zA-Z])(?=[0-9])|(?<=[0-9])(?=[a-zA-Z])")]
    private static partial Regex WordBoundaryPattern();

    private static readonly char[] WordDelimiters = [' ', '_', '-', '/', '(', ')', '[', ']', '.', '~', '*'];

    private static bool IsAnchor(string key) => key.Length >= MinAnchorLength && !StopWords.Contains(key);

    /// <summary>The words of a gamertag that could anchor a <see cref="DuplicateMatch.Token"/> match, as loose keys: each
    /// part between spaces or punctuation, and each word inside it, of <see cref="MinAnchorLength"/>+ letters and no stop word.</summary>
    public static List<string> ExtractTokens(string gamertag)
    {
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in gamertag.Split(WordDelimiters, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var word in WordBoundaryPattern().Split(part).Append(part))
            {
                var key = HistoricalKeys.LooseKey(word);
                if (IsAnchor(key))
                    words.Add(key);
            }
        }
        return [.. words];
    }

    private sealed record Candidate(int Id, bool IsPlaceholder, string Gamertag, string Key, string StrippedKey, IReadOnlyList<string> Tokens, bool HasXbox);

    private sealed record Pair(Candidate From, Candidate To, DuplicateMatch Match, int Distance);

    /// <summary>Edits between two keys however far apart, for a framing or word match.</summary>
    private static int FullDistance(string a, string b) => Distance(a, b, Math.Max(a.Length, b.Length))!.Value;

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
            .Select(u => new Candidate(u.Id, u.IsDummyUser, u.Gamertag!, HistoricalKeys.LooseKey(u.Gamertag!), HistoricalKeys.StripFraming(u.Gamertag!), ExtractTokens(u.Gamertag!), u.HasXbox))
            .Where(c => c.Key.Length > 0)
            .ToList();
        var dismissed = (await _context.DuplicateDismissals.AsNoTracking().Select(d => new { d.LowUserID, d.HighUserID }).ToListAsync(ct))
            .Select(d => (d.LowUserID, d.HighUserID)).ToHashSet();

        // Bucketed by key length: a pair more than two edits apart in length can't be close
        var byLength = candidates.GroupBy(c => c.Key.Length).ToDictionary(g => g.Key, g => g.ToList());
        var byStripped = candidates
            .Where(c => c.StrippedKey.Length >= HistoricalKeys.MinFramedKeyLength && !StopWords.Contains(c.StrippedKey))
            .ToLookup(c => c.StrippedKey);
        var byToken = candidates.SelectMany(c => c.Tokens.Select(t => (Token: t, Candidate: c))).ToLookup(x => x.Token, x => x.Candidate);
        var pairs = new List<Pair>();
        var seen = new HashSet<(int, int)>();

        void TryAddPair(Candidate placeholder, Candidate other, DuplicateMatch match, int distance)
        {
            if (placeholder.Id == other.Id || (placeholder.HasXbox && other.HasXbox))
                return;
            var pair = DuplicateDismissal.InOrder(placeholder.Id, other.Id);
            if (dismissed.Contains(pair) || !seen.Add(pair))
                return;
            // The placeholder goes into a signed-up user; of two placeholders, the newer goes into the older
            var (from, to) = other.IsPlaceholder && other.Id > placeholder.Id ? (other, placeholder) : (placeholder, other);
            pairs.Add(new Pair(from, to, match, distance));
        }

        foreach (var source in candidates.Where(c => c.IsPlaceholder))
        {
            var allowed = Allowed(source.Key.Length);
            for (var length = source.Key.Length - allowed; length <= source.Key.Length + allowed; length++)
            {
                if (!byLength.TryGetValue(length, out var bucket))
                    continue;
                foreach (var other in bucket)
                {
                    var distance = Distance(source.Key, other.Key, Math.Min(allowed, Allowed(other.Key.Length)));
                    if (distance is not null)
                        TryAddPair(source, other, distance == 0 ? DuplicateMatch.Exact : DuplicateMatch.Edits, distance.Value);
                }
            }

            // The same name inside symmetric decoration: "ii flowers ii" and "l Flowers l", "xX Quillon Xx" and "Quillon"
            foreach (var other in byStripped[source.StrippedKey])
            {
                if (other.Key != source.Key)
                    TryAddPair(source, other, DuplicateMatch.Framing, FullDistance(source.Key, other.Key));
            }

            // The placeholder's whole name as a word of exactly one other gamertag: "Flowerz" and "Its Rainin Flowerz".
            // Only between two placeholders with no Xbox account: one shared word is too little to merge history into a
            // real account, and a word that several gamertags share says nothing about which player it was.
            if (!source.HasXbox && IsAnchor(source.Key)
                && byToken[source.Key].Where(o => o.Key != source.Key).Take(2).ToList() is [{ IsPlaceholder: true, HasXbox: false } carrier])
                TryAddPair(source, carrier, DuplicateMatch.Token, FullDistance(source.Key, carrier.Key));
        }

        var checker = new UserMergeService(_context);
        var result = new List<DuplicateSuggestionDto>();
        // Strongest match first, so a word or framing match never crowds a real near-miss out of the cap; each pair's
        // seasons are checked as a merge would be: the cap keeps the page quick
        var ordered = pairs.OrderBy(p => p.Match).ThenBy(p => p.Distance).ThenBy(p => p.To.IsPlaceholder)
            .ThenBy(p => p.From.Gamertag, StringComparer.OrdinalIgnoreCase).Take(max * 4);
        foreach (var (from, to, match, distance) in ordered)
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
                Match = match,
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
        if (DuplicateDismissal.CheckPair(userId, otherUserId) == DuplicatePair.SameUser)
            throw new GamertagClaimException("A user is no duplicate of itself");
        if (await _context.DuplicateDismissals.AnyAsync(DuplicateDismissal.Of(userId, otherUserId), ct))
            return;
        _context.DuplicateDismissals.Add(DuplicateDismissal.Dismiss(userId, otherUserId, _time.GetUtcNow().UtcDateTime, _currentUser?.GetCurrentUserId()));
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>A placeholder's gamertag (<see cref="User.PlaceholderGamertagOf"/>), else its display name.</summary>
    public static string? PlaceholderGamertag(string? userName, string? displayName) => User.PlaceholderGamertagOf(userName) ?? displayName;

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
