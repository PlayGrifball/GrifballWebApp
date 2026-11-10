using OpenTelemetry.Trace;

namespace GrifballWebApp.Server.Telemetry;

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
