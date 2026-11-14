using GrifballWebApp.Application.Jobs;
using Quartz;

namespace GrifballWebApp.Infrastructure.Jobs;

/// <summary>The jobs page's controls (<see cref="IJobControl"/>) on <see cref="JobsService"/>.</summary>
public sealed class JobControl(JobsService jobs) : IJobControl
{
    public async Task<List<JobSummaryDto>> List(CancellationToken ct = default)
        => (await jobs.Overview(ct)).Jobs.Select(j => new JobSummaryDto(j.Group, j.Name, j.State, j.Enabled)).ToList();

    public async Task<JobControlResult> RunNow(string group, string name, int? byUserID, CancellationToken ct = default)
        => Of(await jobs.RunNow(new JobKey(name, group), byUserID, ct));

    public async Task<JobControlResult> SetPaused(string group, string name, bool paused, string? by, CancellationToken ct = default)
        => Of(await jobs.SetPaused(new JobKey(name, group), paused, by, ct));

    private static JobControlResult Of(JobChange change) => change switch
    {
        JobChange.Done => JobControlResult.Done,
        JobChange.NotFound => JobControlResult.NotFound,
        _ => JobControlResult.Conflict,
    };
}
