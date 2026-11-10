using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Search;

namespace GrifballWebApp.Application.Test;

/// <summary>QuickSearchTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QuickSearchUnitTests
{
    [TestCase("2019", true, 2019)]
    [TestCase("1999", false, 0)]
    [TestCase("3000", false, 0)]
    [TestCase("201", false, 0)]
    [TestCase("20a9", false, 0)]
    [TestCase("+201", false, 0)]
    public void TryParseYear_TakesFourDigitYears(string q, bool expected, int expectedYear)
    {
        Assert.That(QuickSearchService.TryParseYear(q, out var year), Is.EqualTo(expected));
        if (expected)
            Assert.That(year, Is.EqualTo(expectedYear));
    }
}
