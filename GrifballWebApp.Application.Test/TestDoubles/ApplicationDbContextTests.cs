namespace GrifballWebApp.Application.Test.TestDoubles;

/// <summary>A fresh <see cref="FakeApplicationDbContext"/> (a throwaway SQLite in-memory database) for each test
/// case: fixtures seed it directly through the context, services under test get it as their IApplicationDbContext.</summary>
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal abstract class ApplicationDbContextTests
{
    protected FakeApplicationDbContext Context { get; private set; } = null!;

    [SetUp]
    public void SetUpContext()
    {
        Context = new FakeApplicationDbContext();
    }

    [TearDown]
    public void TearDownContext()
    {
        Context.Dispose();
    }
}
