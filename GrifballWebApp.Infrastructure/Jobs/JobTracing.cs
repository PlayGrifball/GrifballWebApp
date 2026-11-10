using System.Diagnostics;
using Quartz.Diagnostics;

namespace GrifballWebApp.Infrastructure.Jobs;

/// <summary>
/// Quartz's traces, without its noise. Each job run is a trace of its own (<c>Quartz.Job.Execute</c>, with the job's HTTP
/// and database calls under it), but the scheduler's polling loop makes a root trace of each store call it makes every few
/// seconds (the web host's <c>SchedulerLoopSampler</c> drops those), and the jobs that run every 10 seconds would make one each
/// run (they call <see cref="DontRecordThisRun"/>).
/// </summary>
public static class JobTracing
{
    /// <summary>
    /// Leaves the current job run out of the traces, with everything it does: for a job that runs every few seconds. Its
    /// span is started before the job knows which it is (Quartz tags it after), so the job says so itself.
    /// </summary>
    public static void DontRecordThisRun()
    {
        if (Activity.Current is { } run && run.Source.Name == QuartzInstrumentation.ActivitySourceName)
        {
            run.IsAllDataRequested = false;
            // Its children (ParentBased sampling) aren't sampled either
            run.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }
}
