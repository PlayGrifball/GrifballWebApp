using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using Match = GrifballWebApp.Domain.Halo.Match;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The armor job and its source, over a temp-file SQLite database with Halo substituted.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxArmorTests : SqliteContextFixture
{
    private static readonly DateTime Day = new(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc);

    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly XboxArmorThrottle _throttle = new();
    private int _seq;

    private XboxArmorSource Source(XboxArmorOptions? options = null)
        => new(Context, _halo, Options.Create(options ?? new XboxArmorOptions()), _time, _throttle) { Delay = (_, _) => Task.CompletedTask };

    private static HaloApiResultContainer<CustomizationData, HaloApiErrorContainer> Ok(CustomizationData data) => new(data, null!);

    private static HaloApiResultContainer<CustomizationData, HaloApiErrorContainer> Error(int code, TimeSpan? retryAfter = null)
        => new(default!, new HaloApiErrorContainer { Code = code, RetryAfter = retryAfter ?? TimeSpan.Zero });

    private static CustomizationData Armor(string helmet = "helmets/a.json", string stance = "stances/s.json") => new()
    {
        Appearance = new Appearance { ActionPosePath = "poses/p.json", StancePath = stance },
        SpartanBody = new SpartanBody { BodyType = "body/b.json", LeftArm = "arms/l.json" },
        ArmorCores = new ArmorCoreCollection
        {
            ArmorCores =
            [
                new ArmorCore { CoreId = "other", CorePath = "cores/other.json", IsEquipped = false, Themes = [new ArmorCoreTheme { IsEquipped = true, HelmetPath = "helmets/no.json" }] },
                new ArmorCore
                {
                    CoreId = "mk7", CorePath = "cores/mk7.json", IsEquipped = true,
                    Themes =
                    [
                        new ArmorCoreTheme { IsEquipped = false, HelmetPath = "helmets/unequipped.json" },
                        new ArmorCoreTheme { IsEquipped = true, ThemePath = "themes/t.json", CoatingPath = "coatings/c.json", HelmetPath = helmet, VisorPath = "visors/v.json" },
                    ],
                },
            ],
        },
    };

    /// <summary>An account, and a game it played at <paramref name="at"/>: a Late League match's when <paramref name="inEvent"/>.</summary>
    private async Task<long> PlayerAsync(DateTime? at, bool inEvent)
    {
        var n = ++_seq;
        var user = User.Seed(userName: $"u{n}", xboxUser: XboxUser.FromXbox(100 + n, $"gt{n}"));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        var xuid = user.XboxUserID!.Value;
        if (at is not { } start)
            return xuid;

        var game = Match.Seed(Guid.NewGuid(), startTime: start, endTime: start.AddMinutes(12), duration: TimeSpan.FromMinutes(12));
        game.AddTeam(0, 1, Outcomes.Won);
        game.AddTeam(1, 0, Outcomes.Lost);
        game.AddParticipant(0, xuid, new MatchParticipantStats());
        Context.Matches.Add(game);
        await Context.SaveChangesAsync();
        if (inEvent)
        {
            var other = User.Seed(userName: $"o{n}");
            Context.Users.Add(other);
            await Context.SaveChangesAsync();
            var matched = MatchedMatch.Start([user.Id], [other.Id], start);
            Context.MatchedMatches.Add(matched);
            await Context.SaveChangesAsync();
            matched.Report(game.MatchID);
            await Context.SaveChangesAsync();
        }
        return xuid;
    }

    private static async Task<JobRunReport> RunAsync(XboxArmorJob job)
    {
        var context = Substitute.For<IJobExecutionContext>();
        await job.Execute(context);
        return (JobRunReport)context.Result!;
    }

    [Test]
    public async Task Run_TakesEventPlayersFirst_TheMostRecentlyActiveFirst_ThenTheRestByTheirLastMatch_AndNeverAPlayerWithoutAMatch()
    {
        var lateNoEvent = await PlayerAsync(Day.AddDays(20), inEvent: false);
        var oldEvent = await PlayerAsync(Day, inEvent: true);
        var newEvent = await PlayerAsync(Day.AddDays(10), inEvent: true);
        var olderNoEvent = await PlayerAsync(Day.AddDays(5), inEvent: false);
        await PlayerAsync(null, inEvent: false);
        var asked = new List<string>();
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(call => { asked.Add(call.Arg<string>()); return Ok(Armor()); });

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(asked, Is.EqualTo(new[] { newEvent, oldEvent, lateNoEvent, olderNoEvent }.Select(x => $"xuid({x})")));
            Assert.That((report.Due, report.Checked, report.Stored), Is.EqualTo((4, 4, 4)));
        }
    }

    [Test]
    public async Task Run_StoresTheEquippedThemeOfTheEquippedCore_AndTheStance()
    {
        var xuid = await PlayerAsync(Day, inEvent: true);
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(Armor()));

        await Source().RunAsync();

        var row = await Context.XboxUserArmor.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(XboxUserArmorStatus.Found));
            Assert.That((row.CoreId, row.CorePath, row.ThemePath, row.CoatingPath), Is.EqualTo(("mk7", "cores/mk7.json", "themes/t.json", "coatings/c.json")));
            Assert.That((row.HelmetPath, row.VisorPath, row.GlovePath), Is.EqualTo(("helmets/a.json", "visors/v.json", (string?)null)));
            Assert.That((row.ActionPosePath, row.StancePath, row.BodyType, row.LeftArm), Is.EqualTo(("poses/p.json", "stances/s.json", "body/b.json", "arms/l.json")));
            Assert.That(row.ChangedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
        }
    }

    [Test]
    public async Task Run_AnAccountChecked_IsNotAskedAgainUntilItIsDue_AndAnUnchangedSetKeepsItsChangeTime()
    {
        var xuid = await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(Armor()));
        var firstRun = _time.GetUtcNow().UtcDateTime;
        await Source().RunAsync();

        var soon = await Source().RunAsync();
        _time.Advance(TimeSpan.FromDays(8));
        var later = await Source().RunAsync();

        var row = await Context.XboxUserArmor.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(soon.Due, Is.Zero);
            Assert.That((later.Checked, later.Unchanged, later.Stored), Is.EqualTo((1, 1, 0)));
            Assert.That(row.ChangedAt, Is.EqualTo(firstRun));
            Assert.That(row.CheckedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
        }
    }

    [Test]
    public async Task Run_ANewHelmet_ReplacesTheSet_AndMovesTheChangeTime()
    {
        var xuid = await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(Armor()));
        await Source().RunAsync();
        _time.Advance(TimeSpan.FromDays(8));
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(Armor(helmet: "helmets/b.json")));

        var report = await Source().RunAsync();

        var row = await Context.XboxUserArmor.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Stored, Is.EqualTo(1));
            Assert.That(row.HelmetPath, Is.EqualTo("helmets/b.json"));
            Assert.That(row.ChangedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
        }
    }

    [Test]
    public async Task Run_AnAccountWithNoCustomization_IsNotFound()
    {
        var xuid = await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Error(404));

        var report = await Source().RunAsync();

        Assert.That(((await Context.XboxUserArmor.AsNoTracking().SingleAsync()).Status, report.NotFound), Is.EqualTo((XboxUserArmorStatus.NotFound, 1)));
    }

    [Test]
    public async Task Run_AnAccountWhoseCustomizationVanishes_IsMarkedChanged_SoItsRenderIsRefreshed()
    {
        var xuid = await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(Armor()));
        await Source().RunAsync();
        _time.Advance(TimeSpan.FromDays(8));
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Error(404));

        await Source().RunAsync();

        var row = await Context.XboxUserArmor.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.Status, Is.EqualTo(XboxUserArmorStatus.NotFound));
            Assert.That(row.ChangedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
        }
    }

    [Test]
    public async Task Run_AFailedCheck_KeepsTheSetItHad_AndIsTriedAgainSooner()
    {
        var xuid = await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(Armor()));
        await Source().RunAsync();
        _time.Advance(TimeSpan.FromDays(8));
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Error(503));
        var failed = await Source().RunAsync();
        var tooSoon = await Source().RunAsync();
        _time.Advance(TimeSpan.FromHours(7));
        _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns<HaloApiResultContainer<CustomizationData, HaloApiErrorContainer>>(_ => throw new HttpRequestException("gateway down"));
        var thrown = await Source().RunAsync();

        var row = await Context.XboxUserArmor.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(failed.Failed, Is.EqualTo(1));
            Assert.That(tooSoon.Due, Is.Zero, "a failure is tried again after RetryFailedAfter, not at once");
            Assert.That((thrown.Checked, thrown.Failed), Is.EqualTo((1, 1)));
            Assert.That((row.Status, row.Error, row.HelmetPath), Is.EqualTo((XboxUserArmorStatus.Failed, "gateway down", "helmets/a.json")));
        }
    }

    [Test]
    public async Task Run_WhenHaloThrottles_StopsLeavingTheAccountDue_AndHoldsOffUntilTheWaitIsOver()
    {
        await PlayerAsync(Day.AddDays(1), inEvent: true);
        await PlayerAsync(Day, inEvent: true);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Error(429, TimeSpan.FromMinutes(5)));

        var first = await Source().RunAsync();
        var held = await Source().RunAsync();
        await _halo.Received(1).EconomyPlayerPublicCustomization(Arg.Any<string>());
        _time.Advance(TimeSpan.FromMinutes(6));
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Ok(Armor()));
        var after = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((first.Checked, first.ThrottledFor), Is.EqualTo((0, (TimeSpan?)TimeSpan.FromMinutes(5))));
            Assert.That(held.ThrottledFor, Is.Not.Null);
            Assert.That(after.Stored, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Run_WithTheRealDelay_ChecksEveryAccountInTurn()
    {
        await PlayerAsync(Day, inEvent: false);
        await PlayerAsync(Day.AddDays(1), inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Ok(Armor()));
        var source = new XboxArmorSource(Context, _halo, Options.Create(new XboxArmorOptions { CallsPerMinute = 60_000 }), _time, _throttle);

        var report = await source.RunAsync();

        Assert.That(report.Stored, Is.EqualTo(2));
    }

    [Test]
    public async Task Run_AnErrorWithoutACode_IsRecordedAsNoAnswer()
    {
        await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Error(0));

        await Source().RunAsync();

        Assert.That((await Context.XboxUserArmor.AsNoTracking().SingleAsync()).Error, Is.EqualTo("Halo gave no answer"));
    }

    [Test]
    public async Task Run_ALongErrorMessage_IsCutTo200Characters()
    {
        await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns<HaloApiResultContainer<CustomizationData, HaloApiErrorContainer>>(_ => throw new HttpRequestException(new string('x', 300)));

        await Source().RunAsync();

        Assert.That((await Context.XboxUserArmor.AsNoTracking().SingleAsync()).Error, Has.Length.EqualTo(200));
    }

    [Test]
    public void TheJob_RunsEveryFiveMinutes_StartingInFour()
    {
        var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

        var trigger = (Quartz.ISimpleTrigger)XboxArmorJob.Definition.DefaultSchedule(Quartz.TriggerBuilder.Create(), now).Build();

        Assert.That((trigger.RepeatInterval, trigger.StartTimeUtc), Is.EqualTo((TimeSpan.FromMinutes(5), now.AddMinutes(4))));
    }

    [Test]
    public async Task Run_ThrottledWithoutAWait_WaitsAMinute()
    {
        await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Error(429));

        var report = await Source().RunAsync();

        Assert.That(report.ThrottledFor, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public async Task Run_WhenTurnedOff_AsksNothing()
    {
        await PlayerAsync(Day, inEvent: false);

        var report = await Source(new XboxArmorOptions { Enabled = false }).RunAsync();

        Assert.That(report.Disabled, Is.True);
        await _halo.DidNotReceiveWithAnyArgs().EconomyPlayerPublicCustomization(default!);
    }

    [Test]
    public async Task Run_TakesAtMostAccountsPerRun()
    {
        await PlayerAsync(Day, inEvent: false);
        await PlayerAsync(Day.AddDays(1), inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Ok(Armor()));

        var report = await Source(new XboxArmorOptions { AccountsPerRun = 1 }).RunAsync();

        Assert.That((report.Due, report.Checked), Is.EqualTo((2, 1)));
    }

    [Test]
    public async Task Apply_ACustomizationWithoutAnEquippedCore_StoresNoPieces()
    {
        var row = new XboxUserArmor { XboxUserID = 1 };

        var changed = XboxArmorSource.Apply(row, new CustomizationData { ArmorCores = new ArmorCoreCollection { ArmorCores = [] } }, Day);

        Assert.That((changed, row.Status, row.CoreId, row.HelmetPath), Is.EqualTo((true, XboxUserArmorStatus.Found, (string?)null, (string?)null)));
    }

    [Test]
    public void Apply_AfterAFailedCheck_ThatFindsTheSameSet_IsNotAChange()
    {
        var row = new XboxUserArmor { XboxUserID = 1 };
        XboxArmorSource.Apply(row, Armor(), Day);
        row.Status = XboxUserArmorStatus.Failed;

        var changed = XboxArmorSource.Apply(row, Armor(), Day.AddDays(1));

        Assert.That((changed, row.Status, row.ChangedAt), Is.EqualTo((false, XboxUserArmorStatus.Found, Day)));
    }

    [Test]
    public void Apply_ALongPathOrABlankOne_IsCutOrDropped()
    {
        var row = new XboxUserArmor { XboxUserID = 1 };
        var data = Armor();
        data.ArmorCores.ArmorCores[1].Themes[1].HelmetPath = new string('h', 400);
        data.ArmorCores.ArmorCores[1].Themes[1].VisorPath = "  ";

        XboxArmorSource.Apply(row, data, Day);

        Assert.That((row.HelmetPath!.Length, row.VisorPath), Is.EqualTo((300, (string?)null)));
    }

    private static CustomizationData WithWeapons(string commandoCoating = "coatings/commando.json")
    {
        var data = Armor();
        data.WeaponCores = new WeaponCoreCollection
        {
            WeaponCores =
            [
                // A weapon type with one core, not marked equipped (as Halo answers): its equipped theme counts
                new WeaponCore
                {
                    CoreType = "WeaponCommando", CoreId = "commando", IsEquipped = false,
                    Themes = [new WeaponCoreTheme { IsEquipped = false, CoatingPath = "coatings/old.json" }, new WeaponCoreTheme { IsEquipped = true, CoatingPath = commandoCoating }],
                },
                // Two cores of one type: the equipped core's equipped theme
                new WeaponCore { CoreType = "WeaponAR", CoreId = "ar-a", IsEquipped = false, Themes = [new WeaponCoreTheme { IsEquipped = true, CoatingPath = "coatings/ar-a.json" }] },
                new WeaponCore { CoreType = "WeaponAR", CoreId = "ar-b", IsEquipped = true, Themes = [new WeaponCoreTheme { IsEquipped = true, CoatingPath = "coatings/ar-b.json" }] },
                new WeaponCore { CoreType = "WeaponSniper", CoreId = "s7", IsEquipped = false, Themes = [new WeaponCoreTheme { IsEquipped = true, CoatingPath = " " }] },
                // No themes, or none equipped: no coating for that type
                new WeaponCore { CoreType = "WeaponNeedler", CoreId = "n", IsEquipped = true, Themes = null! },
                new WeaponCore { CoreType = "WeaponMangler", CoreId = "m", IsEquipped = true, Themes = [new WeaponCoreTheme { IsEquipped = false, CoatingPath = "coatings/m.json" }] },
            ],
        };
        return data;
    }

    [Test]
    public void Apply_StoresTheEquippedCoatingOfEachWeaponType()
    {
        var row = new XboxUserArmor { XboxUserID = 1 };

        XboxArmorSource.Apply(row, WithWeapons(), Day);

        Assert.That(row.WeaponCoatingPaths, Is.EqualTo(new Dictionary<string, string>
        {
            ["WeaponAR"] = "coatings/ar-b.json",
            ["WeaponCommando"] = "coatings/commando.json",
        }));
    }

    [Test]
    public void Apply_ANewWeaponCoating_IsAChange_AndNoWeaponsStoresNone()
    {
        var row = new XboxUserArmor { XboxUserID = 1 };
        XboxArmorSource.Apply(row, WithWeapons(), Day);

        var changed = XboxArmorSource.Apply(row, WithWeapons("coatings/adrenal.json"), Day.AddDays(1));
        var withoutWeapons = new XboxUserArmor { XboxUserID = 2 };
        XboxArmorSource.Apply(withoutWeapons, Armor(), Day);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((changed, row.ChangedAt), Is.EqualTo((true, Day.AddDays(1))));
            Assert.That(row.WeaponCoatingPaths["WeaponCommando"], Is.EqualTo("coatings/adrenal.json"));
            Assert.That((withoutWeapons.WeaponCoatingsJson, withoutWeapons.WeaponCoatingPaths.Count), Is.EqualTo(((string?)null, 0)));
        }
    }

    [Test]
    public async Task Job_WithNothingDue_IsSkipped()
    {
        var report = await RunAsync(new XboxArmorJob(Source(), new FakeLogger<XboxArmorJob>()));

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Skipped, "Every Xbox account that played Halo Infinite has its armor checked")));
    }

    [Test]
    public async Task Job_StoringASet_Succeeds_AndSaysHowMany()
    {
        await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Ok(Armor()));

        var report = await RunAsync(new XboxArmorJob(Source(), new FakeLogger<XboxArmorJob>()));

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Succeeded, "1 of 1 accounts checked: 1 stored, 0 unchanged, 0 without one, 0 failed")));
    }

    [Test]
    public async Task Job_EveryCheckFailing_Fails()
    {
        await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Error(500));

        var report = await RunAsync(new XboxArmorJob(Source(), new FakeLogger<XboxArmorJob>()));

        Assert.That(report.Result, Is.EqualTo(JobRunResult.Failed));
    }

    [Test]
    public async Task Job_WhenThrottled_SaysSo_AndLogsIt()
    {
        await PlayerAsync(Day, inEvent: false);
        _halo.EconomyPlayerPublicCustomization(Arg.Any<string>()).Returns(Error(429, TimeSpan.FromMinutes(2)));
        var logger = new FakeLogger<XboxArmorJob>();

        var report = await RunAsync(new XboxArmorJob(Source(), logger));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Summary, Does.Contain("Halo is throttling"));
            Assert.That(logger.Collector.GetSnapshot(), Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void Job_WhenTurnedOff_IsSkipped()
    {
        var report = XboxArmorJob.Report(new XboxArmorReport { Disabled = true }, new FakeLogger<XboxArmorJob>());

        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Skipped, "The armor job is turned off (XboxArmor:Enabled)")));
    }

    [Test]
    public void Throttle_RemainsUntilItsTimeIsOver()
    {
        var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var throttle = new XboxArmorThrottle();
        throttle.Wait(now, TimeSpan.FromMinutes(2));

        Assert.That((throttle.Remaining(now.AddMinutes(1)), throttle.Remaining(now.AddMinutes(2))), Is.EqualTo((TimeSpan.FromMinutes(1), (TimeSpan?)null)));
    }
}
