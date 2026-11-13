using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Historical;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>HistoricalImportWorker.Cap: a report's lists kept to a line count, the last saying how many more there were.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HistoricalImportWorkerCapUnitTests
{
    [Test]
    public void Cap_ALongList_IsTruncated_WithHowManyMoreThereWere()
    {
        var report = new HistoricalImportReport { Warnings = ["a", "b", "c", "d", "e"] };

        HistoricalImportWorker.Cap(report, 2);

        Assert.That(report.Warnings, Is.EqualTo(new List<string> { "a", "b", "... and 3 more" }));
    }

    [Test]
    public void Cap_AShortList_IsLeftAlone()
    {
        var report = new HistoricalImportReport { Errors = ["only"] };

        HistoricalImportWorker.Cap(report, 10);

        Assert.That(report.Errors, Is.EqualTo(new List<string> { "only" }));
    }

    [Test]
    public void Cap_CapsEveryListTheReportHas()
    {
        var report = new HistoricalImportReport
        {
            Skipped = ["s1", "s2", "s3"],
            Warnings = ["w1", "w2", "w3"],
            MissingMatches = ["m1", "m2", "m3"],
            Errors = ["e1", "e2", "e3"],
        };

        HistoricalImportWorker.Cap(report, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Skipped, Is.EqualTo(new List<string> { "s1", "... and 2 more" }));
            Assert.That(report.Warnings, Is.EqualTo(new List<string> { "w1", "... and 2 more" }));
            Assert.That(report.MissingMatches, Is.EqualTo(new List<string> { "m1", "... and 2 more" }));
            Assert.That(report.Errors, Is.EqualTo(new List<string> { "e1", "... and 2 more" }));
        }
    }
}
