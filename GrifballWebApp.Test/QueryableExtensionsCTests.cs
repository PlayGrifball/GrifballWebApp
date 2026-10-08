using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Sorting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace GrifballWebApp.Test;

[TestFixture]
public class OrderByExtensionsInMemoryCTests
{
    public class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public int? Score { get; set; }
        public Address Home { get; set; } = new();
    }

    public class Address
    {
        public string City { get; set; } = "";
    }

    private static List<Person> People() =>
    [
        new() { Name = "Carol", Age = 30, Score = 5, Home = new() { City = "Zed" } },
        new() { Name = "Alice", Age = 25, Score = null, Home = new() { City = "Ann" } },
        new() { Name = "Bob", Age = 30, Score = 1, Home = new() { City = "Moe" } },
        new() { Name = "Dave", Age = 20, Score = 3, Home = new() { City = "Bay" } },
    ];

    private static IQueryable<Person> Query() => People().AsQueryable();

    [Test]
    public void OrderByFilter_ReturnsSameQuery_When_DirectionIsNull()
    {
        var q = Query();
        var result = q.OrderBy(new PaginationFilter { SortColumn = "Name", SortDirection = null });
        Assert.That(result, Is.SameAs(q));
    }

    [Test]
    public void OrderByFilter_ReturnsSameQuery_When_ColumnIsNull()
    {
        var q = Query();
        var result = q.OrderBy(new PaginationFilter { SortColumn = null, SortDirection = SortDirection.Asc });
        Assert.That(result, Is.SameAs(q));
    }

    [Test]
    public void OrderByFilter_Asc_SortsAscending()
    {
        var result = Query().OrderBy(new PaginationFilter { SortColumn = "Age", SortDirection = SortDirection.Asc }).Select(x => x.Age).ToList();
        Assert.That(result, Is.EqualTo(new[] { 20, 25, 30, 30 }));
    }

    [Test]
    public void OrderByFilter_Desc_SortsDescending()
    {
        var result = Query().OrderBy(new PaginationFilter { SortColumn = "Age", SortDirection = SortDirection.Desc }).Select(x => x.Age).ToList();
        Assert.That(result, Is.EqualTo(new[] { 30, 30, 25, 20 }));
    }

    [Test]
    public void OrderByFilter_None_SortsDescending()
    {
        // BUG: SortDirection.None is treated like Desc (anything that is not Asc prefixes "-").
        // Expected "None" to mean no sorting (or the default ascending order); actual is descending.
        var result = Query().OrderBy(new PaginationFilter { SortColumn = "Age", SortDirection = SortDirection.None }).Select(x => x.Age).ToList();
        Assert.That(result, Is.EqualTo(new[] { 30, 30, 25, 20 }));
    }

    [Test]
    public void OrderByFilter_UnknownColumn_LeavesOrderUnchanged()
    {
        var result = Query().OrderBy(new PaginationFilter { SortColumn = "DoesNotExist", SortDirection = SortDirection.Desc }).Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(People().Select(x => x.Name)));
    }

    [Test]
    public void OrderByString_IsCaseInsensitiveOnPropertyName()
    {
        var result = Query().OrderBy("nAmE").Select(x => x.Name).ToList();
        // string comparison itself is the default (culture) comparer
        Assert.That(result, Is.EqualTo(new[] { "Alice", "Bob", "Carol", "Dave" }));
    }

    [Test]
    public void OrderByString_LeadingDash_SortsDescending()
    {
        var result = Query().OrderBy("-name").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Dave", "Carol", "Bob", "Alice" }));
    }

    [Test]
    public void OrderByString_MultipleColumns_UsesThenBy()
    {
        var result = Query().OrderBy("-Age,Name").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Bob", "Carol", "Alice", "Dave" }));
    }

    [Test]
    public void OrderByString_SkipsGarbageAndEmptyEntries()
    {
        var result = Query().OrderBy(",,Nope,-Age,,Name,").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Bob", "Carol", "Alice", "Dave" }));
    }

    [Test]
    public void OrderByString_TrimsWhitespace()
    {
        var result = Query().OrderBy("  Age ").Select(x => x.Age).ToList();
        Assert.That(result, Is.EqualTo(new[] { 20, 25, 30, 30 }));
    }

    [Test]
    public void OrderByString_WhitespaceBeforeDash_IsIgnored()
    {
        // BUG: Parse() checks StartsWith("-") on the untrimmed element, so " -Age" (e.g. "Name, -Age") is looked up
        // as property "-Age", which does not exist, and the sort is silently dropped. Expected: descending by Age.
        var sorts = new SortCollection<Person>("Name, -Age");
        Assert.That(sorts.Sorts.Select(x => x.PropertyName), Is.EqualTo(new[] { "Name" }));
    }

    [Test]
    public void OrderByString_NestedPropertyPath_IsNotSupported()
    {
        var q = Query();
        var sorts = new SortCollection<Person>("Home.City");
        Assert.That(sorts.Sorts, Is.Empty);
        Assert.That(q.OrderBy("Home.City").Select(x => x.Name), Is.EqualTo(People().Select(x => x.Name)));
    }

    [Test]
    public void OrderByString_NullableColumn_PutsNullsFirstAscending()
    {
        var asc = Query().OrderBy("Score").Select(x => x.Score).ToList();
        var desc = Query().OrderBy("-Score").Select(x => x.Score).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(asc, Is.EqualTo(new int?[] { null, 1, 3, 5 }));
            Assert.That(desc, Is.EqualTo(new int?[] { 5, 3, 1, null }));
        });
    }

    [Test]
    public void OrderByString_OnAlreadyOrderedQuery_AppendsThenBy()
    {
        // Existing OrderBy(Age) must remain primary; Name becomes the tie breaker
        var result = Query().OrderByDescending(x => x.Age).OrderBy("Name").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Bob", "Carol", "Alice", "Dave" }));
    }

    [Test]
    public void OrderByString_OnAlreadyOrderedQuery_AppendsThenByDescending()
    {
        var result = Query().OrderByDescending(x => x.Age).OrderBy("-Name").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Carol", "Bob", "Alice", "Dave" }));
    }

    [Test]
    public void OrderByString_WhereBeforeSort_IsNotMistakenForOrdering()
    {
        var result = Query().Where(x => x.Age > 20).OrderBy("-Name").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Carol", "Bob", "Alice" }));
    }

    [Test]
    public void OrderByParams_AppliesEachSort()
    {
        var result = Query().OrderBy("Age", "-Name").Select(x => x.Name).ToList();
        Assert.That(result, Is.EqualTo(new[] { "Dave", "Alice", "Carol", "Bob" }));
    }

    [Test]
    public void OrderByOut_ExposesParsedSorts()
    {
        var result = Query().OrderBy("-age,NAME,bogus", out var sortCollection).Select(x => x.Name).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new[] { "Bob", "Carol", "Alice", "Dave" }));
            Assert.That(sortCollection.Sorts.Select(x => x.PropertyName), Is.EqualTo(new[] { "Age", "Name" }));
            Assert.That(sortCollection.Sorts.Select(x => x.Direction), Is.EqualTo(new[] { ListSortDirection.Descending, ListSortDirection.Ascending }));
            Assert.That(sortCollection.ToString(), Is.EqualTo("-Age,Name"));
        });
    }

    [Test]
    public void SortCollection_EmptyConstructor_DoesNothing()
    {
        var q = Query();
        var sorts = new SortCollection<Person>();
        Assert.Multiple(() =>
        {
            Assert.That(sorts.Apply(q), Is.SameAs(q));
            Assert.That(sorts.ToString(), Is.Empty);
            Assert.That(sorts.Sorts, Is.Empty);
        });
    }

    [Test]
    public void SortCollection_NullEnumerable_DoesNothing()
    {
        var sorts = new SortCollection<Person>((IEnumerable<string>)null!);
        Assert.That(sorts.Sorts, Is.Empty);
    }

    [Test]
    public void AddOrUpdate_AddsNewProperty()
    {
        var sorts = new SortCollection<Person>("Name");
        Assert.That(sorts.AddOrUpdate("-age"), Is.EqualTo("Name,-Age"));
        // Original collection is not mutated
        Assert.That(sorts.ToString(), Is.EqualTo("Name"));
    }

    [Test]
    public void AddOrUpdate_FlipsExistingDirection()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SortCollection<Person>("Name,Age").AddOrUpdate("age"), Is.EqualTo("Name,-Age"));
            Assert.That(new SortCollection<Person>("-Name").AddOrUpdate("Name"), Is.EqualTo("Name"));
        });
    }

    [Test]
    public void AddOrUpdate_IgnoresUnknownProperty()
    {
        Assert.That(new SortCollection<Person>("-Name").AddOrUpdate("bogus"), Is.EqualTo("-Name"));
    }

    [Test]
    public void Remove_RemovesExistingProperty_RegardlessOfDirection()
    {
        var sorts = new SortCollection<Person>("Name,-Age");
        Assert.Multiple(() =>
        {
            Assert.That(sorts.Remove("age"), Is.EqualTo("Name"));
            Assert.That(sorts.Remove("-Name"), Is.EqualTo("-Age"));
            Assert.That(sorts.Remove("Score"), Is.EqualTo("Name,-Age"));
            Assert.That(sorts.Remove("bogus"), Is.EqualTo("Name,-Age"));
            Assert.That(sorts.ToString(), Is.EqualTo("Name,-Age"));
        });
    }

    [Test]
    public void OrderByString_NonComparableProperty_ThrowsWhenEnumerated()
    {
        // Sorting by a property whose type is not IComparable is accepted by the parser but fails at execution.
        var q = Query().OrderBy("Home");
        Assert.That(() => q.ToList(), Throws.Exception);
    }
}

[TestFixture]
public class PaginationFilterCTests
{
    [Test]
    public void DefaultConstructor_UsesFirstPageOfTen()
    {
        var f = new PaginationFilter();
        Assert.Multiple(() =>
        {
            Assert.That(f.PageNumber, Is.EqualTo(1));
            Assert.That(f.PageSize, Is.EqualTo(10));
            Assert.That(f.SortColumn, Is.Null);
            Assert.That(f.SortDirection, Is.Null);
        });
    }

    [TestCase(1, 10, 1, 10)]
    [TestCase(0, 10, 1, 10)]
    [TestCase(-5, 10, 1, 10)]
    [TestCase(7, 250, 7, 250)]
    [TestCase(2, 251, 2, 250)]
    [TestCase(2, 10000, 2, 250)]
    // BUG: there is no lower bound on page size; 0 and negative values are passed through to Take().
    [TestCase(1, 0, 1, 0)]
    [TestCase(1, -3, 1, -3)]
    public void Constructor_ClampsValues(int pageNumber, int pageSize, int expectedPage, int expectedSize)
    {
        var f = new PaginationFilter(pageNumber, pageSize);
        Assert.Multiple(() =>
        {
            Assert.That(f.PageNumber, Is.EqualTo(expectedPage));
            Assert.That(f.PageSize, Is.EqualTo(expectedSize));
        });
    }
}

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class QueryableExtensionsSqlCTests
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
            .Select(x => new PagedSeasonC { Id = x.SeasonID, Name = x.SeasonName })
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
        // See PaginationFilterCTests: the constructor does not clamp page size from below, so SQL Server rejects it.
        var filter = new PaginationFilter(1, -1);
        Assert.That(async () => await _context.Seasons.OrderBy(x => x.SeasonID).PaginationResult(filter), Throws.Exception);
    }

    public class PagedSeasonC
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
