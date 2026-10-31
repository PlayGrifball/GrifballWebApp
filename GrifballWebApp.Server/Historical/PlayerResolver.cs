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
/// Finds the user a gamertag of a historical season belongs to, for that season's dates (<c>windowStart</c> to
/// <c>windowEnd</c>): a gamertag is matched to the account that had it then, never to an account only because it holds
/// the gamertag now. In order:
/// <list type="number">
/// <item>A Waypoint row's XUID: the user linked to that Xbox account.</item>
/// <item>The user of this season's sheet rows (Source Manual) or titles imported before under that gamertag: a re-run
/// (also after a commissioner moved them to another user). Waypoint rows don't count: their gamertag is the
/// account's current one, not the one the season used.</item>
/// <item>XboxGamertagHistory: an account that had the gamertag at a time overlapping the season (or at an unknown
/// time, when no other account's history also claims it).</item>
/// <item>An account whose current gamertag it is, when no rename of that account is known after the season began.</item>
/// <item>The user of the archive's other seasons' sheet rows or titles under the gamertag (the same player, as far as
/// is known; a placeholder a commissioner merged into an account keeps being found).</item>
/// <item>The placeholder for the gamertag, by its user name.</item>
/// </list>
/// Otherwise it creates the placeholder: IsDummyUser, UserName "historical:{gamertag}" (unique, as NormalizedUserName is,
/// and a name no sign-up can take), DisplayName the gamertag, linked to the Xbox account when the row has one that no
/// user holds, so the player claims it by setting their gamertag.
/// </summary>
internal sealed class PlayerResolver
{
    private readonly GrifballContext _context;
    private readonly DateTime _windowStart;
    private readonly DateTime _windowEnd;
    private readonly int _seasonId;
    private readonly Dictionary<string, Resolution> _byGamertag = new();

    public PlayerResolver(GrifballContext context, int seasonId, DateTime windowStart, DateTime windowEnd)
    {
        _context = context;
        _seasonId = seasonId;
        _windowStart = windowStart;
        _windowEnd = windowEnd;
    }

    public int DummiesCreated { get; private set; }
    /// <summary>Gamertags more than one account's history claims for the season: left to a placeholder.</summary>
    public List<string> Ambiguous { get; } = [];

    public async Task<Resolution> Resolve(string rawGamertag, long? xboxUserId, CancellationToken ct)
    {
        var gamertag = HistoricalKeys.CleanGamertag(rawGamertag);
        var normalized = HistoricalKeys.Normalize(gamertag);

        if (xboxUserId is not null)
            return await ResolveXuid(gamertag, xboxUserId.Value, ct);

        if (_byGamertag.TryGetValue(normalized, out var cached))
            return cached;
        var found = await Find(gamertag, normalized, ct);
        var resolution = found ?? new Resolution(await CreateDummy(gamertag, null, ct), ResolvedBy.NewPlaceholder);
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

    private async Task<Resolution?> Find(string gamertag, string normalized, CancellationToken ct)
    {
        // A re-run of this season: whoever its rows under the gamertag are (also after a commissioner moved them)
        var thisSeason = await EarlierRows(gamertag, s => s == _seasonId, ct);
        if (thisSeason is not null)
            return new Resolution(thisSeason.Value, ResolvedBy.EarlierRows);

        var fromHistory = await FromHistory(gamertag, normalized, ct);
        if (fromHistory is not null)
            return fromHistory;

        var current = await _context.XboxUsers.Where(x => x.Gamertag == gamertag && x.User != null)
            .Select(x => new { x.XboxUserID, UserId = x.User!.Id }).FirstOrDefaultAsync(ct);
        if (current is not null)
        {
            var history = await _context.XboxGamertagHistory.Where(h => h.XboxUserID == current.XboxUserID).ToListAsync(ct);
            var renamed = LastRename(history);
            if (renamed is null || renamed <= _windowStart)
                return new Resolution(current.UserId, ResolvedBy.CurrentGamertag);
        }

        // No account had it then, as far as is known: the archive's other seasons under the gamertag are the same player
        var otherSeasons = await EarlierRows(gamertag, s => s != _seasonId, ct);
        if (otherSeasons is not null)
            return new Resolution(otherSeasons.Value, ResolvedBy.EarlierRows);

        var placeholder = await PlaceholderOf(gamertag, ct);
        return placeholder is null ? null : new Resolution(placeholder.Id, ResolvedBy.Placeholder);
    }

    /// <summary>The user of the sheet rows (Source Manual) or titles under the gamertag, in the seasons chosen. Waypoint rows
    /// don't count: their gamertag is the account's current one, not the one the season used.</summary>
    private async Task<int?> EarlierRows(string gamertag, Func<int, bool> inSeason, CancellationToken ct)
    {
        var stats = await _context.PlayerSeasonStats.Where(s => s.Gamertag == gamertag && s.Source == StatSource.Manual)
            .OrderBy(s => s.PlayerSeasonStatID).Select(s => new { s.SeasonID, s.UserID }).ToListAsync(ct);
        var fromStats = stats.FirstOrDefault(s => inSeason(s.SeasonID));
        if (fromStats is not null)
            return fromStats.UserID;
        var titles = await _context.PlayerChampionships.Where(c => c.GamertagUsed == gamertag)
            .OrderBy(c => c.PlayerChampionshipID).Select(c => new { c.SeasonID, c.UserID }).ToListAsync(ct);
        return titles.FirstOrDefault(c => inSeason(c.SeasonID))?.UserID;
    }

    /// <summary>
    /// The account whose history has the gamertag at a time overlapping the season. Dated rows decide; undated rows
    /// count only when no dated one matches, and only when they all are one account's.
    /// </summary>
    private async Task<Resolution?> FromHistory(string gamertag, string normalized, CancellationToken ct)
    {
        var rows = await _context.XboxGamertagHistory.Where(h => h.NormalizedGamertag == normalized)
            .Include(h => h.XboxUser).ThenInclude(x => x.User)
            .ToListAsync(ct);
        var candidates = rows.Where(Overlaps).ToList();
        var dated = candidates.Where(h => h.FirstSeen is not null || h.LastSeen is not null).Select(h => h.XboxUserID).Distinct().ToList();
        var accounts = dated.Count > 0 ? dated : candidates.Select(h => h.XboxUserID).Distinct().ToList();
        if (accounts.Count == 0)
            return null;
        if (accounts.Count > 1)
        {
            Ambiguous.Add(gamertag);
            return null;
        }
        var xboxUser = candidates.First(h => h.XboxUserID == accounts[0]).XboxUser;
        if (xboxUser.User is not null)
            return new Resolution(xboxUser.User.Id, ResolvedBy.GamertagHistory);
        var placeholder = await PlaceholderOf(gamertag, ct);
        if (placeholder is not null && placeholder.XboxUserID is null)
        {
            placeholder.XboxUserID = xboxUser.XboxUserID;
            await _context.SaveChangesAsync(ct);
            return new Resolution(placeholder.Id, ResolvedBy.GamertagHistory);
        }
        return new Resolution(await CreateDummy(gamertag, xboxUser, ct), ResolvedBy.GamertagHistory);
    }

    private bool Overlaps(XboxGamertagHistory h)
        => (h.FirstSeen is null || h.FirstSeen <= _windowEnd) && (h.LastSeen is null || h.LastSeen >= _windowStart);

    /// <summary>When the account last changed gamertag, as far as its history knows.</summary>
    private static DateTime? LastRename(List<XboxGamertagHistory> history)
        => history.Where(h => h.LastSeen is not null).Max(h => h.LastSeen);

    private Task<User?> PlaceholderOf(string gamertag, CancellationToken ct)
    {
        var name = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant();
        return _context.Users.FirstOrDefaultAsync(u => u.IsDummyUser && u.NormalizedUserName == name, ct);
    }

    private async Task<int> CreateDummy(string gamertag, XboxUser? xboxUser, CancellationToken ct)
    {
        var userName = HistoricalKeys.DummyUserName(gamertag);
        var existing = await PlaceholderOf(gamertag, ct);
        if (existing is not null)
        {
            // An Xbox account the gamertag's placeholder isn't tied to (it is another's, or the dates didn't tie it to this
            // one) gets a placeholder of its own, named after its XUID too
            if (xboxUser is null || existing.XboxUserID == xboxUser.XboxUserID)
                return existing.Id;
            userName = $"{userName}:{xboxUser.XboxUserID}";
        }
        var user = new User
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = gamertag.Length > 30 ? gamertag[..30] : gamertag,
            IsDummyUser = true,
            XboxUserID = xboxUser is { User: null } ? xboxUser.XboxUserID : null,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
        DummiesCreated++;
        return user.Id;
    }
}
