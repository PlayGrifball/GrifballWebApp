using System.Diagnostics;
using OpenTelemetry.Trace;
using Quartz.Diagnostics;

namespace GrifballWebApp.Server.Telemetry;

/// <summary>
/// Quartz's traces, without its noise. Each job run is a trace of its own (<c>Quartz.Job.Execute</c>, with the job's HTTP
/// and database calls under it), but the scheduler's polling loop makes a root trace of each store call it makes every few
/// seconds (<see cref="SchedulerLoopSampler"/> drops those), and the jobs that run every 10 seconds would make one each
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

/// <summary>
/// The app's sampling: everything (parent-based, so a span follows its parent), except the root spans of the Quartz
/// scheduler's own loop: acquiring triggers, firing them and completing them, every few seconds whether a job runs or not.
/// A job's run is traced; the store calls a request makes are in that request's trace.
/// </summary>
public sealed class SchedulerLoopSampler : Sampler
{
    public static readonly IReadOnlySet<string> LoopSpans = new HashSet<string>(StringComparer.Ordinal)
    {
        "Quartz.JobStore.AcquireNextTriggers",
        "Quartz.JobStore.TriggersFired",
        "Quartz.JobStore.TriggeredJobComplete",
        "Quartz.JobStore.ReleaseAcquiredTrigger",
    };

    private readonly Sampler _inner = new ParentBasedSampler(new AlwaysOnSampler());

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.ParentContext.TraceId == default && LoopSpans.Contains(samplingParameters.Name))
            return new SamplingResult(SamplingDecision.Drop);
        return _inner.ShouldSample(samplingParameters);
    }
}
