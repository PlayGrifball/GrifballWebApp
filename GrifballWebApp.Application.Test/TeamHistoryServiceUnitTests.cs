using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.TeamPage;

namespace GrifballWebApp.Application.Test;

/// <summary>TeamHistoryServiceTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamHistoryServiceUnitTests
{
    [TestCase("Team 1", ExpectedResult = true)]
    [TestCase("team 12", ExpectedResult = true)]
    [TestCase("Team #3", ExpectedResult = true)]
    [TestCase("Team3", ExpectedResult = true)]
    [TestCase("  TEAM   A ", ExpectedResult = true)]
    [TestCase("Subs", ExpectedResult = true)]
    [TestCase("Free Agents", ExpectedResult = true)]
    [TestCase("", ExpectedResult = true)]
    [TestCase(null, ExpectedResult = true)]
    [TestCase("Trophy Hunters", ExpectedResult = false)]
    [TestCase("Team Rocket", ExpectedResult = false)]
    [TestCase("Team 1 Elite", ExpectedResult = false)]
    [TestCase("A Team", ExpectedResult = false)]
    [TestCase("TeamZ", ExpectedResult = false)]
    public bool IsPlaceholderName_Should_MatchOnlyGenericNames(string? name) => Franchises.IsPlaceholderName(name);
}
