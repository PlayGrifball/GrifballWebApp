using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Historical;

/// <summary>How a historical gamertag was matched to a user.</summary>
public enum ResolvedBy
{
    /// <summary>The Xbox account (XUID) of a Waypoint match row, linked to the user.</summary>
    Xuid,
    /// <summary>Sheet rows or titles imported before under the same gamertag (a re-run, or a placeholder merged since).</summary>
    EarlierRows,
    /// <summary>An Xbox account's gamertag history covering the season.</summary>
    GamertagHistory,
    /// <summary>An account's current gamertag, with no rename known after the season began.</summary>
    CurrentGamertag,
    /// <summary>A placeholder (historical:&lt;gamertag&gt;) made before.</summary>
    Placeholder,
    /// <summary>A placeholder made now.</summary>
    NewPlaceholder,
}

public readonly record struct Resolution(int UserId, ResolvedBy By);

/// <summary>
/// Finds the user a gamertag of a historical season belongs to, for when that season began (<c>windowStart</c>): a gamertag is matched to the account that had it then, never to an account only because it holds
/// the gamertag now. In order:
/// <list type="number">
/// <item>A Waypoint row's XUID: the user linked to that Xbox account.</item>
/// <item>The user of this season's sheet rows or titles imported before under that gamertag: a re-run (also after a
/// commissioner moved them to another user).</item>
/// <item>XboxGamertagHistory: an account that still had the gamertag when the season began (its LastSeen is not
/// before it), when no other account's history also can.</item>
/// <item>An account whose current gamertag it is, when no rename of that account is known after the season began.</item>
/// <item>The user of the archive's other seasons' sheet rows or titles under the gamertag (the same player, as far as
/// is known; a placeholder a commissioner merged into an account keeps being found).</item>
/// <item>The placeholder for the gamertag, by its user name.</item>
/// </list>
/// Otherwise it creates the placeholder: IsDummyUser, UserName "historical:{gamertag}" (unique, as NormalizedUserName is,
/// and a name no sign-up can take), DisplayName the gamertag, linked to the Xbox account when the row has one that no
/// user holds, so the player claims it by setting their gamertag.
/// <para>A gamertag the alias registry (<see cref="HistoricalAliases"/>) maps to a canonical handle is the same player under
/// either: its own gamertag is tried first, the canonical one only where its own finds nothing (history), or only with
/// evidence (an account holding the canonical gamertag now counts only when its own history has one of the aliases). When
/// one list of a season (a sheet's rows for a phase, a roster, a recorded game) has more than one gamertag that maps to the
/// same handle, they are different players (generic aliases can be): every gamertag of that handle in the season is
/// resolved by its own only (<see cref="SharedAliases"/>). A Waypoint row's XUID decides by itself, under the
/// gamertag the account used.</para>
/// </summary>
internal sealed class PlayerResolver
{
    private readonly GrifballContext _context;
    private readonly DateTime _windowStart;
    private readonly int _seasonId;
    private readonly int? _matchmakingSeasonId;
    /// <summary>Resolutions by the gamertag as given (normalised).</summary>
    private readonly Dictionary<string, Resolution> _byGamertag = new();
    /// <summary>Canonical handles (normalised) that two players of one of the season's lists map to, with their gamertags.</summary>
    private readonly Dictionary<string, List<string>> _shared = new();

    /// <param name="seasonId">The season being imported; 0 for a matchmaking season (<paramref name="matchmakingSeasonId"/>).</param>
    public PlayerResolver(GrifballContext context, int seasonId, DateTime windowStart, int? matchmakingSeasonId = null)
    {
        _context = context;
        _seasonId = seasonId;
        _matchmakingSeasonId = matchmakingSeasonId;
        _windowStart = windowStart;
    }

    public int DummiesCreated { get; private set; }
    /// <summary>Gamertags more than one account's history claims for the season: left to a placeholder.</summary>
    public List<string> Ambiguous { get; } = [];

    /// <summary>The handles two players of one of the season's lists map to, each resolved by their own gamertag: a line each.</summary>
    public IEnumerable<string> SharedAliases => _shared.Values
        .Select(tags => $"{string.Join(", ", tags)} map to the same player ({HistoricalAliases.CanonicalGamertag(tags[0])}) in the alias registry; each was resolved by its own gamertag");

    /// <summary>
    /// The season's lists of players (a sheet's rows for a phase, a roster, a recorded game's players), given before any
    /// is resolved: two gamertags of one list that map to the same canonical handle are two players, so every gamertag of
    /// that handle is resolved by its own (one player under two spellings in different lists is still one).
    /// </summary>
    public void Expect(IEnumerable<IEnumerable<string>> lists)
    {
        foreach (var list in lists)
        {
            var byHandle = list.Where(g => !string.IsNullOrWhiteSpace(g)).Select(HistoricalKeys.CleanGamertag)
                .DistinctBy(HistoricalKeys.Normalize)
                .GroupBy(g => HistoricalKeys.Normalize(HistoricalAliases.CanonicalGamertag(g)))
                .Where(g => g.Count() > 1);
            foreach (var handle in byHandle)
            {
                if (!_shared.TryGetValue(handle.Key, out var tags))
                    _shared[handle.Key] = tags = [];
                tags.AddRange(handle.Where(t => !tags.Contains(t, StringComparer.OrdinalIgnoreCase)).ToList());
            }
        }
    }

    public async Task<Resolution> Resolve(string rawGamertag, long? xboxUserId, CancellationToken ct)
    {
        var gamertag = HistoricalKeys.CleanGamertag(rawGamertag);
        var normalized = HistoricalKeys.Normalize(gamertag);

        // The XUID is who the player is: no alias changes that, nor the name of the placeholder made for it
        if (xboxUserId is not null)
            return await ResolveXuid(gamertag, xboxUserId.Value, ct);

        if (_byGamertag.TryGetValue(normalized, out var cached))
            return cached;

        // Two players of one list map to the same handle: by its own gamertag only
        var shared = _shared.ContainsKey(HistoricalKeys.Normalize(HistoricalAliases.CanonicalGamertag(gamertag)));
        var canonical = shared ? gamertag : HistoricalAliases.CanonicalGamertag(gamertag);
        var found = await Find(gamertag, normalized, canonical, ct);
        var resolution = found ?? new Resolution(await CreateDummy(canonical, null, ct), ResolvedBy.NewPlaceholder);
        _byGamertag[normalized] = resolution;
        return resolution;
    }

    /// <summary>
    /// A Waypoint row: the XUID's user. An XUID nobody holds takes the placeholder of a gamertag its history (or its
    /// current gamertag, under the same date rule) gives for the season, or a new placeholder linked to it.
    /// </summary>
    private async Task<Resolution> ResolveXuid(string gamertag, long xboxUserId, CancellationToken ct)
    {
        var xboxUser = await _context.XboxUsers.Include(x => x.User).FirstOrDefaultAsync(x => x.XboxUserID == xboxUserId, ct);
        if (xboxUser?.User is not null)
            return new Resolution(xboxUser.User.Id, ResolvedBy.Xuid);

        var history = await _context.XboxGamertagHistory.Where(h => h.XboxUserID == xboxUserId).ToListAsync(ct);
        var tags = history.Where(Overlaps).Select(h => h.Gamertag).ToList();
        var renamed = LastRename(history);
        if (xboxUser is not null && (renamed is null || renamed <= _windowStart))
            tags.Add(xboxUser.Gamertag);

        foreach (var tag in tags.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var placeholder = await PlaceholderOf(tag, ct);
            if (placeholder is not null && placeholder.XboxUserID is null)
            {
                placeholder.XboxUserID = xboxUserId;
                await _context.SaveChangesAsync(ct);
                return new Resolution(placeholder.Id, ResolvedBy.Placeholder);
            }
        }
        return new Resolution(await CreateDummy(gamertag, xboxUser, ct), ResolvedBy.NewPlaceholder);
    }

    /// <param name="canonical">The canonical handle of the gamertag; the gamertag itself when it has none, or when another
    /// gamertag of the season maps to it too.</param>
    private async Task<Resolution?> Find(string gamertag, string normalized, string canonical, CancellationToken ct)
    {
        var aliased = !string.Equals(canonical, gamertag, StringComparison.OrdinalIgnoreCase);
        var canonicalNormalized = HistoricalKeys.Normalize(canonical);

        // A re-run of this season: whoever its rows under the gamertag are (also after a commissioner moved them)
        var sameSeason = await EarlierRows(gamertag, canonical, thisSeason: true, ct);
        if (sameSeason is not null)
            return new Resolution(sameSeason.Value, ResolvedBy.EarlierRows);

        // The gamertag's own history first: the canonical handle's only when no account's history has the gamertag
        var (fromHistory, anyAccount) = await FromHistory(gamertag, normalized, gamertag, ct);
        if (fromHistory is null && !anyAccount && aliased)
            (fromHistory, _) = await FromHistory(canonical, canonicalNormalized, gamertag, ct);
        if (fromHistory is not null)
            return fromHistory;

        // An account whose gamertag it is now; one that holds the canonical handle only when its history has an alias too
        var current = await CurrentHolder(gamertag, normalized, evidence: null, ct);
        if (current is null && aliased)
        {
            var aliases = HistoricalAliases.GetAliasesFor(canonical).Select(HistoricalKeys.Normalize).Append(normalized).ToList();
            current = await CurrentHolder(canonical, canonicalNormalized, aliases, ct);
        }
        if (current is not null)
            return current;

        // No account had it then, as far as is known: the archive's other seasons under the gamertag are the same player
        var otherSeasons = await EarlierRows(gamertag, canonical, thisSeason: false, ct);
        if (otherSeasons is not null)
            return new Resolution(otherSeasons.Value, ResolvedBy.EarlierRows);

        var placeholder = await PlaceholderOf(canonical, ct);
        if (placeholder is null && aliased)
            placeholder = await PlaceholderOf(gamertag, ct);

        return placeholder is null ? null : new Resolution(placeholder.Id, ResolvedBy.Placeholder);
    }

    /// <summary>
    /// The account whose current gamertag it is, when no rename of it is known after the season began. Compared normalised
    /// (trimmed, inner whitespace collapsed; case by collation and upper-casing): the spaces are taken out in SQL to find
    /// the candidates, then the normalised forms compared. With <paramref name="evidence"/>, only an account whose own
    /// gamertag history has one of those gamertags (normalised).
    /// </summary>
    private async Task<Resolution?> CurrentHolder(string gamertag, string normalized, List<string>? evidence, CancellationToken ct)
    {
        var compact = gamertag.Replace(" ", "");
        var candidates = (await _context.XboxUsers.Where(x => x.Gamertag.Replace(" ", "") == compact && x.User != null)
                .Select(x => new { x.XboxUserID, x.Gamertag, UserId = x.User!.Id }).ToListAsync(ct))
            .Where(x => HistoricalKeys.Normalize(x.Gamertag) == normalized)
            .OrderBy(x => x.XboxUserID);
        foreach (var candidate in candidates)
        {
            var history = await _context.XboxGamertagHistory.Where(h => h.XboxUserID == candidate.XboxUserID).ToListAsync(ct);
            if (evidence is not null && !history.Any(h => evidence.Contains(h.NormalizedGamertag)))
                continue;
            var renamed = LastRename(history);
            return renamed is null || renamed <= _windowStart ? new Resolution(candidate.UserId, ResolvedBy.CurrentGamertag) : null;
        }
        return null;
    }

    /// <summary>The user of the sheet rows or titles under the gamertag (or its canonical handle), in the seasons chosen.</summary>
    private async Task<int?> EarlierRows(string gamertag, string canonical, bool thisSeason, CancellationToken ct)
    {
        var fromStats = await _context.PlayerSeasonStats
            .Where(s => (s.Gamertag == gamertag || s.Gamertag == canonical) && (s.SeasonID == _seasonId) == thisSeason)
            .OrderBy(s => s.PlayerSeasonStatID).Select(s => (int?)s.UserID).FirstOrDefaultAsync(ct);
        if (fromStats is not null)
            return fromStats;
        var fromMatchmaking = await _context.MatchmakingPlayerStats
            .Where(s => (s.Gamertag == gamertag || s.Gamertag == canonical) && (s.MatchmakingSeasonID == _matchmakingSeasonId) == thisSeason)
            .OrderBy(s => s.MatchmakingPlayerStatID).Select(s => (int?)s.UserID).FirstOrDefaultAsync(ct);
        if (fromMatchmaking is not null)
            return fromMatchmaking;
        return await _context.PlayerChampionships
            .Where(c => (c.GamertagUsed == gamertag || c.GamertagUsed == canonical) && (c.SeasonID == _seasonId) == thisSeason)
            .OrderBy(c => c.PlayerChampionshipID).Select(c => (int?)c.UserID).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// The account whose history has the gamertag at a time that can include the season (see <see cref="Overlaps"/>).
    /// When more than one account's history can (a gamertag reused after its owner let go of it), none is chosen, and the
    /// sheet's gamertag (<paramref name="sheetGamertag"/>) is reported. AnyAccount: whether any account's history had it.
    /// </summary>
    private async Task<(Resolution? Found, bool AnyAccount)> FromHistory(string gamertag, string normalized, string sheetGamertag, CancellationToken ct)
    {
        var rows = await _context.XboxGamertagHistory.Where(h => h.NormalizedGamertag == normalized)
            .Include(h => h.XboxUser).ThenInclude(x => x.User)
            .ToListAsync(ct);
        var candidates = rows.Where(Overlaps).ToList();
        var accounts = candidates.Select(h => h.XboxUserID).Distinct().ToList();
        if (accounts.Count == 0)
            return (null, false);
        if (accounts.Count > 1)
        {
            Ambiguous.Add(sheetGamertag);
            return (null, true);
        }
        var xboxUser = candidates.First(h => h.XboxUserID == accounts[0]).XboxUser;
        if (xboxUser.User is not null)
            return (new Resolution(xboxUser.User.Id, ResolvedBy.GamertagHistory), true);
        var placeholder = await PlaceholderOf(gamertag, ct);
        if (placeholder is not null && placeholder.XboxUserID is null)
        {
            placeholder.XboxUserID = xboxUser.XboxUserID;
            await _context.SaveChangesAsync(ct);
            return (new Resolution(placeholder.Id, ResolvedBy.GamertagHistory), true);
        }
        return (new Resolution(await CreateDummy(gamertag, xboxUser, ct), ResolvedBy.GamertagHistory), true);
    }

    /// <summary>
    /// Whether the account may have had the gamertag during the season: it hadn't let go of it before the season began.
    /// Only LastSeen bounds the period: FirstSeen is when the app first saw the account with it (a rename noticed, a row
    /// version, an approved season), not when the account took it, and archive seasons are usually older.
    /// </summary>
    private bool Overlaps(XboxGamertagHistory h) => GamertagMatching.Covers(h, _windowStart);

    /// <summary>When the account last changed gamertag, as far as its history knows.</summary>
    private static DateTime? LastRename(List<XboxGamertagHistory> history)
        => history.Where(h => h.LastSeen is not null).Max(h => h.LastSeen);

    private Task<User?> PlaceholderOf(string gamertag, CancellationToken ct) => new GamertagMatching(_context).PlaceholderAsync(gamertag, untiedOnly: false, ct);

    /// <summary>The gamertag's placeholder, made the one way placeholders are (<see cref="GamertagMatching.CreatePlaceholderAsync"/>):
    /// an Xbox account the gamertag's placeholder isn't tied to (it is another's, or the dates didn't tie it to this one)
    /// gets a placeholder of its own, named after its XUID too.</summary>
    private async Task<int> CreateDummy(string gamertag, XboxUser? xboxUser, CancellationToken ct)
    {
        var (user, created) = await new GamertagMatching(_context).CreatePlaceholderAsync(gamertag, xboxUser, ct);
        if (created)
            DummiesCreated++;
        return user.Id;
    }
}
