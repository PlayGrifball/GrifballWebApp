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

    private const string Cut = "IX_ScheduleRecommendationMatchesHistory_ScheduleRecommendation~";

    // EF's own names whenever they stay apart, cut ones included: the history indexes Postgres already has keep theirs.
    [Test]
    public void HistoryIndexRenames_None_WhileEFsNamesStayApart()
    {
        var renames = PostgresHistory.HistoryIndexRenames(
            [("IX_TeamAvailabilityHistory_TeamAvailabilityID_PeriodEnd", "TeamAvailabilityID,PeriodEnd"),
             ("IX_TeamAvailabilityHistory_TeamID_AvailabilityOptionID_PeriodE~", "TeamID,AvailabilityOptionID,PeriodEnd")], 63);

        Assert.That(renames, Is.All.Null);
    }

    // Postgres cuts identifiers at 63 bytes, and EF's cut gave ScheduleRecommendationMatchesHistory's two indexes one
    // name, so the model wouldn't build. The first by its columns keeps EF's name; the others keep its start and end in
    // a hash of their columns, the same every time.
    [Test]
    public void HistoryIndexRenames_ThatClash_AllButTheFirst_FitAndStayApart()
    {
        (string, string)[] indexes = [(Cut, "ScheduleRecommendationMatchID,PeriodEnd"), (Cut, "ScheduleRecommendationID,PeriodEnd"),
            ("IX_ScheduleRecommendationMatchesHistory_SeasonMatchID_PeriodEnd", "SeasonMatchID,PeriodEnd")];

        var renames = PostgresHistory.HistoryIndexRenames(indexes, 63);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(renames[1], Is.Null, "ScheduleRecommendationID sorts first: EF's");
            Assert.That(renames[2], Is.Null, "No clash: EF's");
            Assert.That(renames[0], Has.Length.LessThanOrEqualTo(63).And.StartWith(Cut[..54] + "~").And.Not.EqualTo(Cut));
            Assert.That(PostgresHistory.HistoryIndexRenames(indexes, 63), Is.EqualTo(renames), "Or each migrations add would rename it");
        }
    }

    // The limit is the model's (the provider's), not a copy of it.
    [Test]
    public void HistoryIndexRenames_FitTheLimitGiven()
    {
        var renames = PostgresHistory.HistoryIndexRenames([("IX_LongTable_A~", "A,PeriodEnd"), ("IX_LongTable_A~", "AB,PeriodEnd")], 15);

        Assert.That(renames[1], Has.Length.EqualTo(15).And.StartWith("IX_Lon~"));
    }
}
