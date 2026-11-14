using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>PostgresHistoryModelTests's unit tests: no database, host, file or network. Its other tests are
/// GrifballWebApp.Migrations.Postgres.Test's (the ones that need GrifballWebApp.Migrations.Postgres loaded).</summary>
[TestFixture]
public class PostgresHistoryModelUnitTests
{
    [Test]
    public void HistoryFunctionsVersion_HasUnixLineEndings()
    {
        var version = new HistoryFunctionsVersion(9, "a\r\nb", "c\r\nd");

        Assert.That((version.Create, version.Drop), Is.EqualTo(("a\nb", "c\nd")));
    }

    private static readonly string[] TeamAvailabilityHistoryIndexes =
        ["IX_TeamAvailabilityHistory_TeamAvailabilityID_PeriodEnd", "IX_TeamAvailabilityHistory_TeamID_AvailabilityOptionID_PeriodE~"];

    // EF's own names, cut as EF cuts them, whenever they stay apart: the history indexes Postgres already has keep
    // their names (TeamAvailabilityHistory's is one EF cuts).
    [Test]
    public void HistoryIndexNames_AreEFs_WhileTheyStayApart()
    {
        var names = PostgresHistory.HistoryIndexNames("TeamAvailabilityHistory", [["TeamAvailabilityID", "PeriodEnd"], ["TeamID", "AvailabilityOptionID", "PeriodEnd"]]);

        Assert.That(names, Is.EqualTo(TeamAvailabilityHistoryIndexes));
    }

    // Postgres cuts identifiers at 63 bytes, and EF's cut gave ScheduleRecommendationMatchesHistory's two indexes one
    // name, so the model wouldn't build: the names that clash keep their start and end in a hash of the whole.
    [Test]
    public void HistoryIndexNames_ThatClashWhenCut_FitAndStayApart()
    {
        string[][] indexes = [["ScheduleRecommendationMatchID", "PeriodEnd"], ["ScheduleRecommendationID", "PeriodEnd"], ["SeasonMatchID", "PeriodEnd"]];
        var names = PostgresHistory.HistoryIndexNames("ScheduleRecommendationMatchesHistory", indexes);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(names, Has.All.Length.LessThanOrEqualTo(PostgresHistory.MaxIdentifierLength));
            Assert.That(names.Take(2), Has.All.StartWith("IX_ScheduleRecommendationMatchesHistory_ScheduleRecomm").And.Not.EndWith("~"));
            Assert.That(names, Is.Unique);
            Assert.That(names[2], Is.EqualTo("IX_ScheduleRecommendationMatchesHistory_SeasonMatchID_PeriodEnd"), "No clash: EF's");
            Assert.That(PostgresHistory.HistoryIndexNames("ScheduleRecommendationMatchesHistory", indexes), Is.EqualTo(names),
                "The same names every time, or each migrations add would rename them");
        }
    }
}
