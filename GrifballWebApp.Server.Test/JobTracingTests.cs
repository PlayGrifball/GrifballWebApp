using System.Diagnostics;
using GrifballWebApp.Server.Telemetry;
using OpenTelemetry.Trace;
using Quartz.Diagnostics;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Server.Test;

/// <summary>Quartz's traces: a job run is traced, the scheduler loop's store calls and the 10-second jobs aren't.</summary>
[TestFixture]
[NonParallelizable] // An ActivityListener sees every source in the process
public class JobTracingTests
{
    private static SamplingDecision Decide(string name, ActivityContext parent = default)
        => new SchedulerLoopSampler().ShouldSample(new SamplingParameters(parent, ActivityTraceId.CreateRandom(), name, ActivityKind.Internal)).Decision;

    [Test]
    public void TheSchedulerLoopsStoreCalls_AreDropped_AsRoots()
    {
        var parent = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        using (Assert.EnterMultipleScope())
        {
            foreach (var loop in SchedulerLoopSampler.LoopSpans)
                Assert.That(Decide(loop), Is.EqualTo(SamplingDecision.Drop), loop);
            Assert.That(Decide("Quartz.JobStore.TriggersFired", parent), Is.EqualTo(SamplingDecision.RecordAndSample), "Under a sampled parent, it follows it");
            Assert.That(Decide("Quartz.Job.Execute"), Is.EqualTo(SamplingDecision.RecordAndSample), "A job run is traced");
            Assert.That(Decide("Quartz.JobStore.ScheduleJob"), Is.EqualTo(SamplingDecision.RecordAndSample));
            Assert.That(Decide("GET /api/Jobs/List"), Is.EqualTo(SamplingDecision.RecordAndSample));
        }
    }

    [Test]
    public void AJobThatRunsEvery10Seconds_LeavesItsRunOutOfTheTraces()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name is QuartzInstrumentation.ActivitySourceName or "Test",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        using var quartz = new ActivitySource(QuartzInstrumentation.ActivitySourceName);
        using var other = new ActivitySource("Test");

        using (var run = quartz.StartActivity("Quartz.Job.Execute"))
        {
            JobTracing.DontRecordThisRun();
            using (Assert.EnterMultipleScope())
            {
                Assert.That(run!.Recorded, Is.False);
                Assert.That(run.IsAllDataRequested, Is.False);
            }
        }

        using var request = other.StartActivity("GET /");
        JobTracing.DontRecordThisRun();
        Assert.That(request!.Recorded, Is.True, "Only a Quartz job run");
    }
}
