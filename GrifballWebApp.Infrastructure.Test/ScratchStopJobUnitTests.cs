using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Quartz;
using GrifballWebApp.Infrastructure.DatabaseBackup;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>ScratchStopJobTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ScratchStopJobUnitTests
{
    private static readonly string[] ExpectedCalls = ["stop", "stop"];
    private static readonly string[] ExpectedSummaries = ["Stopped the scratch server", "Stopped the scratch server (queued by grif-worker-1)"];

    internal sealed class Scaler : IScratchScaler
    {
        public readonly ConcurrentQueue<string> Calls = new();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Calls.Enqueue("start");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Calls.Enqueue("stop");
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task The_stop_reports_what_it_did()
    {
        var scaler = new Scaler();
        var context = Substitute.For<IJobExecutionContext>();

        await new ScratchStopJob(scaler).Execute(context, new ScratchStopRequest());
        await new ScratchStopJob(scaler).Execute(context, new ScratchStopRequest("grif-worker-1"));

        Assert.That(scaler.Calls, Is.EqualTo(ExpectedCalls));
        var summaries = context.ReceivedCalls().Where(c => c.GetMethodInfo().Name == "set_Result")
            .Select(c => ((JobRunReport)c.GetArguments()[0]!).Summary).ToList();
        Assert.That(summaries, Is.EqualTo(ExpectedSummaries));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("grif-backup-scratch", true)]
    public void The_stop_is_a_job_only_with_an_on_demand_scratch_server(string? deployment, bool registered)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ScratchStopJobRegistration.DeploymentKey] = deployment }).Build();
        var jobs = new RecurringJobs().AddScratchStop(configuration);
        Assert.That(jobs.FindOnDemand(OnDemandJobs.JobKeyOf(typeof(ScratchStopJob))) is not null, Is.EqualTo(registered));
    }
}
