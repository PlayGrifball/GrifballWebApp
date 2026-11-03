using GrifballWebApp.Database;
using GrifballWebApp.Database.Services;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Database.Models;
using System.Reflection;

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
    private readonly TimeProvider _time;
    private readonly ICurrentUserService? _currentUser;
    /// <summary>The merge in progress: every SaveChanges of it goes through this, so the audit record has all it changed.</summary>
    private MergeManifestRecorder _recorder = null!;

    public UserMergeService(GrifballContext context, TimeProvider? time = null, ICurrentUserService? currentUser = null)
    {
        _context = context;
        _time = time ?? TimeProvider.System;
        _currentUser = currentUser;
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

        // What the caller left unsaved (a claim it just added) is its own change, not the merge's: saved before the
        // merge starts recording
        await _context.SaveChangesAsync(ct);
        _recorder = new MergeManifestRecorder(_context);
        var sourceUserName = mergeFrom.UserName;
        var targetUserName = mergeTo.UserName;

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
        // Recorded games are what the player played, whichever account: they always move (their user key is restricted)
        foreach (var line in await _context.RecordedGameParticipants.Where(p => p.UserID == mergeFromId).ToListAsync(ct))
            line.UserID = mergeToId;

        // Historical stats and championships: their foreign keys to the user are restricted, so they move
        // before the source is deleted (a merge that would leave them on a deleted source was refused above).
        if (options.TransferSeasonStats)
            await TransferSeasonStats(mergeToId, mergeFromId, ct);

        if (options.TransferChampionships)
            await TransferChampionships(mergeToId, mergeFromId, ct);

        await SettleClaims(mergeTo, mergeFromId, ct);

        // Delete just in case it doesnt cascade
        var rolesToDelete = await _context.UserRoles.Where(x => x.UserId == mergeFromId).ToArrayAsync(ct);
        _context.UserRoles.RemoveRange(rolesToDelete);
        //ExecuteDeleteAsync does not work in with in memory test
        //await _context.UserRoles.Where(x => x.UserId == existingUserID).ExecuteDeleteAsync(ct);

        // Transfer entities
        await _recorder.SaveChangesAsync(ct);

        // Delete user
        if (options.DeleteMergeFrom)
        {
            // What the database would cascade to (or set null in) is loaded first, so the manifest sees it go and an
            // undo can bring it back
            await LoadWhatDeletingCascadesTo(mergeFromId, ct);
            _context.Users.Remove(mergeFrom);
            await _recorder.SaveChangesAsync(ct);
        }

        // The audit record, in the same transaction: every path to a merge writes one
        var manifest = _recorder.Manifest;
        _context.UserMerges.Add(new UserMerge
        {
            MergedAt = _time.GetUtcNow().UtcDateTime,
            TriggeredByUserID = options.TriggeredByUserID ?? _currentUser?.GetCurrentUserId(),
            Trigger = options.Trigger,
            Reason = options.Reason is { Length: > 200 } reason ? reason[..200] : options.Reason,
            SourceUserID = mergeFromId,
            SourceUserName = sourceUserName,
            TargetUserID = mergeToId,
            TargetUserName = targetUserName,
            Manifest = manifest.Serialize(),
            RowsMoved = manifest.Entries.Count(e => e.Op == MergeManifestOp.Modified),
            RowsRemoved = manifest.Entries.Count(e => e.Op == MergeManifestOp.Deleted),
            RowsAdded = manifest.Entries.Count(e => e.Op == MergeManifestOp.Added),
        });
        await _context.SaveChangesAsync(ct);

        await _context.CommitTransactionAsync(ct);
    }

    /// <summary>Why a merge with the default options would be refused (different teams in a season, clashing stats), or
    /// null: the same rules <see cref="Merge"/> applies, for suggesting merges without making them.</summary>
    public async Task<string?> ConflictAsync(int mergeToId, int mergeFromId, CancellationToken ct = default)
    {
        try
        {
            await ThrowIfOnDifferentTeamsInASeason(mergeToId, mergeFromId, ct);
            await ThrowIfBothHaveStatsForASeason(mergeToId, mergeFromId, ct);
            return null;
        }
        catch (MergeConflictException e)
        {
            return e.Message;
        }
    }

    private static readonly MethodInfo LoadReferencingMethod = typeof(UserMergeService).GetMethod(nameof(LoadReferencing), BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    /// Loads every row whose foreign key to the user cascades or sets null on delete (roles, logins, claims, tokens,
    /// lookups, ...), read from the model so a new table is covered without changing the merge. Restricted keys are
    /// left: the merge moved those rows, or refused.
    /// </summary>
    private async Task LoadWhatDeletingCascadesTo(int userId, CancellationToken ct)
    {
        var userType = _context.Model.FindEntityType(typeof(User))!;
        foreach (var fk in userType.GetReferencingForeignKeys())
        {
            if (fk.DeleteBehavior is DeleteBehavior.Restrict or DeleteBehavior.NoAction or DeleteBehavior.ClientNoAction)
                continue;
            var dependent = fk.DeclaringEntityType;
            if (dependent.IsOwned() || dependent.FindPrimaryKey() is null || dependent.HasSharedClrType || fk.Properties.Count != 1)
                continue;
            var property = fk.Properties[0];
            var load = (Task)LoadReferencingMethod.MakeGenericMethod(dependent.ClrType)
                .Invoke(this, [property.Name, property.ClrType == typeof(int?), userId, ct])!;
            await load;
        }
    }

    private Task LoadReferencing<T>(string property, bool nullable, int userId, CancellationToken ct) where T : class
        => nullable
            ? _context.Set<T>().Where(e => EF.Property<int?>(e, property) == userId).LoadAsync(ct)
            : _context.Set<T>().Where(e => EF.Property<int>(e, property) == userId).LoadAsync(ct);

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
    /// call: refused before anything changes.
    /// </summary>
    private async Task ThrowIfBothHaveStatsForASeason(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var rows = await _context.PlayerSeasonStats
            .Where(s => s.UserID == mergeToId || s.UserID == mergeFromId)
            .Select(s => new { s.UserID, s.SeasonID, s.StintIndex, s.IsPlayoffs, s.Season.SeasonName })
            .ToListAsync(ct);
        var source = rows.Where(r => r.UserID == mergeFromId).ToList();
        var clash = rows.Where(r => r.UserID == mergeToId)
            .FirstOrDefault(t => source.Any(s => s.SeasonID == t.SeasonID && s.StintIndex == t.StintIndex && s.IsPlayoffs == t.IsPlayoffs));
        if (clash is not null)
            throw new MergeConflictException($"Both users have historical stats for {(clash.IsPlayoffs ? "the playoffs of " : "")}season {clash.SeasonName} (stint {clash.StintIndex}). Remove or fix one of them first.");
        var matchmaking = await _context.MatchmakingPlayerStats
            .Where(s => s.UserID == mergeToId || s.UserID == mergeFromId)
            .GroupBy(s => new { s.MatchmakingSeasonID, s.MatchmakingSeason.Name })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.Name)
            .FirstOrDefaultAsync(ct);
        if (matchmaking is not null)
            throw new MergeConflictException($"Both users have historical stats for {matchmaking}. Remove or fix one of them first.");
    }

    /// <summary>
    /// A user is never deleted with historical records left on it (their foreign keys are restricted): a merge that
    /// is told to delete the source but not to move them is refused before anything changes.
    /// </summary>
    private async Task ThrowIfHistoricalRecordsWouldBeLeft(int mergeFromId, MergeOptions options, CancellationToken ct)
    {
        var left = (!options.TransferSeasonStats && (await _context.PlayerSeasonStats.AnyAsync(x => x.UserID == mergeFromId, ct) || await _context.MatchmakingPlayerStats.AnyAsync(x => x.UserID == mergeFromId, ct)))
            || (!options.TransferChampionships && await _context.PlayerChampionships.AnyAsync(x => x.UserID == mergeFromId, ct));
        if (left)
            throw new UserHasHistoricalStatsException(mergeFromId);
    }

    /// <summary>The source's season stats move to the target; a clash was refused before the merge. Its matchmaking
    /// seasons' too.</summary>
    private async Task TransferSeasonStats(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var stats = await _context.PlayerSeasonStats.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        foreach (var stat in stats)
            stat.UserID = mergeToId;
        foreach (var stat in await _context.MatchmakingPlayerStats.Where(x => x.UserID == mergeFromId).ToListAsync(ct))
            stat.UserID = mergeToId;
        await _recorder.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The source's championships move to the target, except titles the target already holds for that season and tier
    /// ((SeasonID, Tier, UserID) is unique): one person listed twice on a roster under two gamertags.
    /// </summary>
    private async Task TransferChampionships(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var championships = await _context.PlayerChampionships.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        var held = await _context.PlayerChampionships.Where(x => x.UserID == mergeToId).ToListAsync(ct);
        foreach (var championship in championships)
        {
            var same = held.FirstOrDefault(h => h.SeasonID == championship.SeasonID && string.Equals(h.Tier, championship.Tier, StringComparison.OrdinalIgnoreCase));
            if (same is not null)
            {
                // One title per player: the target keeps its own, the captaincy too if the source had it
                same.IsCaptain |= championship.IsCaptain;
                _context.PlayerChampionships.Remove(championship);
            }
            else
                championship.UserID = mergeToId;
        }
    }

    /// <summary>
    /// Pending gamertag claims about the source (its placeholder) can't stay as they are once it is gone: merged into
    /// another placeholder, they are about that one; merged into a real user, the user's own claim is settled as approved
    /// (the placeholder is theirs now) and anyone else's as rejected (it can't be theirs any more), so approving one never
    /// writes history onto the wrong account.
    /// </summary>
    private async Task SettleClaims(User mergeTo, int mergeFromId, CancellationToken ct)
    {
        var claims = await _context.GamertagClaims
            .Where(c => c.PlaceholderUserID == mergeFromId && c.Status == GamertagClaimStatus.Pending)
            .ToListAsync(ct);
        foreach (var claim in claims)
        {
            if (mergeTo.IsDummyUser)
                claim.PlaceholderUserID = mergeTo.Id;
            else
                claim.Status = (claim.ClaimantUserID ?? mergeFromId) == mergeTo.Id || (claim.XboxUserID is not null && mergeTo.XboxUserID == claim.XboxUserID)
                    ? GamertagClaimStatus.Approved
                    : GamertagClaimStatus.Rejected;
        }

        // Claims the source made (its own past gamertags, a Discord match about it) are the target's now; one the target
        // already has open or approved stays the only one (one per kind, user and gamertag)
        var made = await _context.GamertagClaims.Where(c => c.ClaimantUserID == mergeFromId).ToListAsync(ct);
        var targets = await _context.GamertagClaims
            .Where(c => c.ClaimantUserID == mergeTo.Id && c.Status != GamertagClaimStatus.Rejected)
            .Select(c => new { c.Kind, c.NormalizedGamertag })
            .ToListAsync(ct);
        foreach (var claim in made)
        {
            if (claim.Status != GamertagClaimStatus.Rejected && targets.Any(t => t.Kind == claim.Kind && t.NormalizedGamertag == claim.NormalizedGamertag))
            {
                claim.Status = GamertagClaimStatus.Rejected;
                claim.RejectionReason = "The same claim was made by the account it was merged into";
            }
            claim.ClaimantUserID = mergeTo.Id;
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
        await _recorder.SaveChangesAsync(ct);
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
    public bool DeleteMergeFrom { get; set; } = true;

    /// <summary>What caused the merge, for its audit record (<see cref="UserMerge"/>).</summary>
    public MergeTrigger Trigger { get; set; } = MergeTrigger.Manual;
    /// <summary>Why, e.g. "claim 12", for the audit record.</summary>
    public string? Reason { get; set; }
    /// <summary>Who caused it; the signed-in user when null.</summary>
    public int? TriggeredByUserID { get; set; }

    /// <summary>The default transfers, recorded as caused by <paramref name="trigger"/>.</summary>
    public static MergeOptions For(MergeTrigger trigger, string? reason = null, int? triggeredByUserId = null)
        => new() { Trigger = trigger, Reason = reason, TriggeredByUserID = triggeredByUserId };
}

/// <summary>The two users can't be merged as they are, e.g. both are on different teams in one season.</summary>
public class MergeConflictException(string message) : Exception(message);

/// <summary>
/// A user with historical stats or championships can't be deleted (the foreign keys are restricted): merge it
/// into the account they belong to instead, which moves them.
/// </summary>
public class UserHasHistoricalStatsException(int userId)
    : Exception($"User {userId} has historical stats; merge instead of deleting.")
{
    public int UserId { get; } = userId;
}
