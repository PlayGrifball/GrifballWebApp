using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Sorting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace GrifballWebApp.Test;

[TestFixture]
public class OrderByExtensionsTests
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
