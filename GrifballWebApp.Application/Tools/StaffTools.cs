using System.ComponentModel;
using GrifballWebApp.Application.Commissioner;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.Scheduler;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Application.Tools;

/// <summary>A season's schedule recommendation: whether one is being worked out, and the latest one worked out (null: none yet).</summary>
public sealed record ScheduleRecommendationStatusDto(int SeasonID, OnDemandJobStatusDto Run, ScheduleRecommendationDto? Latest);

/// <summary>
/// What staff's pages show, read only: the commissioner dashboard, the pending gamertag claims, a season's schedule
/// recommendation and the background jobs. Each tool for the roles its page is for (<see cref="ToolRolesAttribute"/>); the MCP
/// server lists them to those alone, and the Discord bot's chat, which answers in a public channel, doesn't get them.
/// </summary>
public class StaffTools
{
    private readonly ToolCaller _caller;
    private readonly CommissionerDashboardService _dashboard;
    private readonly GamertagClaimService _claims;
    private readonly SeasonService _seasons;
    private readonly SeasonVisibility _visibility;
    private readonly ScheduleRecommendations _recommendations;
    private readonly IScheduleRecommendationRuns _runs;
    private readonly IJobControl _jobs;

    public StaffTools(ToolCaller caller, CommissionerDashboardService dashboard, GamertagClaimService claims, SeasonService seasons,
        SeasonVisibility visibility, ScheduleRecommendations recommendations, IScheduleRecommendationRuns runs, IJobControl jobs)
    {
        _caller = caller;
        _dashboard = dashboard;
        _claims = claims;
        _seasons = seasons;
        _visibility = visibility;
        _recommendations = recommendations;
        _runs = runs;
        _jobs = jobs;
    }

    [Description("The commissioner dashboard: the pending reschedule requests (each one's ID for decide_reschedule), the matches overdue to be "
        + "played with how many hours late, the seasons still to start that won't be public by the time their signups open, and the counts. "
        + "Commissioners and sysadmins.")]
    [ToolRoles(SiteRoles.Commissioner, SiteRoles.Sysadmin)]
    public async Task<CommissionerDashboardDto> GetCommissionerDashboard(CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return await _dashboard.GetAsync(ct);
    }

    [Description("The gamertag claims waiting for a decision: each one's ID (for decide_gamertag_claim), the gamertag, who claims it and the "
        + "season it was played in. Sysadmins.")]
    [ToolRoles(SiteRoles.Sysadmin)]
    public async Task<List<GamertagClaimDto>> ListPendingGamertagClaims(CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return await _claims.List(GamertagClaimStatus.Pending, ct);
    }

    [Description("A season's schedule recommendation, as the scheduler page shows it: whether one is being worked out (run_schedule_recommendation "
        + "starts one), and the latest worked out, with its ID (for apply_schedule_recommendation) and each match's suggested time. "
        + "Commissioners and sysadmins.")]
    [ToolRoles(SiteRoles.Commissioner, SiteRoles.Sysadmin)]
    public async Task<ScheduleRecommendationStatusDto> GetScheduleRecommendation(
        [Description("The season's ID; left out, the running season, else the next to start.")] int? seasonID = null,
        CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        var id = await StaffSeasons.SeasonOrDefault(_seasons, _visibility, _caller, seasonID, SeasonPurpose.Active, ct);
        return new ScheduleRecommendationStatusDto(id, await _runs.Status(id, ct), await _recommendations.Latest(id, ct));
    }

    [Description("The background jobs, as the jobs page lists them: each one's group and name (for run_job and set_job_paused), its state "
        + "(Scheduled, Paused, Off, Manual, OnDemand, Error, Blocked) and whether it's on. Sysadmins.")]
    [ToolRoles(SiteRoles.Sysadmin)]
    public async Task<List<JobSummaryDto>> ListJobs(CancellationToken ct = default)
    {
        _caller.RequireRolesOf();
        return await _jobs.List(ct);
    }
}
