using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Application.Test;

/// <summary>ProfileCommendationsTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class ProfileCommendationsUnitTests
{
    [TestCase(0, 0)]
    [TestCase(9, 0)]
    [TestCase(10, 1)]
    [TestCase(999, 5)]
    [TestCase(1000, 6)]
    [TestCase(50000, 6)]
    public void TierOf_CountsTheThresholdsReached(int count, int tier)
    {
        Assert.That(ProfileCommendationsService.TierOf(count), Is.EqualTo(tier));
    }
}
