using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.QueryableExtensions;
using GrifballWebApp.Application.Sorting;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class OrderByAndPaginationSqlTests
{
    private GrifballContext _context;
    private static readonly string[] expected = new[] { "alpha", "Bravo", "Charlie", "Delta", "Echo" };
    private static readonly string[] expectedArray = new[] { "Echo", "Delta", "Charlie", "Bravo", "alpha" };
    private static readonly string[] expectedArray0 = new[] { "alpha", "Echo", "Bravo", "Charlie", "Delta" };
    private static readonly string[] expectedArray1 = new[] { "Charlie", "Bravo", "Delta", "alpha", "Echo" };
    private static readonly int[] expectedArray2 = new[] { 3, 5, 1 };
    private static readonly int[] expectedArray3 = new[] { 5, 1, 3 };
    private static readonly int[] expectedArray4 = new[] { 1, 5, 3 };
    private static readonly int[] expectedArray5 = new[] { 3, 1, 5 };

    [SetUp]
    public async Task Setup()
    {
        _context = await TestDatabase.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task SeedSeasons()
    {
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string[] names = ["Echo", "alpha", "Delta", "Bravo", "Charlie"];
        for (var i = 0; i < names.Length; i++)
        {
            _context.Seasons.Add(Season.Seed(seasonName: names[i]).WithDates(seasonStart: baseDate.AddDays(i), seasonEnd: baseDate.AddDays(i < 2 ? 100 : 50)));
        }
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Test]
    public async Task OrderByFilter_TranslatesAscendingAndDescending()
    {
        await SeedSeasons();

        var asc = await _context.Seasons.OrderBy(new PaginationFilter { SortColumn = "seasonname", SortDirection = SortDirection.Asc }).Select(x => x.SeasonName).ToListAsync();
        var desc = await _context.Seasons.OrderBy(new PaginationFilter { SortColumn = "SeasonName", SortDirection = SortDirection.Desc }).Select(x => x.SeasonName).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            // SQL Server default collation is case-insensitive
            Assert.That(asc, Is.EqualTo(expected));
            Assert.That(desc, Is.EqualTo(expectedArray));
        }
    }

    [Test]
    public async Task OrderByString_MultipleColumns_TranslatesThenBy()
    {
        await SeedSeasons();

        var result = await _context.Seasons.OrderBy("-SeasonEnd,SeasonName").Select(x => x.SeasonName).ToListAsync();

        Assert.That(result, Is.EqualTo(expectedArray0));
    }

    [Test]
    public async Task OrderByString_AfterExistingOrder_TranslatesThenByDescending()
    {
        await SeedSeasons();

        var result = await _context.Seasons.OrderBy(x => x.SeasonEnd).OrderBy("-SeasonStart").Select(x => x.SeasonName).ToListAsync();

        Assert.That(result, Is.EqualTo(expectedArray1));
    }

    [Test]
    public async Task OrderByString_NullableColumn_SortsNullsAsTheDatabaseDoes()
    {
        await SeedSeasons();
        var season = await _context.Seasons.FirstAsync();
        _context.SeasonMatches.AddRange(
            SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 1, scheduledTime: new DateTime(2024, 5, 2)),
            SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 3),
            SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 5, scheduledTime: new DateTime(2024, 5, 1)));
        await _context.SaveChangesAsync();

        var asc = await _context.SeasonMatches.OrderBy("ScheduledTime").Select(x => x.BestOf).ToListAsync();
        var desc = await _context.SeasonMatches.OrderBy("-ScheduledTime").Select(x => x.BestOf).ToListAsync();

        // SQL Server and SQLite sort NULL lowest: first ascending, last descending. Postgres sorts it highest.
        var nullsFirst = TestDatabase.Provider != DatabaseProvider.Postgres;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(asc, Is.EqualTo(nullsFirst ? expectedArray2 : expectedArray3));
            Assert.That(desc, Is.EqualTo(nullsFirst ? expectedArray4 : expectedArray5));
        }
    }

    [Test]
    public async Task OrderByString_OnProjection_TranslatesMemberAccess()
    {
        await SeedSeasons();

        // Same shape the HomeController/UserManagementService use: sort on a projected DTO
        var result = await _context.Seasons
            .Select(x => new PagedSeason { Id = x.SeasonID, Name = x.SeasonName })
            .OrderBy(new PaginationFilter { SortColumn = "name", SortDirection = SortDirection.Desc })
            .Select(x => x.Name)
            .ToListAsync();

        Assert.That(result, Is.EqualTo(expectedArray));
    }

    [Test]
    public async Task OrderByString_NavigationProperty_FailsTranslation()
    {
        await SeedSeasons();

        // A sort column naming a collection navigation is accepted by the parser but cannot be translated
        var query = _context.Seasons.OrderBy("Teams");

        Assert.That(async () => await query.ToListAsync(), Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task PaginationResult_ReturnsEmpty_When_NoRows()
    {
        var result = await _context.Seasons.PaginationResult(new PaginationFilter());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TotalCount, Is.Zero);
            Assert.That(result.Results, Is.Empty);
        }
    }

    [TestCase(1, 2, new[] { "alpha", "Bravo" })]
    [TestCase(2, 2, new[] { "Charlie", "Delta" })]
    [TestCase(3, 2, new[] { "Echo" })]
    [TestCase(4, 2, new string[0])]
    [TestCase(0, 3, new[] { "alpha", "Bravo", "Charlie" })] // page clamps to 1
    [TestCase(1, 1000, new[] { "alpha", "Bravo", "Charlie", "Delta", "Echo" })] // size clamps to 250
    public async Task PaginationResult_PagesSortedQuery(int page, int size, string[] expected)
    {
        await SeedSeasons();
        var filter = new PaginationFilter(page, size) { SortColumn = "SeasonName", SortDirection = SortDirection.Asc };
        var paged = await _context.Seasons.OrderBy(filter).Select(x => x.SeasonName).PaginationResult(filter);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(paged.TotalCount, Is.EqualTo(5));
            Assert.That(paged.Results, Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task PaginationResult_NegativePageSize_ClampsToOne()
    {
        await SeedSeasons();
        // See PaginationFilterTests: PageSize clamps to [1, 250], so a negative
        // value never reaches Take() and SQL Server no longer rejects it.
        var filter = new PaginationFilter(1, -1);
        var paged = await _context.Seasons.OrderBy(x => x.SeasonID).PaginationResult(filter);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(filter.PageSize, Is.EqualTo(1));
            Assert.That(paged.TotalCount, Is.EqualTo(5));
            Assert.That(paged.Results, Has.Length.EqualTo(1));
        }
    }

    public class PagedSeason
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
