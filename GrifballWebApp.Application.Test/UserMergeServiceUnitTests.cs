using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Application.Test;

/// <summary>UserMergeServiceTests' unit tests: a merge refused before it reads anything. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class UserMergeServiceUnitTests
{
    private readonly UserMergeService _service = new(null!);

    [Test]
    public async Task Merge_IntoItself_Throws()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.Merge(5, 5));
        Assert.That(ex!.Message, Does.Contain("itself"));
    }
}
