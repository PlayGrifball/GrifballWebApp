using System.ComponentModel;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Profile;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Sysadmins' actions, as their pages and /admin do them, on the same services: deciding gamertag claims (merges go through the
/// claim's process, never by hand), pulling a Halo game and refreshing the medals. A service's refusal is the answer.
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

    public SysadminTools(ToolCaller caller, GamertagClaimService claims, IDataPullService dataPull, MatchDetailsService matchDetails, IJobControl jobs,
        ISetGamertagService setGamertag, IApplicationDbContext context)
    {
        _setGamertag = setGamertag;
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
        + "refused when the user has a gamertag already or another user holds it; a placeholder holding it is merged into the user. Name the user by their site "
        + "user ID (search gives it) or by their Discord user ID. Sysadmins.")]
    [ToolReachesHalo]
    public async Task<string> SetUserGamertag(
        [Description("The Xbox gamertag to set.")] string gamertag,
        [Description("The site user's ID: this or discordUserID.")] int? userID = null,
        [Description("The user's Discord user ID: this or userID.")] long? discordUserID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        if (string.IsNullOrWhiteSpace(gamertag))
            throw new ToolException("Give the gamertag to set.");
        if ((userID is null) == (discordUserID is null))
            throw new ToolException("Name the user by userID or by discordUserID, one of them.");

        var id = userID ?? await _context.Users.Where(u => u.DiscordUserID == discordUserID && !u.IsDummyUser).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct)
            ?? throw new ToolException("No site account is linked to that Discord user. They make one by using /setgamertag themselves or signing in to the site with Discord.");
        var error = await _setGamertag.SetGamertag(id, gamertag.Trim(), ct);
        return error is null ? $"Set: user {id} has the gamertag {gamertag.Trim()}." : throw new ToolException(error);
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
