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

    [Test]
    public void ScheduleDto_KeepsWhatItIsGiven()
    {
        var lastRun = new DateTime(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc);
        var schedule = new HistoricalImportScheduleDto
        {
            Paused = true,
            Interval = TimeSpan.FromHours(24),
            LastRunAt = lastRun,
            NextRunAt = lastRun.AddDays(1),
            LastJobID = 42,
            LastRunNote = "a run in progress",
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Paused, Is.True);
            Assert.That(schedule.Interval, Is.EqualTo(TimeSpan.FromHours(24)));
            Assert.That(schedule.LastRunAt, Is.EqualTo(lastRun));
            Assert.That(schedule.NextRunAt, Is.EqualTo(new DateTime(2026, 1, 2, 6, 0, 0, DateTimeKind.Utc)));
            Assert.That(schedule.LastJobID, Is.EqualTo(42));
            Assert.That(schedule.LastRunNote, Is.EqualTo("a run in progress"));
        }
    }

    [Test]
    public void Report_KeepsARefreshsSourcesAndItsJob()
    {
        var report = new HistoricalImportReport { SourcesUnchanged = 3, ImportJobID = 7 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.SourcesUnchanged, Is.EqualTo(3));
            Assert.That(report.ImportJobID, Is.EqualTo(7));
            Assert.That(new HistoricalImportReport().ImportJobID, Is.Null, "No job queued unless a refresh says so");
        }
    }

    [Test]
    public void ScheduleDto_Defaults_AreUnscheduled()
    {
        var schedule = new HistoricalImportScheduleDto();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(schedule.Paused, Is.False);
            Assert.That(schedule.Interval, Is.EqualTo(TimeSpan.Zero));
            Assert.That(schedule.LastRunAt, Is.Null);
            Assert.That(schedule.NextRunAt, Is.Null);
            Assert.That(schedule.LastJobID, Is.Null);
            Assert.That(schedule.LastRunNote, Is.Null);
        }
    }
}
