using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Test.TestDoubles;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>TeamHistoryService.GetAsync against the fake IApplicationDbContext. Franchises.IsPlaceholderName's own
/// cases stay in TeamHistoryServiceUnitTests, which has no database.
///
/// Only the not-found path is covered here: the found path's team projection (AsSplitQuery, with Titles and
/// Players each a nested collection) needs the SQL APPLY operation on SQLite ("Translating this query requires
/// the SQL APPLY operation, which is not supported on SQLite"), which only Infrastructure's DbContextOptionsBuilder
/// provides (see UserManagementServiceUnitTests' note on the same gap). GrifballWebApp.Integration.Test covers
/// GetAsync's found path against the real providers.</summary>
[TestFixture]
internal sealed class TeamHistoryServiceGetAsyncUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private TeamHistoryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new TeamHistoryService(Context, _time);
    }

    [Test]
    public async Task GetAsync_NoSuchTeam_ReturnsNull()
    {
        var history = await _service.GetAsync(999);

        Assert.That(history, Is.Null);
    }
}
