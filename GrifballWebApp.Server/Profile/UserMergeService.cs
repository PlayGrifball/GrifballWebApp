using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Profile;

/// <summary>
/// Useful for when multiple accounts are mistakely made for the same person
/// </summary>
public interface IUserMergeService
{
    /// <summary>
    /// Useful for when multiple accounts are mistakely made for the same person
    /// </summary>
    /// <param name="mergeToId">The user to transfer owned entities to</param>
    /// <param name="mergeFromId">The user to take owned entities from, this account will be deleted</param>
    /// <param name="options">Options for the merge operation, such as whether to transfer Discord/Xbox IDs, team players, etc. Defaults to transfer all and delete merge from.</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task Merge(int mergeToId, int mergeFromId, MergeOptions? options = null, CancellationToken ct = default);
}

/// <inheritdoc/>
public class UserMergeService : IUserMergeService
{
    private readonly GrifballContext _context;
    public UserMergeService(GrifballContext context)
    {
        _context = context;
    }
    public async Task Merge(int mergeToId, int mergeFromId, MergeOptions? options = null, CancellationToken ct = default)
    {
        if (mergeToId == mergeFromId)
            throw new ArgumentException("Cannot merge a user into itself");

        options ??= new MergeOptions();

        // Look both users up before starting the transaction, so a missing user (for example a second click
        // after the first merge already deleted the source) fails cleanly without leaving a transaction open.
        var mergeTo = await _context.Users
            .Where(u => u.Id == mergeToId)
            .FirstOrDefaultAsync(ct) ?? throw new UserNotFoundException(mergeToId);

        var mergeFrom = await _context.Users
            .Where(u => u.Id == mergeFromId)
            .FirstOrDefaultAsync(ct) ?? throw new UserNotFoundException(mergeFromId);

        // So are the conflicts no merge rule can settle.
        if (options.TransferTeamPlayers)
            await ThrowIfOnDifferentTeamsInASeason(mergeToId, mergeFromId, ct);
        if (options.TransferSeasonStats)
            await ThrowIfBothHaveStatsForASeason(mergeToId, mergeFromId, ct);
        if (options.DeleteMergeFrom)
            await ThrowIfHistoricalRecordsWouldBeLeft(mergeFromId, options, ct);

        await _context.StartTransactionAsync(ct);

        // External logins (Auth.UserLogins) are what an external sign-in such as "Sign in with Discord" looks up.
        // They move in the same SaveChanges as the other transfers, before the source is deleted, so a failed
        // merge never loses them. The key is (LoginProvider, ProviderKey), so only UserId changes and the target
        // can hold any number of logins, including several for one provider.
        var logins = await _context.UserLogins.Where(l => l.UserId == mergeFromId).ToListAsync(ct);
        if (options.TransferLogins)
        {
            logins.ForEach(l => l.UserId = mergeToId);
        }

        if (options.TransferDiscord)
        {
            if (mergeFrom.DiscordUserID is not null)
            {
                mergeTo.DiscordUserID = mergeFrom.DiscordUserID;
                mergeFrom.DiscordUserID = null;
            }
            else if (mergeTo.DiscordUserID is null && options.TransferLogins)
            {
                // A login-only account (signed in with Discord, never linked by the bot) has no DiscordUserID.
                // Its Discord login key is the Discord user ID, so link the bot account from it, but only when the
                // target has no link of its own and that Discord user is known and not linked to anyone else.
                await LinkDiscordFromLogin(mergeTo, logins, ct);
            }
        }

        if (mergeFrom.XboxUserID is not null && options.TransferXbox)
        {
            mergeTo.XboxUserID = mergeFrom.XboxUserID;
            mergeFrom.XboxUserID = null;
        }

        if (options.TransferTeamPlayers)
            await TransferTeamPlayers(mergeToId, mergeFromId, ct);

        if (options.TransferExperiences)
            await TransferExperiences(mergeToId, mergeFromId, ct);

        if (options.TransferSignups)
            await TransferSignups(mergeToId, mergeFromId, ct);

        // Matchmaking history and reschedule requests always move: deleting the source would otherwise take its
        // matched players (and their votes) with it, or fail on the reschedules' restricted foreign keys.
        await TransferMatchmaking(mergeTo, mergeFrom, ct);
        await TransferReschedules(mergeToId, mergeFromId, ct);

        // Historical stats, championships and aliases: their foreign keys to the user are restricted, so they move
        // before the source is deleted (a merge that would leave them on a deleted source was refused above).
        if (options.TransferSeasonStats)
            await TransferSeasonStats(mergeToId, mergeFromId, ct);

        if (options.TransferChampionships)
            await TransferChampionships(mergeToId, mergeFromId, ct);

        if (options.TransferAliases)
            await TransferAliases(mergeToId, mergeFromId, ct);

        // Delete just in case it doesnt cascade
        var rolesToDelete = await _context.UserRoles.Where(x => x.UserId == mergeFromId).ToArrayAsync(ct);
        _context.UserRoles.RemoveRange(rolesToDelete);
        //ExecuteDeleteAsync does not work in with in memory test
        //await _context.UserRoles.Where(x => x.UserId == existingUserID).ExecuteDeleteAsync(ct);

        // Transfer entities
        await _context.SaveChangesAsync(ct);

        // Delete user
        if (options.DeleteMergeFrom)
        {
            _context.Users.Remove(mergeFrom);
            await _context.SaveChangesAsync(ct);
        }

        await _context.CommitTransactionAsync(ct);
    }

    /// <summary>
    /// One user can't be on two teams in a season, and which of the two the merged user should keep is a commissioner's
    /// call: refused before anything changes.
    /// </summary>
    private async Task ThrowIfOnDifferentTeamsInASeason(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var target = await _context.TeamPlayers.Where(tp => tp.UserID == mergeToId)
            .Select(tp => new { tp.TeamID, tp.Team.SeasonID, tp.Team.Season.SeasonName })
            .ToListAsync(ct);
        var source = await _context.TeamPlayers.Where(tp => tp.UserID == mergeFromId)
            .Select(tp => new { tp.TeamID, tp.Team.SeasonID })
            .ToListAsync(ct);
        var clash = target.FirstOrDefault(t => source.Any(s => s.SeasonID == t.SeasonID && s.TeamID != t.TeamID));
        if (clash is not null)
            throw new MergeConflictException($"Both users are on different teams in season {clash.SeasonName}. Remove one of them from their team first.");
    }

    /// <summary>
    /// Two rows for one player, stint and phase of a season can't both be kept, and which is right is a commissioner's
    /// call: refused before anything changes. A Waypoint row and a Manual one are no clash, the Waypoint one wins
    /// (see <see cref="TransferSeasonStats"/>).
    /// </summary>
    private async Task ThrowIfBothHaveStatsForASeason(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var rows = await _context.PlayerSeasonStats
            .Where(s => s.UserID == mergeToId || s.UserID == mergeFromId)
            .Select(s => new HistoricalStatKey(s.PlayerSeasonStatID, s.UserID, s.SeasonID, s.StintIndex, s.IsPlayoffs, s.Source, s.Season.SeasonName))
            .ToListAsync(ct);
        var (target, source) = KeptStats(rows, mergeToId, mergeFromId);
        var clash = target.FirstOrDefault(t => source.Any(s => s.SeasonID == t.SeasonID && s.StintIndex == t.StintIndex && s.IsPlayoffs == t.IsPlayoffs));
        if (clash is not null)
            throw new MergeConflictException($"Both users have historical stats for {(clash.IsPlayoffs ? "the playoffs of " : "")}season {clash.SeasonName} (stint {clash.StintIndex}). Remove or fix one of them first.");
    }

    /// <summary>
    /// A user is never deleted with historical records left on it (their foreign keys are restricted): a merge that
    /// is told to delete the source but not to move them is refused before anything changes.
    /// </summary>
    private async Task ThrowIfHistoricalRecordsWouldBeLeft(int mergeFromId, MergeOptions options, CancellationToken ct)
    {
        var left = (!options.TransferSeasonStats && await _context.PlayerSeasonStats.AnyAsync(x => x.UserID == mergeFromId, ct))
            || (!options.TransferChampionships && await _context.PlayerChampionships.AnyAsync(x => x.UserID == mergeFromId, ct))
            || (!options.TransferAliases && await _context.PlayerAliases.AnyAsync(x => x.UserID == mergeFromId, ct));
        if (left)
            throw new UserHasHistoricalStatsException(mergeFromId);
    }

    private sealed record HistoricalStatKey(int PlayerSeasonStatID, int UserID, int SeasonID, int StintIndex, bool IsPlayoffs, StatSource Source, string SeasonName);

    /// <summary>
    /// Each user's stats as they will be after the merge: in a season where one of them has Waypoint stats, the
    /// other's Manual ones go (a player's season is never both). Returns the rows kept for each.
    /// </summary>
    private static (List<HistoricalStatKey> Target, List<HistoricalStatKey> Source) KeptStats(List<HistoricalStatKey> rows, int mergeToId, int mergeFromId)
    {
        var target = rows.Where(r => r.UserID == mergeToId).ToList();
        var source = rows.Where(r => r.UserID == mergeFromId).ToList();
        var targetWaypointSeasons = target.Where(r => r.Source == StatSource.Waypoint).Select(r => r.SeasonID).ToHashSet();
        var sourceWaypointSeasons = source.Where(r => r.Source == StatSource.Waypoint).Select(r => r.SeasonID).ToHashSet();
        return (
            target.Where(r => !(r.Source == StatSource.Manual && sourceWaypointSeasons.Contains(r.SeasonID))).ToList(),
            source.Where(r => !(r.Source == StatSource.Manual && targetWaypointSeasons.Contains(r.SeasonID))).ToList());
    }

    /// <summary>
    /// The source's season stats move to the target. Where one of them has Waypoint stats for a season, the other's
    /// Manual stats for it are removed (Waypoint replaces Manual); any other clash was refused before the merge.
    /// </summary>
    private async Task TransferSeasonStats(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var stats = await _context.PlayerSeasonStats
            .Where(x => x.UserID == mergeFromId || x.UserID == mergeToId)
            .ToListAsync(ct);
        var keys = stats.Select(s => new HistoricalStatKey(s.PlayerSeasonStatID, s.UserID, s.SeasonID, s.StintIndex, s.IsPlayoffs, s.Source, "")).ToList();
        var (keptTarget, keptSource) = KeptStats(keys, mergeToId, mergeFromId);
        var kept = keptTarget.Concat(keptSource).Select(k => k.PlayerSeasonStatID).ToHashSet();
        foreach (var stat in stats)
        {
            if (!kept.Contains(stat.PlayerSeasonStatID))
                _context.PlayerSeasonStats.Remove(stat);
            else if (stat.UserID == mergeFromId)
                stat.UserID = mergeToId;
        }
        // The replaced rows go before the moved ones take their keys.
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The source's championships move to the target, except titles the target already holds for that season and tier
    /// ((SeasonID, Tier, UserID) is unique): one person listed twice on a roster under two gamertags.
    /// </summary>
    private async Task TransferChampionships(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var championships = await _context.PlayerChampionships.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        var held = await _context.PlayerChampionships.Where(x => x.UserID == mergeToId).Select(x => new { x.SeasonID, x.Tier }).ToListAsync(ct);
        foreach (var championship in championships)
        {
            if (held.Any(h => h.SeasonID == championship.SeasonID && string.Equals(h.Tier, championship.Tier, StringComparison.OrdinalIgnoreCase)))
                _context.PlayerChampionships.Remove(championship);
            else
                championship.UserID = mergeToId;
        }
    }

    /// <summary>The source's other Xbox accounts are the target's, but for those it already has ((UserID, NormalizedAlias) is unique).</summary>
    private async Task TransferAliases(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var aliases = await _context.PlayerAliases.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        var targetAliases = await _context.PlayerAliases.Where(x => x.UserID == mergeToId).Select(x => x.NormalizedAlias).ToListAsync(ct);
        foreach (var alias in aliases)
        {
            if (targetAliases.Contains(alias.NormalizedAlias, StringComparer.OrdinalIgnoreCase))
                _context.PlayerAliases.Remove(alias);
            else
                alias.UserID = mergeToId;
        }
    }

    /// <summary>
    /// The source's team memberships move to the target. Where both are on the same team the target keeps its own row
    /// (which becomes the captain's if the source's was), and the source's goes: (UserID, TeamID) is unique.
    /// </summary>
    private async Task TransferTeamPlayers(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var teamPlayers = await _context.TeamPlayers.Where(tp => tp.UserID == mergeFromId).ToListAsync(ct);
        var targetRows = await _context.TeamPlayers.Where(tp => tp.UserID == mergeToId).ToDictionaryAsync(tp => tp.TeamID, ct);
        foreach (var tp in teamPlayers)
        {
            if (targetRows.TryGetValue(tp.TeamID, out var targetRow))
            {
                var captainOf = await _context.Teams.FirstOrDefaultAsync(t => t.CaptainID == tp.TeamPlayerID, ct);
                if (captainOf is not null)
                {
                    captainOf.CaptainID = targetRow.TeamPlayerID;
                    targetRow.DraftCaptainOrder ??= tp.DraftCaptainOrder;
                }
                _context.TeamPlayers.Remove(tp);
            }
            else
            {
                tp.UserID = mergeToId;
            }
        }
        // The captain moves before the source's row goes (Teams.CaptainID has no cascade).
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// UserExperience is only which game versions a user has played, keyed (UserID, GameVersionID): the key can't be
    /// changed, so the source's rows are removed and the target gets any versions it doesn't already have.
    /// </summary>
    private async Task TransferExperiences(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var experiences = await _context.UserExperiences.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        var targetVersions = await _context.UserExperiences.Where(x => x.UserID == mergeToId).Select(x => x.GameVersionID).ToListAsync(ct);
        foreach (var experience in experiences)
        {
            _context.UserExperiences.Remove(experience);
            if (targetVersions.Contains(experience.GameVersionID) is false)
                _context.UserExperiences.Add(new UserExperience { UserID = mergeToId, GameVersionID = experience.GameVersionID });
        }
    }

    /// <summary>
    /// The source's signups move to the target, except for seasons the target signed up for itself: (UserID, SeasonID)
    /// is unique, and the target's own signup (and availability) is the one kept.
    /// </summary>
    private async Task TransferSignups(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var signups = await _context.SeasonSignups.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        var targetSeasons = await _context.SeasonSignups.Where(x => x.UserID == mergeToId).Select(x => x.SeasonID).ToListAsync(ct);
        foreach (var signup in signups)
        {
            if (targetSeasons.Contains(signup.SeasonID))
                _context.SeasonSignups.Remove(signup);
            else
                signup.UserID = mergeToId;
        }
    }

    /// <summary>
    /// The source's matched players (and through them its winner and kick votes) move to the target, and its record
    /// is combined: wins and losses add up, MMR is the average of the two weighted by games played (the target's if
    /// neither played), and the streaks are those of whichever account has the games (the target's if both).
    /// A queued source leaves the queue.
    /// </summary>
    private async Task TransferMatchmaking(User mergeTo, User mergeFrom, CancellationToken ct)
    {
        var matchedPlayers = await _context.MatchedPlayers.Where(x => x.UserID == mergeFrom.Id).ToListAsync(ct);
        matchedPlayers.ForEach(x => x.UserID = mergeTo.Id);

        var targetGames = mergeTo.Wins + mergeTo.Losses;
        var sourceGames = mergeFrom.Wins + mergeFrom.Losses;
        if (sourceGames > 0)
        {
            mergeTo.MMR = (int)Math.Round(((double)mergeTo.MMR * targetGames + (double)mergeFrom.MMR * sourceGames) / (targetGames + sourceGames));
            if (targetGames == 0)
            {
                mergeTo.WinStreak = mergeFrom.WinStreak;
                mergeTo.LossStreak = mergeFrom.LossStreak;
            }
            mergeTo.Wins += mergeFrom.Wins;
            mergeTo.Losses += mergeFrom.Losses;
            mergeFrom.Wins = 0;
            mergeFrom.Losses = 0;
        }

        var queued = await _context.QueuedPlayer.FirstOrDefaultAsync(x => x.UserID == mergeFrom.Id, ct);
        if (queued is not null)
            _context.QueuedPlayer.Remove(queued);
    }

    /// <summary>Reschedule requests the source made or decided are the target's (both foreign keys are restricted).</summary>
    private async Task TransferReschedules(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var reschedules = await _context.MatchReschedules
            .Where(x => x.RequestedByUserID == mergeFromId || x.ApprovedByUserID == mergeFromId)
            .ToListAsync(ct);
        foreach (var reschedule in reschedules)
        {
            if (reschedule.RequestedByUserID == mergeFromId)
                reschedule.RequestedByUserID = mergeToId;
            if (reschedule.ApprovedByUserID == mergeFromId)
                reschedule.ApprovedByUserID = mergeToId;
        }
    }


    private async Task LinkDiscordFromLogin(User mergeTo, List<UserLogin> logins, CancellationToken ct)
    {
        var discordLogin = logins.FirstOrDefault(l => l.LoginProvider == DiscordProvider);
        if (discordLogin is null || !long.TryParse(discordLogin.ProviderKey, out var discordUserId))
            return;

        var known = await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == discordUserId, ct);
        var linkedElsewhere = await _context.Users.AnyAsync(u => u.DiscordUserID == discordUserId, ct);
        if (known && !linkedElsewhere)
            mergeTo.DiscordUserID = discordUserId;
    }

    private const string DiscordProvider = "Discord";
}

/// <summary>A user to merge does not exist, e.g. a second request after the first merge already deleted the source.</summary>
public class UserNotFoundException(int userId) : Exception("Did not find user " + userId)
{
    public int UserId { get; } = userId;
}

public class MergeOptions
{
    /// <summary>Moves the bot's Discord link (DiscordUserID) to the target. When the source has none, the target has none
    /// and <see cref="TransferLogins"/> is on, the link is taken from the source's Discord login if that Discord user is known.</summary>
    public bool TransferDiscord { get; set; } = true;
    /// <summary>Moves every external login (any provider, e.g. "Sign in with Discord") to the target, so it then signs in as the target.
    /// With this off and <see cref="DeleteMergeFrom"/> on, the source's logins are deleted with it.</summary>
    public bool TransferLogins { get; set; } = true;
    public bool TransferXbox { get; set; } = true;
    public bool TransferTeamPlayers { get; set; } = true;
    public bool TransferExperiences { get; set; } = true;
    public bool TransferSignups { get; set; } = true;
    public bool TransferSeasonStats { get; set; } = true;
    public bool TransferChampionships { get; set; } = true;
    public bool TransferAliases { get; set; } = true;
    public bool DeleteMergeFrom { get; set; } = true;
}

/// <summary>The two users can't be merged as they are, e.g. both are on different teams in one season.</summary>
public class MergeConflictException(string message) : Exception(message);

/// <summary>
/// A user with historical stats, championships or aliases can't be deleted (the foreign keys are restricted): merge it
/// into the account they belong to instead, which moves them.
/// </summary>
public class UserHasHistoricalStatsException(int userId)
    : Exception($"User {userId} has historical stats; merge instead of deleting.")
{
    public int UserId { get; } = userId;
}
