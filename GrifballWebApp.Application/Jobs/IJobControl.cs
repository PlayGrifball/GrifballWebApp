namespace GrifballWebApp.Application.Jobs;

/// <summary>A background job as the jobs page lists it: its group and name (together its key), its state, and whether it's on.</summary>
public sealed record JobSummaryDto(string Group, string Name, string State, bool Enabled);

/// <summary>What asking a job to run or to pause did.</summary>
public enum JobControlResult
{
    Done,
    NotFound,
    /// <summary>Not now: it's off, running, waits for its page's input, or has no trigger yet.</summary>
    Conflict,
}

/// <summary>
/// The jobs page's controls, for the Application layer (the agents' tools): the jobs, running one now, pausing or resuming one.
/// Infrastructure's Quartz-backed JobsService does them.
/// </summary>
public interface IJobControl
{
    Task<List<JobSummaryDto>> List(CancellationToken ct = default);

    Task<JobControlResult> RunNow(string group, string name, int? byUserID, CancellationToken ct = default);

    Task<JobControlResult> SetPaused(string group, string name, bool paused, string? by, CancellationToken ct = default);
}
