using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Domain.Test;

/// <summary>A season's scheduling rules (<see cref="SeasonSchedulingRules"/>): the league's defaults until staff change them.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonSchedulingRulesAggregateTests
{
    [Test]
    public void Defaults_FourPlayersMakeASlot_OneMatchANight_TwoAWeek_NoCapOnConcurrentMatches()
    {
        var rules = SeasonSchedulingRules.Defaults(7);

        Assert.That((rules.SeasonID, rules.MinPlayersAvailable, rules.MaxMatchesPerTeamPerNight, rules.MaxMatchesPerTeamPerWeek, rules.MaxConcurrentMatches),
            Is.EqualTo((7, 4, 1, 2, (int?)null)));
    }

    [Test]
    public void Change_KeepsWhatsSensible_AndRefusesTheRest()
    {
        var rules = SeasonSchedulingRules.Defaults(7);
        rules.Change(minPlayersAvailable: 3, maxMatchesPerTeamPerNight: 2, maxMatchesPerTeamPerWeek: 3, maxConcurrentMatches: 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((rules.MinPlayersAvailable, rules.MaxMatchesPerTeamPerNight, rules.MaxMatchesPerTeamPerWeek, rules.MaxConcurrentMatches), Is.EqualTo((3, 2, 3, (int?)4)));
            Assert.That(() => rules.Change(0, 1, 2, null), Throws.InstanceOf<ArgumentOutOfRangeException>(), "Nobody available is no slot");
            Assert.That(() => rules.Change(4, 0, 2, null), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => rules.Change(4, 2, 1, null), Throws.InstanceOf<ArgumentOutOfRangeException>(), "A week holds a night");
            Assert.That(() => rules.Change(4, 1, 2, 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That((rules.MinPlayersAvailable, rules.MaxMatchesPerTeamPerNight), Is.EqualTo((3, 2)), "Refused: unchanged");
        }
    }
}
