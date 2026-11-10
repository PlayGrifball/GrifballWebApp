using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Application.Test;

/// <summary>A VOD title whose one team a season has, named second rather than first: that team is the suggestion.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class VodMatcherSidesTests
{
    private static readonly int[] BravoOnly = [12];
    private static readonly DateTime Start = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly VodMatcher Matcher = new(
    [
        new VodCatalogSeason(1, "Spring League 2024", Start, Start.AddMonths(3), VodTitleParser.ParseSeasonName("Spring League 2024", Start),
            [new VodCatalogTeam(11, "Alpha", VodTitleParser.TeamKey("Alpha")), new VodCatalogTeam(12, "Bravo", VodTitleParser.TeamKey("Bravo"))], []),
    ]);

    [Test]
    public void Match_SuggestsTheOneTeamFound_WhenItIsTheSecondNamed()
    {
        var match = Matcher.Match("Zulu vs Bravo", Start.AddDays(20));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.Status, match.SeasonID), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)1)));
            Assert.That(match.TeamIDs, Is.EqualTo(BravoOnly));
            Assert.That(match.Note, Does.StartWith("Only Bravo found in Spring League 2024"));
        }
    }
}
