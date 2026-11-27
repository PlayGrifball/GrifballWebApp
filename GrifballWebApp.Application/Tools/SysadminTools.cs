using System.ComponentModel;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Sysadmins' actions, as their pages and /admin do them, on the same services: deciding gamertag claims, setting gamertags, finding and merging
/// duplicate users (audited, undoable), pulling a Halo game and refreshing the medals. A service's refusal is the answer.
/// </summary>
[ToolRoles(SiteRoles.Sysadmin)]
public class SysadminTools
{
    private readonly ToolCaller _caller;
    private readonly GamertagClaimService _claims;
    private readonly IDataPullService _dataPull;
    private readonly MatchDetailsService _matchDetails;
    private readonly IJobControl _jobs;
    private readonly ISetGamertagService _setGamertag;
    private readonly IApplicationDbContext _context;
    private readonly IDiscordGamertagSetter _discordSetter;
    private readonly DuplicateSuggestionService _duplicates;
    private readonly IUserMergeService _userMerges;
    private readonly UserMergeAuditService _auditedMerges;

    public SysadminTools(ToolCaller caller, GamertagClaimService claims, IDataPullService dataPull, MatchDetailsService matchDetails, IJobControl jobs,
        ISetGamertagService setGamertag, IApplicationDbContext context, IDiscordGamertagSetter discordSetter,
        DuplicateSuggestionService duplicates, IUserMergeService userMerges, UserMergeAuditService auditedMerges)
    {
        _setGamertag = setGamertag;
        _discordSetter = discordSetter;
        _duplicates = duplicates;
        _userMerges = userMerges;
        _auditedMerges = auditedMerges;
        _context = context;
        _matchDetails = matchDetails;
        _jobs = jobs;
        _caller = caller;
        _claims = claims;
        _dataPull = dataPull;
    }

    [Description("Approves or rejects a gamertag claim (list_pending_gamertag_claims lists them), as the claims page does: approved, the claimed "
        + "account's history becomes the claimant's. Rejecting can give a reason. Sysadmins.")]
    public async Task<string> DecideGamertagClaim(
        [Description("The claim's ID.")] int claimID,
        [Description("True to approve, false to reject.")] bool approve,
        [Description("Why it's rejected, optional.")] string? reason = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        if (approve)
            await _claims.Approve(claimID, ct);
        else
            await _claims.Reject(claimID, reason, ct);
        return approve ? "Approved." : "Rejected.";
    }

    [Description("Links an Xbox account to a site user by setting that user's gamertag, as the player does with /setgamertag or on their profile, with the same rules: "
        + "refused when the user has a gamertag already or another user holds it (the site's Edit User page, in a browser, can change one or take it over); a placeholder holding it is merged into the user. Name the user by their site "
        + "user ID (search gives it) or by their Discord user ID: a Discord user with no account yet gets one made, as /setgamertag makes it for them "
        + "(give their Discord username too, which the new account is named after), or takes over the account signed in with their Discord and a placeholder linked to them. Sysadmins.")]
    [ToolReachesHalo]
    public async Task<string> SetUserGamertag(
        [Description("The Xbox gamertag to set.")] string gamertag,
        [Description("The site user's ID: this or discordUserID.")] int? userID = null,
        [Description("The user's Discord user ID: this or userID.")] long? discordUserID = null,
        [Description("The user's Discord username: needed when they have no site account yet, to make one.")] string? discordUsername = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        if (string.IsNullOrWhiteSpace(gamertag))
            throw new ToolException("Give the gamertag to set.");
        if ((userID is null) == (discordUserID is null))
            throw new ToolException("Name the user by userID or by discordUserID, one of them.");

        gamertag = gamertag.Trim();
        if (discordUserID is { } discordID)
        {
            if (discordID <= 0)
                throw new ToolException("That isn't a Discord user ID.");
            var discordError = await _discordSetter.SetGamertagForDiscordUser((ulong)discordID, discordUsername?.Trim(), gamertag, ct);
            return discordError is null ? $"Set: Discord user {discordID} has the gamertag {gamertag}." : throw new ToolException(discordError);
        }

        var id = userID!.Value;
        var isDummy = await _context.Users.Where(u => u.Id == id).Select(u => (bool?)u.IsDummyUser).FirstOrDefaultAsync(ct)
            ?? throw new ToolException($"There is no user {id}.");
        if (isDummy)
            throw new ToolException($"User {id} is a placeholder from the archive, not an account: merge it into the player's account (list_duplicate_users, merge_duplicate_user), or set the gamertag by their Discord user ID.");
        var error = await _setGamertag.SetGamertag(id, gamertag, ct);
        return error is null ? $"Set: user {id} has the gamertag {gamertag}." : throw new ToolException(error + " A sysadmin can change or take over a gamertag on the site's Edit User page.");
    }

    [Description("Merges a placeholder (a gamertag from the archive) into another user it duplicates, as the claims page's Merge does (list_duplicate_users lists the pairs): "
        + "its seasons, teams and stats become the target's, and it is deleted. Audited, and undo_user_merge puts it back. Refused when the seasons conflict, "
        + "or when the source isn't a placeholder (merge_users merges any two). Sysadmins.")]
    public async Task<string> MergeDuplicateUser(
        [Description("The placeholder's user ID, merged away.")] int sourceUserID,
        [Description("The user ID it is merged into.")] int targetUserID,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await Merging(() => _duplicates.Merge(sourceUserID, targetUserID, ct));
        return $"Merged: user {sourceUserID} is now user {targetUserID}. list_user_merges gives the merge, to undo it.";
    }

    [Description("Says two users are different players, as the claims page's Dismiss does: the pair isn't suggested again. Sysadmins.")]
    public async Task<string> DismissDuplicateUser(
        [Description("One user's ID.")] int userID,
        [Description("The other user's ID.")] int otherUserID,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await Merging(() => _duplicates.Dismiss(userID, otherUserID, ct));
        return $"Dismissed: users {userID} and {otherUserID} aren't suggested as duplicates again.";
    }

    [Description("Merges one user into another, as the merge page does: the source's data moves onto the target and the source is deleted (unless told to keep it). "
        + "Audited, and undo_user_merge puts it back. Refused when the two can't be merged as they are (different teams in one season). Every transfer is on unless turned off. Sysadmins.")]
    public async Task<string> MergeUsers(
        [Description("The user ID that keeps everything: the target.")] int mergeToUserID,
        [Description("The user ID merged away: the source.")] int mergeFromUserID,
        [Description("Moves the Discord link; default true.")] bool transferDiscord = true,
        [Description("Moves the sign-in logins; default true.")] bool transferLogins = true,
        [Description("Moves the Xbox account; default true.")] bool transferXbox = true,
        [Description("Moves the team memberships; default true.")] bool transferTeamPlayers = true,
        [Description("Moves the experiences; default true.")] bool transferExperiences = true,
        [Description("Moves the signups; default true.")] bool transferSignups = true,
        [Description("Deletes the source user after the merge; default true.")] bool deleteMergeFrom = true,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        var options = new MergeOptions
        {
            TransferDiscord = transferDiscord,
            TransferLogins = transferLogins,
            TransferXbox = transferXbox,
            TransferTeamPlayers = transferTeamPlayers,
            TransferExperiences = transferExperiences,
            TransferSignups = transferSignups,
            DeleteMergeFrom = deleteMergeFrom,
            Trigger = MergeTrigger.Manual,
            TriggeredByUserID = _caller.UserID,
        };
        await Merging(() => _userMerges.Merge(mergeToUserID, mergeFromUserID, options, ct));
        return $"Merged: user {mergeFromUserID} into user {mergeToUserID}. list_user_merges gives the merge, to undo it.";
    }

    [Description("Puts back what a merge changed (list_user_merges gives the IDs), as the merges page does. Refused, with nothing changed, when it was undone already, "
        + "a later merge touched the same rows (undo that one first), or a row changed since. Sysadmins.")]
    public async Task<string> UndoUserMerge(
        [Description("The merge's ID.")] int mergeID,
        [Description("Why, optional.")] string? note = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        await Merging(() => _auditedMerges.Undo(mergeID, note, ct));
        return $"Undone: merge {mergeID} is put back.";
    }

    /// <summary>Runs a merge's step; a refusal the site would answer 404, 409 or 403 is the tool's answer.</summary>
    private static async Task Merging(Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception e) when (e is UserNotFoundException or MergeConflictException or MergeUndoRefusedException or GamertagClaimException)
        {
            throw new ToolException(e.Message);
        }
        catch (MergeDeniedToAccessTokenException e)
        {
            throw new ToolException(e.Message);
        }
        catch (ArgumentException e)
        {
            throw new ToolException(e.Message);
        }
        catch (DbUpdateException)
        {
            throw new ToolException("The data conflicts, so nothing was changed.");
        }
    }

    [Description("Pulls one Halo game's stats from Halo and saves them, as the admin page does (a game saved already stays as it is). "
        + "Refused when Halo has no such team game. Sysadmins.")]
    [ToolReachesHalo]
    public async Task<string> PullHaloGame([Description("The game's match ID, a GUID.")] Guid matchID)
    {
        _caller.RequireRolesOf();
        if (matchID == Guid.Empty)
            throw new ToolException("That isn't a game's match ID.");
        await _dataPull.GetAndSaveMatch(matchID);
        // The pull saves nothing, quietly, for a game Halo hasn't got or one that isn't a team game
        return await _matchDetails.ExistsAsync(matchID)
            ? "Pulled: get_game shows it."
            : throw new ToolException($"Halo has no team game {matchID}: nothing was saved.");
    }

    [Description("Downloads Halo's medals, as the admin page does: once, when there are none yet (refused once they're downloaded). Sysadmins.")]
    [ToolReachesHalo]
    public async Task<string> RefreshMedals()
    {
        _caller.RequireRolesOf();
        await _dataPull.DownloadMedals();
        return "The medals are downloaded.";
    }

    [Description("Runs a background job now, as the jobs page does (list_jobs gives each one's group and name). Refused for one that's off, "
        + "running, or needs its page's input. Sysadmins.")]
    public async Task<string> RunJob(
        [Description("The job's group.")] string group,
        [Description("The job's name.")] string name,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return Done(await _jobs.RunNow(group, name, _caller.UserID, ct), "It's running.");
    }

    [Description("Pauses or resumes a background job's schedule, as the jobs page does (list_jobs gives each one's group and name). Sysadmins.")]
    public async Task<string> SetJobPaused(
        [Description("The job's group.")] string group,
        [Description("The job's name.")] string name,
        [Description("True to pause, false to resume.")] bool paused,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return Done(await _jobs.SetPaused(group, name, paused, _caller.Name, ct), paused ? "It's paused." : "It's resumed.");
    }

    /// <summary>The answer for a job's change: <paramref name="done"/>, else the refusal saying why not.</summary>
    private static string Done(JobControlResult result, string done) => result switch
    {
        JobControlResult.Done => done,
        JobControlResult.NotFound => throw new ToolException("There is no such job: list_jobs gives each one's group and name."),
        JobControlResult.DeniedToAccessToken => throw new ToolException("An access token may not run this job."),
        _ => throw new ToolException("The job can't do that now: it's off, running, or needs its page's input."),
    };
}
