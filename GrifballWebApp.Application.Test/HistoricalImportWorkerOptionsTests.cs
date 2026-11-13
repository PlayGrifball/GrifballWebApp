using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Application.Test;

/// <summary>The worker's defaults (HistoricalImport:Worker) and the schedule the page shows.</summary>
[TestFixture]
internal sealed class HistoricalImportWorkerOptionsTests
{
    [Test]
    public void WorkerOptions_Defaults()
    {
        var options = new HistoricalImportWorkerOptions();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.PollInterval, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(options.StaleAfter, Is.EqualTo(TimeSpan.FromMinutes(30)));
            Assert.That(options.KeepFinishedFor, Is.EqualTo(TimeSpan.FromDays(30)));
            Assert.That(options.ReportLinesKept, Is.EqualTo(500));
        }
    }
}
