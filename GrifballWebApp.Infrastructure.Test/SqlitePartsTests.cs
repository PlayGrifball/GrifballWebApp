using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Preview;
using GrifballWebApp.Infrastructure.Scheduler;
using GrifballWebApp.Application.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Surprenant.Grunt.Core;
using User = GrifballWebApp.Domain.Players.User;
using XboxUser = GrifballWebApp.Domain.Players.XboxUser;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// Pieces that read or write a real (temp-file SQLite) GrifballContext: string.Contains on SQLite, case-insensitive as
/// the other servers' collations make it (SqliteMethodCallTranslatorProviderWithContains); the preview's smoke-test
/// gamertags, made up without asking Halo (PreviewSmokeXboxUsers); and the schedule recommendation job's progress and
/// its season lookup.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SqlitePartsTests : SqliteContextFixture
{
    private static readonly string[] RedAndBlue = ["blueteam", "RedTeam"];
    private static readonly long[] BothXuids = [42, 43];
    private static readonly string[] Everyone = ["blueteam", "Other", "RedTeam"];

    private async Task SeedUsersAsync()
    {
        Context.Users.AddRange(User.Seed(userName: "RedTeam"), User.Seed(userName: "blueteam"), User.Seed(userName: "Other"));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task Contains_OnSqlite_IgnoresCase()
    {
        await SeedUsersAsync();

        var found = await Context.Users.Where(u => u.UserName!.Contains("TEAM")).Select(u => u.UserName!).OrderBy(n => n).ToListAsync();

        Assert.That(found, Is.EqualTo(RedAndBlue));
    }

    [Test]
    public async Task Contains_AnEmptyPattern_MatchesEveryRow()
    {
        await SeedUsersAsync();
        var pattern = "";

        var found = await Context.Users.Where(u => u.UserName!.Contains(pattern)).Select(u => u.UserName!).OrderBy(n => n).ToListAsync();

        Assert.That(found, Is.EqualTo(Everyone));
    }

    [Test]
    public async Task Contains_APatternInNoRow_MatchesNone()
    {
        await SeedUsersAsync();

        Assert.That(await Context.Users.AnyAsync(u => u.UserName!.Contains("xyz")), Is.False);
    }

    // ---- PreviewSmokeXboxUsers ----

    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();

    private PreviewSmokeXboxUsers SmokeUsers() => new(
        new GetsertXboxUserService(NullLogger<GetsertXboxUserService>.Instance, Context, _halo, new FakeTimeProvider()), Context);

    [Test]
    public async Task Smoke_ASmokeGamertag_IsMadeUp_WithItsOwnXuid_WithoutAskingHalo()
    {
        var (made, error) = await SmokeUsers().GetsertXboxUserByGamertag("smoke-alpha");

        Context.ChangeTracker.Clear();
        var saved = await Context.XboxUsers.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(error, Is.Null);
            Assert.That((made?.XboxUserID, made?.Gamertag), Is.EqualTo(((long?)PreviewSmokeXboxUsers.Xuid("smoke-alpha"), (string?)"smoke-alpha")));
            Assert.That((saved.XboxUserID, saved.Gamertag), Is.EqualTo((PreviewSmokeXboxUsers.Xuid("smoke-alpha"), "smoke-alpha")));
            Assert.That(saved.XboxUserID, Is.Negative, "never a real account's");
        }
        await _halo.DidNotReceiveWithAnyArgs().UserByGamertag(default!);
    }

    [Test]
    public async Task Smoke_ASmokeGamertagMadeBefore_IsTheSameAccount_WithItsUser()
    {
        var user = User.Seed(userName: "smoker", xboxUser: XboxUser.FromXbox(PreviewSmokeXboxUsers.Xuid("smoke-beta"), "smoke-beta"));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var (found, _) = await SmokeUsers().GetsertXboxUserByGamertag("smoke-beta");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(found?.User?.Id, Is.EqualTo(user.Id));
            Assert.That(await Context.XboxUsers.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Smoke_AnyOtherGamertagOrXuid_GoesToHalosOwnLookup()
    {
        Context.XboxUsers.AddRange(XboxUser.FromXbox(42, "RealPlayer"), XboxUser.FromXbox(43, "Another"));
        await Context.SaveChangesAsync();
        var smoke = SmokeUsers();

        var (byTag, _) = await smoke.GetsertXboxUserByGamertag("RealPlayer");
        var byXuid = await smoke.GetsertXboxUserByXuid(43);
        var byXuids = await smoke.GetsertXboxUsersByXuid([42, 43]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byTag?.XboxUserID, Is.EqualTo(42));
            Assert.That(byXuid.Gamertag, Is.EqualTo("Another"));
            Assert.That(byXuids.Select(x => x.XboxUserID).Order(), Is.EqualTo(BothXuids));
        }
        await _halo.DidNotReceiveWithAnyArgs().UserByGamertag(default!);
    }

    // ---- ScheduleRecommendationJob ----

    private ScheduleRecommendationJob ScheduleJob(ScheduleRunOptions options) => new(
        new ScheduleService(Context, NullLogger<ScheduleService>.Instance), new ScheduleRecommendations(Context, new FakeTimeProvider()), Options.Create(options), new FakeTimeProvider());

    [TestCase(50, 2, 50, Description = "half the generations, a fifth of the time")]
    [TestCase(10, 9, 90, Description = "the time budget nearly spent")]
    [TestCase(400, 0, 99, Description = "never 100 before it's saved")]
    [TestCase(0, 0, 0)]
    public void Percent_IsWhicheverIsFurther_TheGenerationsOrTheTimeBudget(int generation, int elapsedSeconds, int percent)
        => Assert.That(ScheduleJob(new ScheduleRunOptions { MaxGenerations = 100, TimeBudget = TimeSpan.FromSeconds(10) }).Percent(generation, TimeSpan.FromSeconds(elapsedSeconds)),
            Is.EqualTo(percent));

    [Test]
    public void Percent_WithNoTimeBudget_GoesByGenerationsAlone()
        => Assert.That(ScheduleJob(new ScheduleRunOptions { MaxGenerations = 200, TimeBudget = null }).Percent(50, TimeSpan.FromHours(1)), Is.EqualTo(25));

    [Test]
    public void Execute_ForASeasonThatIsntThere_Fails()
    {
        var job = ScheduleJob(new ScheduleRunOptions());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await job.Execute(Substitute.For<IJobExecutionContext>(), new RecommendSchedule(404, "admin")),
                Throws.Exception.With.Message.EqualTo("Season not found"));
            Assert.That(ScheduleRecommendationJob.RunName(404), Is.EqualTo("season-404"));
        }
    }
}
