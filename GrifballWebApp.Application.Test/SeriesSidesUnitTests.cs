using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Domain.Halo.Match;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.Queries;
using GrifballWebApp.Application.SeasonMatchPage;

namespace GrifballWebApp.Application.Test;

/// <summary>SeriesSidesTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeriesSidesUnitTests
{
    private static readonly string[] TwoTeams = ["a", "b"];
    private static readonly string[] OneTeam = ["a"];
    private static readonly string[] ThreeTeams = ["a", "b", "c"];

    [Test]
    public void Of_PutsTheTeamLeaningMoreHome_Home_WhicheverComesFirst()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SeriesSides.Of(TwoTeams, t => t == "a" ? 2 : -4), Is.EqualTo(("a", "b")));
            Assert.That(SeriesSides.Of(TwoTeams, t => t == "a" ? -1 : 3), Is.EqualTo(("b", "a")));
            // Both lean home: the one leaning less is still the other side
            Assert.That(SeriesSides.Of(TwoTeams, t => t == "a" ? 1 : 2), Is.EqualTo(("b", "a")));
        }
    }

    [Test]
    public void Of_CantDecide_ATieOrAGameNotOfTwoTeams()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SeriesSides.Of(TwoTeams, _ => 0), Is.Null, "Nobody rostered");
            Assert.That(SeriesSides.Of(TwoTeams, _ => -1), Is.Null, "Both lean away alike");
            Assert.That(SeriesSides.Of(OneTeam, _ => 4), Is.Null);
            Assert.That(SeriesSides.Of(ThreeTeams, t => t == "a" ? 4 : 0), Is.Null);
        }
    }

    [Test]
    public void Lean_IsHomePlayersLessAwayPlayers()
    {
        var rosters = new SeriesRosters(new HashSet<long> { 1, 2, 3 }, new HashSet<long> { 7, 8 });

        Assert.That(rosters.Lean([1, 2, 7, 99]), Is.EqualTo(1), "Two home, one away, a ringer on neither");
    }
}
