using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Sorting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class OrderByAndPaginationSqlTests
{
    private GrifballContext _context;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task SeedSeasons()
    {
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        string[] names = ["Echo", "alpha", "Delta", "Bravo", "Charlie"];
        for (var i = 0; i < names.Length; i++)
        {
            _context.Seasons.Add(new Season
            {
                SeasonName = names[i],
                SeasonStart = baseDate.AddDays(i),
                // two seasons share the same end date to exercise ThenBy
                SeasonEnd = baseDate.AddDays(i < 2 ? 100 : 50),
            });
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

        Assert.Multiple(() =>
        {
            // SQL Server default collation is case-insensitive
            Assert.That(asc, Is.EqualTo(new[] { "alpha", "Bravo", "Charlie", "Delta", "Echo" }));
            Assert.That(desc, Is.EqualTo(new[] { "Echo", "Delta", "Charlie", "Bravo", "alpha" }));
        });
    }

    [Test]
    public async Task OrderByString_MultipleColumns_TranslatesThenBy()
    {
        await SeedSeasons();

        var result = await _context.Seasons.OrderBy("-SeasonEnd,SeasonName").Select(x => x.SeasonName).ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { "alpha", "Echo", "Bravo", "Charlie", "Delta" }));
    }

    [Test]
    public async Task OrderByString_AfterExistingOrder_TranslatesThenByDescending()
    {
        await SeedSeasons();

        var result = await _context.Seasons.OrderBy(x => x.SeasonEnd).OrderBy("-SeasonStart").Select(x => x.SeasonName).ToListAsync();

        Assert.That(result, Is.EqualTo(new[] { "Charlie", "Bravo", "Delta", "alpha", "Echo" }));
    }

    [Test]
    public async Task OrderByString_NullableColumn_SqlPutsNullsFirstAscending()
    {
        await SeedSeasons();
        var season = await _context.Seasons.FirstAsync();
        _context.SeasonMatches.AddRange(
            new SeasonMatch { SeasonID = season.SeasonID, BestOf = 1, ScheduledTime = new DateTime(2024, 5, 2) },
            new SeasonMatch { SeasonID = season.SeasonID, BestOf = 3, ScheduledTime = null },
            new SeasonMatch { SeasonID = season.SeasonID, BestOf = 5, ScheduledTime = new DateTime(2024, 5, 1) });
        await _context.SaveChangesAsync();

        var asc = await _context.SeasonMatches.OrderBy("ScheduledTime").Select(x => x.BestOf).ToListAsync();
        var desc = await _context.SeasonMatches.OrderBy("-ScheduledTime").Select(x => x.BestOf).ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(asc, Is.EqualTo(new[] { 3, 5, 1 }));
            Assert.That(desc, Is.EqualTo(new[] { 1, 5, 3 }));
        });
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

        Assert.That(result, Is.EqualTo(new[] { "Echo", "Delta", "Charlie", "Bravo", "alpha" }));
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

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalCount, Is.EqualTo(0));
            Assert.That(result.Results, Is.Empty);
        });
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

        Assert.Multiple(() =>
        {
            Assert.That(paged.TotalCount, Is.EqualTo(5));
            Assert.That(paged.Results, Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task PaginationResult_InMemoryQueryable_IsNotSupported()
    {
        // PaginationResult relies on EF async operators; a plain LINQ-to-objects queryable is rejected.
        var query = new[] { 1, 2, 3 }.AsQueryable();
        Assert.That(async () => await query.PaginationResult(new PaginationFilter()), Throws.InstanceOf<InvalidOperationException>());
        await Task.CompletedTask;
    }

    [Test]
    public async Task PaginationResult_NegativePageSize_Throws()
    {
        await SeedSeasons();
        // See PaginationFilterTests: the constructor does not clamp page size from below, so SQL Server rejects it.
        var filter = new PaginationFilter(1, -1);
        Assert.That(async () => await _context.Seasons.OrderBy(x => x.SeasonID).PaginationResult(filter), Throws.Exception);
    }

    public class PagedSeason
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
