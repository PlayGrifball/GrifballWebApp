using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GrunUser = Surprenant.Grunt.Models.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The two image jobs and their run reports, over a real XboxImageSource on a temp-file SQLite database with Halo substituted.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxImageJobsTests : SqliteContextFixture
{
    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();
    private readonly IXboxImageDownloader _downloader = Substitute.For<IXboxImageDownloader>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    private XboxImageSource Source()
        => new(Context, _halo, _downloader, Options.Create(new XboxImagesOptions { ProfileCallsPerMinute = 3 }), _time)
        {
            Delay = (_, _) => Task.CompletedTask,
        };

    private static async Task<JobRunReport> RunAsync(IJob job)
    {
        var context = Substitute.For<IJobExecutionContext>();
        await job.Execute(context);
        return (JobRunReport)context.Result!;
    }

    private async Task AddAccountAsync(long xuid)
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(xuid, $"Player{xuid}"));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task GamerPictureJob_WithNothingDue_IsSkipped()
    {
        var report = await RunAsync(new XboxGamerPictureJob(Source(), new FakeLogger<XboxGamerPictureJob>()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Skipped));
            Assert.That(report.Summary, Is.EqualTo("Every Xbox account has its gamer picture checked"));
        }
    }

    [Test]
    public async Task GamerPictureJob_StoringAPicture_Succeeds_AndSaysHowMany()
    {
        await AddAccountAsync(1);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GrunUser>, HaloApiErrorContainer>(
            [new GrunUser { xuid = "1", gamertag = "Player1", gamerpic = new Gamerpic { large = "https://x/l.png" } }], null!));
        _downloader.DownloadAsync("https://x/l.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage([0x89, 0x50, 0x4E, 0x47], "image/png"));

        var report = await RunAsync(new XboxGamerPictureJob(Source(), new FakeLogger<XboxGamerPictureJob>()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Succeeded));
            Assert.That(report.Summary, Is.EqualTo("1 of 1 accounts checked: 1 stored, 0 unchanged, 0 without one, 0 failed"));
        }
    }

    [Test]
    public void Report_WithSomeStoredAndSomeFailed_Succeeds()
    {
        var report = XboxImageJobReports.Of(new XboxImagesReport { Due = 2, Checked = 2, Stored = 1, Failed = 1 }, "gamer picture", new FakeLogger<XboxGamerPictureJob>());

        Assert.That(report.Result, Is.EqualTo(JobRunResult.Succeeded));
    }

    [Test]
    public async Task GamerPictureJob_WhenEveryCheckFails_Fails()
    {
        await AddAccountAsync(1);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GrunUser>, HaloApiErrorContainer>(default!, new HaloApiErrorContainer { Code = 503 }));

        var report = await RunAsync(new XboxGamerPictureJob(Source(), new FakeLogger<XboxGamerPictureJob>()));

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Failed, "1 of 1 accounts checked: 0 stored, 0 unchanged, 0 without one, 1 failed")));
    }

    [Test]
    public async Task GamerPictureJob_WhenHaloThrottles_SaysSo_AndLogsAWarning()
    {
        await AddAccountAsync(1);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(new HaloApiResultContainer<List<GrunUser>, HaloApiErrorContainer>(default!, new HaloApiErrorContainer { Code = 429, RetryAfter = TimeSpan.FromMinutes(5) }));
        var log = new FakeLogger<XboxGamerPictureJob>();

        var report = await RunAsync(new XboxGamerPictureJob(Source(), log));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Result, Is.EqualTo(JobRunResult.Succeeded), "Nothing failed: it stopped and tries again");
            Assert.That(report.Summary, Does.EndWith("Halo is throttling, stopped (wait 00:05:00)"));
            Assert.That(log.Collector.GetSnapshot().Single().Level, Is.EqualTo(LogLevel.Warning));
        }
    }

    [Test]
    public async Task EmblemJob_WithNothingDue_IsSkipped()
    {
        var report = await RunAsync(new XboxEmblemJob(Source(), new FakeLogger<XboxEmblemJob>()));

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Skipped, "Every Xbox account has its emblem checked")));
    }

    [Test]
    public async Task EmblemJob_AnAccountWithoutAnEmblem_Succeeds_AsNotFound()
    {
        await AddAccountAsync(1);
        _halo.GameCmsGetEmblemMapping().Returns(new HaloApiResultContainer<Dictionary<string, Dictionary<string, Surprenant.Grunt.Models.HaloInfinite.EmblemImageAssets>>, HaloApiErrorContainer>([], null!));
        _halo.EconomyPlayerAppearanceCustomization("xuid(1)").Returns(new HaloApiResultContainer<Surprenant.Grunt.Models.HaloInfinite.AppearanceCustomization, HaloApiErrorContainer>(default!, new HaloApiErrorContainer { Code = 404 }));

        var report = await RunAsync(new XboxEmblemJob(Source(), new FakeLogger<XboxEmblemJob>()));

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Succeeded, "1 of 1 accounts checked: 0 stored, 0 unchanged, 1 without one, 0 failed")));
    }

    [Test]
    public void TheJobs_RunEveryFiveMinutes_ASecondApart_ButNotTogether()
    {
        var now = DateTimeOffset.UtcNow;
        var pictures = (ISimpleTrigger)XboxGamerPictureJob.Definition.DefaultTrigger(now);
        var emblems = (ISimpleTrigger)XboxEmblemJob.Definition.DefaultTrigger(now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((pictures.RepeatInterval, pictures.RepeatCount, pictures.StartTimeUtc, pictures.JobKey), Is.EqualTo((TimeSpan.FromMinutes(5), -1, now + TimeSpan.FromMinutes(2), XboxGamerPictureJob.Key)));
            Assert.That((emblems.RepeatInterval, emblems.RepeatCount, emblems.StartTimeUtc, emblems.JobKey), Is.EqualTo((TimeSpan.FromMinutes(5), -1, now + TimeSpan.FromMinutes(3), XboxEmblemJob.Key)));
        }
    }
}
