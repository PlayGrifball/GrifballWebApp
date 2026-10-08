using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.QueryableExtensions;
using GrifballWebApp.Server.Sorting;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace GrifballWebApp.Test;

[TestFixture]
public class PaginationFilterTests
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
