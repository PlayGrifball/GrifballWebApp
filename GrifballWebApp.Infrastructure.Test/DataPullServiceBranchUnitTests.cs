using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Surprenant.Grunt.Core;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>DataPullServiceBranchTests' unit tests: a pull that returns or throws before reading anything. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DataPullServiceBranchUnitTests
{
    private readonly IHaloInfiniteClientFactory _factory = Substitute.For<IHaloInfiniteClientFactory>();
    private readonly DataPullService _service;

    public DataPullServiceBranchUnitTests()
    {
        // Neither path reaches the database or the Xbox users
        _service = new DataPullService(NullLogger<DataPullService>.Instance, _factory, null!, null!);
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_EmptyList_DoesNothing()
    {
        await _service.DownloadRecentMatchesForPlayers([]);

        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }

    [Test]
    public async Task DownloadRecentMatchesForPlayers_CancelledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.DownloadRecentMatchesForPlayers([1], ct: cts.Token));
        Assert.That(_factory.ReceivedCalls(), Is.Empty);
    }
}
