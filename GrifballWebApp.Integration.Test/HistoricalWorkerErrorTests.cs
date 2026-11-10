using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Historical;

namespace GrifballWebApp.Integration.Test;

/// <summary>A failed pass of a background worker is logged and the worker waits for the next one, instead of stopping.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalWorkerErrorTests
{
    private static IServiceScopeFactory FailingScopes(out Func<int> calls)
    {
        var scopes = Substitute.For<IServiceScopeFactory>();
        var count = 0;
        scopes.CreateScope().Returns(_ =>
        {
            Interlocked.Increment(ref count);
            throw new InvalidOperationException("boom");
        });
        calls = () => Volatile.Read(ref count);
        return scopes;
    }

    private static async Task RunUntilCalled(Microsoft.Extensions.Hosting.BackgroundService worker, Func<int> calls)
    {
        await worker.StartAsync(CancellationToken.None);
        for (var i = 0; i < 100 && calls() == 0; i++)
            await Task.Delay(50);
        // Still running after the failure: waiting for its next pass
        Assert.That(worker.ExecuteTask, Is.Not.Null);
        Assert.That(worker.ExecuteTask!.IsCompleted, Is.False, "A failed pass doesn't stop the worker");
        await worker.StopAsync(CancellationToken.None);
        Assert.That(calls(), Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task TheXuidLookupWorker_KeepsGoingAfterAFailedPass()
    {
        var scopes = FailingScopes(out var calls);
        var worker = new XuidLookupWorker(scopes, new XuidLookupSignal(), Options.Create(new XuidLookupOptions()), NullLogger<XuidLookupWorker>.Instance);

        await RunUntilCalled(worker, calls);
    }

    [Test]
    public async Task TheHistoricalImportWorker_KeepsGoingAfterAFailedPass()
    {
        var scopes = FailingScopes(out var calls);
        var worker = new HistoricalImportWorker(scopes, new HistoricalImportSignal(), Options.Create(new HistoricalImportWorkerOptions()), TimeProvider.System,
            NullLogger<HistoricalImportWorker>.Instance, new XuidLookupSignal());

        await RunUntilCalled(worker, calls);
    }
}
