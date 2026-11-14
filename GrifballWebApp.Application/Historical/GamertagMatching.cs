using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Historical;

/// <summary>
/// How a gamertag is matched to an account or a placeholder, shared by the historical import (PlayerResolver, roster
/// suggestions), setting a gamertag (SetGamertagService) and claims (GamertagClaimService), so the three never disagree.
/// </summary>
public class GamertagMatching
{
    private readonly IApplicationDbContext _context;

    public GamertagMatching(IApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>The placeholder (historical:&lt;gamertag&gt;) of a gamertag; with <paramref name="untiedOnly"/>, only
    /// one not tied to an Xbox account yet. Suffix-aware: "Name#1234" (a modern gamertag's suffix) finds the placeholder
    /// of "Name" when it has none of its own, and "Name" finds the only "Name#&lt;digits&gt;" one; only untied ones that way
    /// (an Xbox account's own placeholder is that account's gamertag, not a namesake's).</summary>
    public async Task<User?> PlaceholderAsync(string gamertag, bool untiedOnly, CancellationToken ct)
    {
        var name = HistoricalKeys.DummyUserName(gamertag).ToUpperInvariant();
        var exact = await _context.Users.FirstOrDefaultAsync(u => u.IsDummyUser && u.NormalizedUserName == name && (!untiedOnly || u.XboxUserID == null), ct);
        if (exact is not null)
            return exact;

        var baseTag = HistoricalKeys.WithoutSuffix(gamertag);
        if (!string.Equals(baseTag, HistoricalKeys.CleanGamertag(gamertag), StringComparison.Ordinal))
        {
            var baseName = HistoricalKeys.DummyUserName(baseTag).ToUpperInvariant();
            return await _context.Users.FirstOrDefaultAsync(u => u.IsDummyUser && u.NormalizedUserName == baseName && u.XboxUserID == null, ct);
        }

        var prefix = name + "#";
        var suffixed = (await _context.Users
                .Where(u => u.IsDummyUser && u.NormalizedUserName!.StartsWith(prefix) && u.XboxUserID == null)
                .ToListAsync(ct))
            .Where(u => u.NormalizedUserName![prefix.Length..].All(char.IsAsciiDigit))
            .ToList();
        return suffixed.Count == 1 ? suffixed[0] : null;
    }

    /// <summary>
    /// The placeholder of a gamertag, made if there is none: IsDummyUser, UserName "historical:{gamertag}" (unique, as
    /// NormalizedUserName is, and a name no sign-up can take), DisplayName the gamertag, linked to the Xbox account when
    /// one is given that no user holds. When the gamertag's placeholder is another Xbox account's, the account gets one
    /// of its own, named after its XUID too. The one way placeholders are made: the import (PlayerResolver) and Xbox
    /// accounts nobody signed up with (<see cref="EnsureXboxPlaceholdersAsync"/>). Saves.
    /// </summary>
    public async Task<(User User, bool Created)> CreatePlaceholderAsync(string gamertag, XboxUser? xboxUser, CancellationToken ct)
    {
        gamertag = HistoricalKeys.CleanGamertag(gamertag);
        var userName = HistoricalKeys.DummyUserName(gamertag);
        var existing = await PlaceholderAsync(gamertag, untiedOnly: false, ct);
        if (existing is not null)
        {
            if (xboxUser is null || existing.XboxUserID == xboxUser.XboxUserID)
                return (existing, false);
            // Found by its suffix-less name: the gamertag's own name is free
            var normalizedUserName = userName.ToUpperInvariant();
            if (existing.NormalizedUserName == normalizedUserName)
            {
                userName = $"{userName}:{xboxUser.XboxUserID}";
                var normalized = userName.ToUpperInvariant();
                var own = await _context.Users.FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
                if (own is not null)
                    return (own, false);
            }
        }
        var holder = xboxUser is null ? null : await _context.Users.FirstOrDefaultAsync(u => u.XboxUserID == xboxUser.XboxUserID, ct);
        var user = User.ImportPlaceholder(userName, gamertag, xboxUser is not null && holder is null ? xboxUser.XboxUserID : null);
        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);
        return (user, true);
    }

    /// <summary>
    /// Gives each Xbox account no user holds that played in a league game (a game reported for a season match: a sub who
    /// never signed up, say) a placeholder, so its games count in stats and leaderboards; the player's sign-up merges it
    /// later (SetGamertagService). Anyone else who was only in a pulled game (a pickup custom's opponent) is no league
    /// player and gets no user: their Xbox account and games are kept, on its own page. Only the given XUIDs, or every
    /// such account when null. Idempotent. Returns how many were made.
    /// </summary>
    public async Task<int> EnsureXboxPlaceholdersAsync(IReadOnlyCollection<long>? xuids, CancellationToken ct)
    {
        var query = _context.XboxUsers
            .Where(x => !_context.Users.Any(u => u.XboxUserID == x.XboxUserID))
            .Where(PlayedInALeagueGame(_context));
        if (xuids is not null)
        {
            if (xuids.Count == 0)
                return 0;
            var ids = xuids.ToList();
            query = query.Where(x => ids.Contains(x.XboxUserID));
        }
        var orphans = await query.OrderBy(x => x.XboxUserID).ToListAsync(ct);
        var made = 0;
        foreach (var xboxUser in orphans)
        {
            if ((await CreatePlaceholderAsync(xboxUser.Gamertag, xboxUser, ct)).Created)
                made++;
        }
        return made;
    }

    /// <summary>Xbox accounts in a game reported for a season match (a series game's Halo match): the league's players, subs included.</summary>
    public static System.Linq.Expressions.Expression<Func<XboxUser, bool>> PlayedInALeagueGame(IApplicationDbContext context)
        => x => context.SeasonSeriesGames.Any(g => g.Match != null
            && g.Match.MatchTeams.Any(t => t.MatchParticipants.Any(p => p.XboxUserID == x.XboxUserID)));

    /// <summary>
    /// Whether a placeholder's rows can be the account's under that past gamertag: none of their seasons (or Late League
    /// seasons and games) began after the account let go of it, and no other account's history has the gamertag at a time
    /// that can include them.
    /// </summary>
    public async Task<bool> FitsTheHistoryAsync(int placeholderId, XboxGamertagHistory past, CancellationToken ct)
    {
        var starts = await _context.PlayerSeasonStats.Where(s => s.UserID == placeholderId).Select(s => s.Season.SeasonStart)
            .Concat(_context.Seasons.Championships().Where(c => c.UserID == placeholderId).Select(c => c.Season.SeasonStart))
            .Concat(_context.MatchmakingPlayerStats.Where(s => s.UserID == placeholderId).Select(s => s.MatchmakingSeason.StartsAt))
            .Concat(_context.MatchedMatches.Where(MatchedMatch.HasPlayer(placeholderId))
                .Select(m => m.StartedAt))
            .ToListAsync(ct);
        if (starts.Any(s => !past.WasHeldAt(s)))
            return false;
        DateTime? earliest = starts.Count == 0 ? null : starts.Min();
        return !await _context.XboxUsers.PastGamertags()
            .Where(h => h.XboxUserID != past.XboxUserID && h.NormalizedGamertag == past.NormalizedGamertag)
            .Where(XboxGamertagHistory.HeldAt(earliest))
            .AnyAsync(ct);
    }

    /// <summary>
    /// Adds a pending claim for a sysadmin to decide, unless the same one (kind, account, gamertag) is pending or approved
    /// already. A rejected one stops it too unless <paramref name="evenIfRejected"/>: a user setting their gamertag again
    /// may claim again (a rejection doesn't block the account forever), the import doesn't repeat a suggestion a
    /// sysadmin turned down. Returns the claim added, null when none was. The caller saves. An XUID lookup's claim gives
    /// what Xbox said the account's gamertag is now, and when (<paramref name="lookedUp"/>).
    /// </summary>
    public async Task<GamertagClaim?> ClaimAsync(GamertagClaimKind kind, long xboxUserId, string gamertag, int placeholderId, int? seasonId, int? teamId, bool evenIfRejected, CancellationToken ct,
        (string CurrentGamertag, DateTime At)? lookedUp = null)
    {
        var normalized = HistoricalKeys.Normalize(gamertag);
        if (await ClaimedAsync(c => c.Kind == kind && c.XboxUserID == xboxUserId && c.NormalizedGamertag == normalized, evenIfRejected, ct))
            return null;
        var clean = HistoricalKeys.CleanGamertag(gamertag);
        var claim = kind == GamertagClaimKind.XuidLookup
            ? GamertagClaim.FromXuidLookup(xboxUserId, clean, normalized, placeholderId,
                (lookedUp ?? throw new ArgumentNullException(nameof(lookedUp), "An XUID lookup's claim says what Xbox found")).CurrentGamertag, lookedUp.Value.At)
            : GamertagClaim.Suggest(kind, xboxUserId, clean, normalized, placeholderId, seasonId, teamId);
        _context.GamertagClaims.Add(claim);
        return claim;
    }

    /// <summary>
    /// Whether a claim the same as the one to make (<paramref name="same"/>) stops it being made again: any, or only an open
    /// one (<see cref="GamertagClaim.Open"/>) when <paramref name="evenIfRejected"/>. Those added and not saved yet count too.
    /// </summary>
    private async Task<bool> ClaimedAsync(System.Linq.Expressions.Expression<Func<GamertagClaim, bool>> same, bool evenIfRejected, CancellationToken ct)
    {
        var isSame = same.Compile();
        if (_context.GamertagClaims.Local.Any(c => isSame(c) && (!evenIfRejected || c.IsOpen)))
            return true;
        var saved = _context.GamertagClaims.Where(same);
        if (evenIfRejected)
            saved = saved.Where(GamertagClaim.Open);
        return await saved.AnyAsync(ct);
    }

    /// <summary>
    /// <see cref="ClaimAsync"/> for a claim about a user rather than an Xbox account (a Discord match): one pending or
    /// approved per kind, user and gamertag, and a rejected one stops it too unless <paramref name="evenIfRejected"/>.
    /// The caller saves.
    /// </summary>
    public async Task<GamertagClaim?> ClaimForUserAsync(GamertagClaimKind kind, int claimantUserId, long? xboxUserId, string gamertag, int placeholderId, bool evenIfRejected, CancellationToken ct)
    {
        if (kind != GamertagClaimKind.DiscordMatch)
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a Discord match is suggested about a user");
        var normalized = HistoricalKeys.Normalize(gamertag);
        if (await ClaimedAsync(c => c.Kind == kind && c.ClaimantUserID == claimantUserId && c.NormalizedGamertag == normalized, evenIfRejected, ct))
            return null;
        var claim = GamertagClaim.FromDiscordMatch(claimantUserId, xboxUserId, HistoricalKeys.CleanGamertag(gamertag), normalized, placeholderId);
        _context.GamertagClaims.Add(claim);
        return claim;
    }

    /// <summary>
    /// The signed-up user a gamertag is, as far as is known now: the one whose Xbox account has it (compared loosely:
    /// case, spaces and a "#1234" suffix aside), or whose account's history has it, when only one does. Null otherwise.
    /// </summary>
    public async Task<User?> RealUserOfAsync(string gamertag, CancellationToken ct)
    {
        var key = HistoricalKeys.LooseKey(gamertag);
        var normalized = HistoricalKeys.Normalize(gamertag);
        // Compared in .NET (letters and digits only): a few hundred signed-up players
        var holders = (await _context.Users.Where(u => !u.IsDummyUser && u.XboxUser != null)
                .Select(u => new { User = u, u.XboxUser!.Gamertag }).ToListAsync(ct))
            .Where(u => HistoricalKeys.LooseKey(u.Gamertag) == key)
            .Select(u => u.User)
            .ToList();
        if (holders.Count == 1)
            return holders[0];
        if (holders.Count > 1)
            return null;
        var accounts = await _context.XboxUsers.PastGamertags().Where(h => h.NormalizedGamertag == normalized).Select(h => h.XboxUserID).Distinct().ToListAsync(ct);
        var fromHistory = await _context.Users.Where(u => !u.IsDummyUser && u.XboxUserID != null && accounts.Contains(u.XboxUserID.Value)).ToListAsync(ct);
        return fromHistory.Count == 1 ? fromHistory[0] : null;
    }
}
