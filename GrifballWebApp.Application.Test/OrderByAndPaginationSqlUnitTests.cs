using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.QueryableExtensions;

namespace GrifballWebApp.Application.Test;

/// <summary>OrderByAndPaginationSqlTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OrderByAndPaginationSqlUnitTests
{
    private static readonly int[] Numbers = [1, 2, 3];

    [Test]
    public async Task PaginationResult_InMemoryQueryable_IsNotSupported()
    {
        // PaginationResult relies on EF async operators; a plain LINQ-to-objects queryable is rejected.
        var query = Numbers.AsQueryable();
        Assert.That(async () => await query.PaginationResult(new PaginationFilter()), Throws.InstanceOf<InvalidOperationException>());
        await Task.CompletedTask;
    }
}
