using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Profile;

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
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    private readonly ICurrentUserService? _currentUser;
    /// <summary>The merge in progress: every SaveChanges of it goes through this, so the audit record has all it changed.</summary>
    private MergeManifestRecorder _recorder = null!;

    public UserMergeService(IApplicationDbContext context, TimeProvider? time = null, ICurrentUserService? currentUser = null)
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
        await ThrowIfUnsettled(mergeToId, mergeFromId, options, ct);

        await _context.StartTransactionAsync(ct);

        // What the caller left unsaved (a claim it just added) is its own change, not the merge's: saved before the
        // merge starts recording
        await _context.SaveChangesAsync(ct);
        _recorder = new MergeManifestRecorder(_context);
        var sourceUserName = mergeFrom.UserName;
        var targetUserName = mergeTo.UserName;

        await TransferLoginsAndDiscord(mergeTo, mergeFrom, options, ct);

        if (options.TransferXbox)
            mergeTo.TakeXboxFrom(mergeFrom);

        if (options.TransferTeamPlayers)
            await TransferTeamPlayers(mergeToId, mergeFromId, ct);

        if (options.TransferExperiences)
            await TransferExperiences(mergeTo, mergeFrom, ct);

        if (options.TransferSignups)
            await TransferSignups(mergeToId, mergeFromId, ct);

        // Matchmaking history and reschedule requests always move: deleting the source would otherwise take its
        // matched players (and their votes) with it, or fail on the reschedules' restricted foreign keys.
        await TransferMatchmaking(mergeTo, mergeFrom, ct);
        await TransferReschedules(mergeToId, mergeFromId, ct);
        // Recorded games are what the player played, whichever account: they always move (their user key is restricted)
        foreach (var game in await _context.RecordedGames.Where(g => g.Participants.Any(p => p.UserID == mergeFromId)).Include(g => g.Participants).ToListAsync(ct))
            game.ReassignPlayer(mergeFromId, mergeToId);

        // Historical stats and championships: their foreign keys to the user are restricted, so they move
        // before the source is deleted (a merge that would leave them on a deleted source was refused above).
        if (options.TransferSeasonStats)
            await TransferSeasonStats(mergeToId, mergeFromId, ct);

        if (options.TransferChampionships)
            await TransferChampionships(mergeToId, mergeFromId, ct);

        await SettleClaims(mergeTo, mergeFromId, options.TriggeredByUserID ?? _currentUser?.GetCurrentUserId(), ct);

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
        _context.UserMerges.Add(UserMerge.Record(
            mergedAt: _time.GetUtcNow().UtcDateTime,
            trigger: options.Trigger,
            triggeredByUserID: options.TriggeredByUserID ?? _currentUser?.GetCurrentUserId(),
            reason: options.Reason,
            sourceUserID: mergeFromId,
            sourceUserName: sourceUserName,
            targetUserID: mergeToId,
            targetUserName: targetUserName,
            manifest: manifest.Serialize(),
            rowsMoved: manifest.Entries.Count(e => e.Op == MergeManifestOp.Modified),
            rowsRemoved: manifest.Entries.Count(e => e.Op == MergeManifestOp.Deleted),
            rowsAdded: manifest.Entries.Count(e => e.Op == MergeManifestOp.Added)));
        await _context.SaveChangesAsync(ct);

        await _context.CommitTransactionAsync(ct);
    }

    /// <summary>The conflicts no merge rule can settle, for what the options transfer: thrown.</summary>
    private async Task ThrowIfUnsettled(int mergeToId, int mergeFromId, MergeOptions options, CancellationToken ct)
    {
        if (options.TransferTeamPlayers)
            await ThrowIfOnDifferentTeamsInASeason(mergeToId, mergeFromId, ct);
        if (options.TransferSeasonStats)
            await ThrowIfBothHaveStatsForASeason(mergeToId, mergeFromId, ct);
        if (options.DeleteMergeFrom)
            await ThrowIfHistoricalRecordsWouldBeLeft(mergeFromId, options, ct);
        await ThrowIfBothInAnActiveMatch(mergeToId, mergeFromId, ct);
    }

    /// <summary>
    /// External logins (Auth.UserLogins) are what an external sign-in such as "Sign in with Discord" looks up.
    /// They move in the same SaveChanges as the other transfers, before the source is deleted, so a failed
    /// merge never loses them. The key is (LoginProvider, ProviderKey), so only UserId changes and the target
    /// can hold any number of logins, including several for one provider. Then the Discord account.
    /// </summary>
    private async Task TransferLoginsAndDiscord(User mergeTo, User mergeFrom, MergeOptions options, CancellationToken ct)
    {
        var mergeToId = mergeTo.Id;
        var mergeFromId = mergeFrom.Id;
        var logins = await _context.UserLogins.Where(l => l.UserId == mergeFromId).ToListAsync(ct);
        if (options.TransferLogins)
        {
            logins.ForEach(l => l.UserId = mergeToId);
        }

        if (options.TransferDiscord)
        {
            if (mergeFrom.DiscordUserID is not null)
                mergeTo.TakeDiscordFrom(mergeFrom);
            else if (mergeTo.DiscordUserID is null && options.TransferLogins)
            {
                // A login-only account (signed in with Discord, never linked by the bot) has no DiscordUserID.
                // Its Discord login key is the Discord user ID, so link the bot account from it, but only when that
                // Discord user is known and not linked to anyone else; a target with a link of its own keeps it.
                // (The target's own link is also checked here, not only by User.LinkDiscord: CodeQL's
                // cs/user-controlled-bypass flags a request option alone guarding this.)
                await LinkDiscordFromLogin(mergeTo, logins, ct);
            }
        }
    }

    /// <summary>Why a merge with the default options would be refused (different teams in a season, clashing stats, both
    /// in a match being played), or null: the same rules <see cref="Merge"/> applies, for suggesting merges without making them.</summary>
    public async Task<string?> ConflictAsync(int mergeToId, int mergeFromId, CancellationToken ct = default)
    {
        try
        {
            await ThrowIfOnDifferentTeamsInASeason(mergeToId, mergeFromId, ct);
            await ThrowIfBothHaveStatsForASeason(mergeToId, mergeFromId, ct);
            await ThrowIfBothInAnActiveMatch(mergeToId, mergeFromId, ct);
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
        var target = await _context.Teams.Rosters().Where(tp => tp.UserID == mergeToId)
            .Select(tp => new { tp.TeamID, tp.Team.SeasonID, tp.Team.Season.SeasonName })
            .ToListAsync(ct);
        var source = await _context.Teams.Rosters().Where(tp => tp.UserID == mergeFromId)
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
        var clash = await _context.PlayerSeasonStats
            .Where(PlayerSeasonStat.ClashesOnMerge(_context.PlayerSeasonStats, mergeFromId, mergeToId))
            .OrderBy(s => s.SeasonID).ThenBy(s => s.IsPlayoffs).ThenBy(s => s.StintIndex)
            .Select(s => new { s.StintIndex, s.IsPlayoffs, s.Season.SeasonName })
            .FirstOrDefaultAsync(ct);
        if (clash is not null)
            throw new MergeConflictException($"Both users have historical stats for {(clash.IsPlayoffs ? "the playoffs of " : "")}season {clash.SeasonName} (stint {clash.StintIndex}). Remove or fix one of them first.");
        var matchmaking = await _context.MatchmakingPlayerStats
            .Where(MatchmakingPlayerStat.ClashesOnMerge(_context.MatchmakingPlayerStats, mergeFromId, mergeToId))
            .OrderBy(s => s.MatchmakingSeasonID)
            .Select(s => s.MatchmakingSeason.Name)
            .FirstOrDefaultAsync(ct);
        if (matchmaking is not null)
            throw new MergeConflictException($"Both users have historical stats for {matchmaking}. Remove or fix one of them first.");
    }

    /// <summary>
    /// Both users playing one Late League match that is still going: one person twice, voting twice, and which of the two
    /// stays in it would change the vote. Refused until the match ends (<see cref="MatchedMatch.ReassignPlayer"/>).
    /// </summary>
    private async Task ThrowIfBothInAnActiveMatch(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var match = await _context.MatchedMatches
            .Where(m => m.Status == MatchedMatchStatus.Active)
            .Where(MatchedMatch.HasPlayer(mergeToId))
            .Where(MatchedMatch.HasPlayer(mergeFromId))
            .Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);
        if (match is not null)
            throw new MergeConflictException($"Both users are in Late League match {match}, which is still being played. Wait for it to end first.");
    }

    /// <summary>
    /// A user is never deleted with historical records left on it (their foreign keys are restricted): a merge that
    /// is told to delete the source but not to move them is refused before anything changes.
    /// </summary>
    private async Task ThrowIfHistoricalRecordsWouldBeLeft(int mergeFromId, MergeOptions options, CancellationToken ct)
    {
        var left = (!options.TransferSeasonStats && (await _context.PlayerSeasonStats.AnyAsync(x => x.UserID == mergeFromId, ct) || await _context.MatchmakingPlayerStats.AnyAsync(x => x.UserID == mergeFromId, ct)))
            || (!options.TransferChampionships && await _context.Seasons.Championships().AnyAsync(x => x.UserID == mergeFromId, ct));
        if (left)
            throw new UserHasHistoricalStatsException(mergeFromId);
    }

    /// <summary>The source's season stats move to the target; a clash was refused before the merge. Its matchmaking
    /// seasons' too.</summary>
    private async Task TransferSeasonStats(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var stats = await _context.PlayerSeasonStats.Where(x => x.UserID == mergeFromId).ToListAsync(ct);
        foreach (var stat in stats)
            stat.ReassignUser(mergeFromId, mergeToId);
        foreach (var stat in await _context.MatchmakingPlayerStats.Where(x => x.UserID == mergeFromId).ToListAsync(ct))
            stat.ReassignUser(mergeFromId, mergeToId);
        await _recorder.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The source's championships move to the target, through each season that has one (<see cref="Season.MergeTitles"/>),
    /// except titles the target already holds for that season and tier ((SeasonID, Tier, UserID) is unique): one person
    /// listed twice on a roster under two gamertags.
    /// </summary>
    private async Task TransferChampionships(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var seasons = await _context.Seasons.WithChampionships()
            .Where(s => s.PlayerChampionships.Any(c => c.UserID == mergeFromId))
            .ToListAsync(ct);
        foreach (var season in seasons)
            season.MergeTitles(mergeFromId, mergeToId);
    }

    /// <summary>
    /// Pending gamertag claims about the source (its placeholder) can't stay as they are once it is gone: merged into
    /// another placeholder, they are about that one; merged into a real user, the user's own claim is settled as approved
    /// (the placeholder is theirs now) and anyone else's as rejected (it can't be theirs any more), so approving one never
    /// writes history onto the wrong account.
    /// </summary>
    private async Task SettleClaims(User mergeTo, int mergeFromId, int? decidedBy, CancellationToken ct)
    {
        // Decided by whoever made the merge, when it was made: the claims' own rules (GamertagClaim) say how
        var decidedAt = _time.GetUtcNow().UtcDateTime;
        var claims = await _context.GamertagClaims
            .Where(c => c.PlaceholderUserID == mergeFromId && c.Status == GamertagClaimStatus.Pending)
            .ToListAsync(ct);
        foreach (var claim in claims)
            claim.PlaceholderMergedInto(mergeTo, decidedBy, decidedAt);

        // Claims the source made (its own past gamertags, a Discord match about it) are the target's now; one the target
        // already has open or approved stays the only one (one per kind, user and gamertag)
        var made = await _context.GamertagClaims.Where(c => c.ClaimantUserID == mergeFromId).ToListAsync(ct);
        var targets = await _context.GamertagClaims.Where(GamertagClaim.Open).Where(c => c.ClaimantUserID == mergeTo.Id).ToListAsync(ct);
        foreach (var claim in made)
            claim.ClaimantMergedInto(mergeTo, targets, decidedBy, decidedAt);

        // Claims the source decided (a sysadmin's, or settled by a merge it made, this one included) were decided by the
        // target now: the decider has no foreign key, so nothing else would move it
        var decided = await _context.GamertagClaims.Where(c => c.DecidedByUserID == mergeFromId).ToListAsync(ct);
        foreach (var claim in decided.Concat(claims).Concat(made))
            claim.ReassignDecider(mergeFromId, mergeTo.Id);
    }

    /// <summary>
    /// The source's team memberships move to the target. Where both are on the same team the target keeps its own row
    /// (which becomes the captain's if the source's was), and the source's goes: (UserID, TeamID) is unique.
    /// </summary>
    private async Task TransferTeamPlayers(int mergeToId, int mergeFromId, CancellationToken ct)
    {
        var teams = await _context.Teams.WithRoster().Where(t => t.TeamPlayers.Any(tp => tp.UserID == mergeFromId)).ToListAsync(ct);
        foreach (var team in teams)
            team.MergePlayer(mergeFromId, mergeToId);
        // The captain moves before the source's row goes (Teams.CaptainID has no cascade).
        await _recorder.SaveChangesAsync(ct);
    }

    /// <summary>
    /// UserExperience is only which game versions a user has played, keyed (UserID, GameVersionID): the key can't be
    /// changed, so the source's rows are removed and the target gets any versions it doesn't already have.
    /// </summary>
    private async Task TransferExperiences(User mergeTo, User mergeFrom, CancellationToken ct)
    {
        await _context.Entry(mergeTo).Collection(u => u.PersonExperiences).LoadAsync(ct);
        await _context.Entry(mergeFrom).Collection(u => u.PersonExperiences).LoadAsync(ct);
        mergeTo.TakeExperiencesFrom(mergeFrom);
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
                signup.MoveToUser(mergeToId);
        }
    }

    /// <summary>
    /// The source's matched players (and through them its winner and kick votes) move to the target, each by its match
    /// (<see cref="MatchedMatch.ReassignPlayer"/>): in an ended match the target played too, the source's player goes. Its record
    /// is combined: wins and losses add up, MMR is the average of the two weighted by games played (the target's if
    /// neither played), and the streaks are those of whichever account has the games (the target's if both).
    /// A queued source leaves the queue.
    /// </summary>
    private async Task TransferMatchmaking(User mergeTo, User mergeFrom, CancellationToken ct)
    {
        var matches = await _context.MatchedMatches
            .Where(MatchedMatch.HasPlayer(mergeFrom.Id))
            .WithPlayersAndVotes()
            .ToListAsync(ct);
        foreach (var match in matches)
        {
            // Refused before the merge began (ThrowIfBothInAnActiveMatch): only a match the queue started since gets here. A race,
            // not a conflict to settle: not a MergeConflictException, which callers take to mean nothing changed and carry
            // on. This fails the request, and its context rolls the transaction back.
            if (match.ReassignPlayer(mergeFrom.Id, mergeTo.Id) is PlayerReassignment.BothInActiveMatch)
                throw new InvalidOperationException($"Both users joined Late League match {match.Id} while they were being merged; nothing was merged.");
        }

        mergeTo.TakeLateLeagueRecordFrom(mergeFrom);

        var queued = await _context.QueuedPlayer.FirstOrDefaultAsync(QueuedPlayer.Of(mergeFrom.Id), ct);
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
            reschedule.ReassignUser(mergeFromId, mergeToId);
    }


    private async Task LinkDiscordFromLogin(User mergeTo, List<UserLogin> logins, CancellationToken ct)
    {
        var discordLogin = logins.FirstOrDefault(l => l.LoginProvider == DiscordProvider);
        if (discordLogin is null || !long.TryParse(discordLogin.ProviderKey, out var discordUserId))
            return;

        var known = await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == discordUserId, ct);
        var linkedElsewhere = await _context.Users.AnyAsync(u => u.DiscordUserID == discordUserId, ct);
        if (!known || linkedElsewhere)
            return;
        // Linked, or refused (DiscordLinking.LinkedToOther) for a target linked to another Discord account: it keeps that
        // one, and only the login moves. Either way the merge goes on.
        _ = mergeTo.LinkDiscord(discordUserId);
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
