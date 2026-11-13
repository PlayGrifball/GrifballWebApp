using OpenTelemetry.Trace;

namespace GrifballWebApp.Server.Telemetry;

/// <summary>
/// The app's sampling: everything (parent-based, so a span follows its parent), except two kinds of root span that
/// would be one-span traces crowding the Telemetry page:
/// <list type="bullet">
/// <item>The Quartz scheduler's own loop: acquiring triggers, firing them and completing them, every few seconds
/// whether a job runs or not. A job's run is traced; the store calls a request makes are in that request's trace.</item>
/// <item>A Redis command with no current Activity: the SignalR backplane's subscribe/publish handlers, or a cache or
/// Halo call pacer call outside a request or job. A Redis command under a request or job (a child span) is kept,
/// following its parent as usual.</item>
/// </list>
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

    /// <summary>The Redis instrumentation's db system attribute, old (<c>db.system</c>) or new (<c>db.system.name</c>)
    /// semantic conventions: set on every span it creates.</summary>
    private static readonly string[] RedisSystemTagKeys = ["db.system", "db.system.name"];
    private const string Redis = "redis";

    private readonly Sampler _inner = new ParentBasedSampler(new AlwaysOnSampler());

    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        if (samplingParameters.ParentContext.TraceId == default
            && (LoopSpans.Contains(samplingParameters.Name) || IsRedis(samplingParameters)))
            return new SamplingResult(SamplingDecision.Drop);
        return _inner.ShouldSample(samplingParameters);
    }

    private static bool IsRedis(in SamplingParameters samplingParameters)
        => samplingParameters.Tags?.Any(tag => Redis.Equals(tag.Value) && RedisSystemTagKeys.Contains(tag.Key)) == true;
}
