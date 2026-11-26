using SpartanCatalog = GrifballWebApp.Application.Spartan.SpartanCatalog;
using SpartanCatalogCore = GrifballWebApp.Application.Spartan.SpartanCatalogCore;
using SpartanCatalogRegion = GrifballWebApp.Application.Spartan.SpartanCatalogRegion;
using SpartanCatalogPiece = GrifballWebApp.Application.Spartan.SpartanCatalogPiece;
using System.Net;
using System.Text;
using System.Text.Json;
using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Spartan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using PlayGrifball.InfiniteSpartanRender.Client;
using Quartz;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Surprenant.Grunt.Models.HaloInfinite;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>The render job: renders the Spartans users asked for (their choice, else their equipped set) with the render service
/// (a fake HTTP service here) and keeps the files; over a temp-file SQLite database.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SpartanRenderTests : SqliteContextFixture
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly SpartanCatalog Catalog = new(
        [new SpartanCatalogCore("Mark VII", [new("REGION", "helmet", 1, [new(1, "Aviator")])])], ["111"], []);
    private static readonly SpartanChoice Choice = new("Mark VII", new Dictionary<string, string> { ["helmet"] = "Aviator" });

    private sealed class FakeCatalogs(SpartanCatalog? catalog) : ISpartanCatalogSource
    {
        public int Calls { get; private set; }

        public Task<SpartanCatalog?> Get(CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(catalog);
        }
    }

    /// <summary>The render service: every job and set is done at once; a file is its job's name as bytes. Records what it was asked.</summary>
    private sealed class Service : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> Loadouts { get; } = [];
        public HttpStatusCode? FailWith { get; set; }
        public bool SetFails { get; set; }
        public bool Hangs { get; set; }
        public bool Unreachable { get; set; }
        public Action? OnRequest { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            OnRequest?.Invoke();
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add($"{request.Method} {path}");
            if (Unreachable)
                throw new HttpRequestException("No route to host");
            if (Hangs)
                await Task.Delay(Timeout.Infinite, cancellationToken);
            if (FailWith is { } status)
                return new HttpResponseMessage(status) { Content = new StringContent(new string('e', 600)) };
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Method == HttpMethod.Post && path == "/spartans")
            {
                Loadouts.Add(body);
                using var json = JsonDocument.Parse(body);
                var name = $"{json.RootElement.GetProperty("output").GetString()}-{json.RootElement.GetProperty("framing").GetString()}";
                return Json($$"""{"id":"{{name}}","state":"done","output":"{{json.RootElement.GetProperty("output").GetString()}}"}""");
            }
            if (request.Method == HttpMethod.Post && path == "/spartan-sets")
            {
                Loadouts.Add(body);
                return Json(SetJson());
            }
            if (path == "/spartan-sets/set1")
                return Json(SetJson());
            if (path.EndsWith("/file", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(path.Split('/')[2])) };
            if (path.StartsWith("/sets/", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(path)) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private string SetJson() => SetFails
            ? """{"id":"set1","state":"failed","items":[]}"""
            : """
              {"id":"set1","state":"done","items":[
               {"name":"stance-glb","state":"done","output":"glb","url":"/sets/set1/stance-glb"},
               {"name":"stance-body","state":"done","output":"png","url":"/sets/set1/stance-body"},
               {"name":"stance-head","state":"done","output":"png","url":"/sets/set1/stance-head"},
               {"name":"neutral-glb","state":"done","output":"glb","url":"/sets/set1/neutral-glb"},
               {"name":"neutral-body","state":"done","output":"png","url":"/sets/set1/neutral-body"},
               {"name":"neutral-head","state":"done","output":"png","url":"/sets/set1/neutral-head"}]}
              """;

        private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();
    private readonly FakeTimeProvider _time = new(Start);
    private readonly Service _service = new();
    private FakeCatalogs _catalogs = new(Catalog);
    private SpartanRenderOptions _options = new();
    private readonly XboxArmorThrottle _throttle = new();
    private int _seq;

    private SpartanRenderSource Source()
    {
        var mapper = new SpartanArmorMapper(_halo, new MemoryCache(new MemoryCacheOptions())) { Delay = (_, _) => Task.CompletedTask };
        var client = new InfiniteSpartanRenderClient(new HttpClient(_service) { BaseAddress = new Uri("http://render.test/") });
        return new SpartanRenderSource(Context, mapper, _catalogs, client, Options.Create(_options), _time, _throttle);
    }

    /// <summary>A user with an Xbox account, asking for a render (and with a choice when <paramref name="choice"/> is given).</summary>
    private async Task<(int UserId, long Xuid)> UserAsync(SpartanChoice? choice = null, UserSpartanRenderState state = UserSpartanRenderState.Requested,
        DateTime? requestedAt = null, DateTime? renderedAt = null, string? loadoutKey = null)
    {
        var n = ++_seq;
        var user = User.Seed(userName: $"u{n}", xboxUser: XboxUser.FromXbox(900 + n, $"gt{n}"));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        if (choice is not null)
            Context.UserSpartans.Add(new UserSpartan { UserId = user.Id, ChoiceJson = JsonSerializer.Serialize(choice, SpartanService.Json), UpdatedAt = Start.UtcDateTime });
        Context.UserSpartanRenders.Add(new UserSpartanRender
        {
            UserId = user.Id, State = state, RequestedAt = requestedAt ?? Start.UtcDateTime.AddMinutes(-n), RenderedAt = renderedAt, LoadoutKey = loadoutKey,
        });
        await Context.SaveChangesAsync();
        return (user.Id, user.XboxUserID!.Value);
    }

    private async Task EquipAsync(long xuid, DateTime? changedAt = null, string corePath = "cores/mk7.json")
    {
        Context.XboxUserArmor.Add(new XboxUserArmor
        {
            XboxUserID = xuid, Status = XboxUserArmorStatus.Found, CoreId = "mk7", CorePath = corePath, ActionPosePath = "poses/p.json",
            CheckedAt = Start.UtcDateTime, ChangedAt = changedAt ?? Start.UtcDateTime.AddDays(-1),
        });
        await Context.SaveChangesAsync();
    }

    private void Serve(string path, string? title = null, int? stance = null)
        => _halo.GameCmsGetItem(path, string.Empty).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(
            new InGameItem
            {
                CommonData = new CommonItemData { Title = new DisplayString { Value = title! } },
                StanceId = stance is { } s ? new IdentifierName { MIdentifier = s } : null!,
            }, null!));

    private async Task<UserSpartanRender> RenderOf(int userId)
        => await Context.UserSpartanRenders.AsNoTracking().SingleAsync(r => r.UserId == userId);

    private async Task<List<string>> FileNamesOf(int userId)
        => await Context.UserSpartanFiles.AsNoTracking().Where(f => f.UserId == userId).OrderBy(f => f.Name).Select(f => f.Name).ToListAsync();

    [Test]
    public async Task ADisabledJob_RendersNothing_AndDoesNotEvenLook()
    {
        _options = new SpartanRenderOptions { Enabled = false };
        await UserAsync(Choice);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Disabled, Is.True);
            Assert.That(_service.Requests, Is.Empty);
            Assert.That(_catalogs.Calls, Is.Zero);
        }
    }

    [Test]
    public async Task WithNoRenderWaiting_ARunIsOneQuery_AndTheCatalogIsNotAsked()
    {
        await UserAsync(Choice, UserSpartanRenderState.Done, renderedAt: Start.UtcDateTime);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Waiting, report.Done, report.Failed, report.Marked), Is.EqualTo((0, 0, 0, 0)));
            Assert.That(_catalogs.Calls, Is.Zero);
        }
    }

    [Test]
    public async Task WhenTheRenderServiceIsDown_TheRendersWait()
    {
        _catalogs = new FakeCatalogs(null);
        var (userId, _) = await UserAsync(Choice);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.CatalogUnavailable, report.Waiting, report.Done), Is.EqualTo((true, 1, 0)));
            Assert.That((await RenderOf(userId)).State, Is.EqualTo(UserSpartanRenderState.Requested));
        }
    }

    [Test]
    public async Task AUsersChoice_IsRenderedInTheNeutralStance_ThreeFiles_AndKept()
    {
        var (userId, _) = await UserAsync(Choice);

        var report = await Source().RunAsync();

        var render = await RenderOf(userId);
        var head = await Context.UserSpartanFiles.AsNoTracking().SingleAsync(f => f.UserId == userId && f.Name == "neutral-head");
        var glb = await Context.UserSpartanFiles.AsNoTracking().SingleAsync(f => f.UserId == userId && f.Name == "neutral-glb");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed), Is.EqualTo((1, 0)));
            Assert.That(await FileNamesOf(userId), Is.EqualTo(new List<string> { "neutral-body", "neutral-glb", "neutral-head" }));
            Assert.That((head.ContentType, Encoding.UTF8.GetString(head.Content)), Is.EqualTo(("image/png", "png-head")));
            Assert.That((glb.ContentType, Encoding.UTF8.GetString(glb.Content)), Is.EqualTo(("model/gltf-binary", "glb-body")));
            Assert.That((render.State, render.RenderedAt, render.Error), Is.EqualTo((UserSpartanRenderState.Done, Start.UtcDateTime, (string?)null)));
            Assert.That(render.LoadoutKey, Has.Length.EqualTo(64));
            Assert.That(_service.Loadouts, Has.Count.EqualTo(3));
            Assert.That(_service.Loadouts[0], Does.Contain("\"core\":\"Mark VII\"").And.Contain("\"helmet\":\"Aviator\""));
        }
    }

    [Test]
    public async Task AnEquippedSet_IsRenderedInItsStance_SixFilesFromTheSet()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Done, Is.EqualTo(1));
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(6));
            Assert.That(_service.Loadouts.Single(), Does.Contain("\"stance\":77").And.Contain("\"core\":\"Mark VII\""));
            Assert.That((await Context.UserSpartanFiles.AsNoTracking().SingleAsync(f => f.UserId == userId && f.Name == "stance-glb")).ContentType, Is.EqualTo("model/gltf-binary"));
            Assert.That((await RenderOf(userId)).State, Is.EqualTo(UserSpartanRenderState.Done));
        }
    }

    [Test]
    public async Task AChoiceWithTheEquippedStance_IsRenderedInThatStance()
    {
        var (userId, xuid) = await UserAsync(Choice);
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);

        await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(6));
            Assert.That(_service.Loadouts.Single(), Does.Contain("\"helmet\":\"Aviator\"").And.Contain("\"stance\":77"));
        }
    }

    [Test]
    public async Task ARenderAlreadyMade_ForTheSameSpartan_IsDoneWithoutAskingTheService()
    {
        var (userId, _) = await UserAsync(Choice);
        await Source().RunAsync();
        var key = (await RenderOf(userId)).LoadoutKey;
        var render = await Context.UserSpartanRenders.SingleAsync();
        render.State = UserSpartanRenderState.Requested;
        await Context.SaveChangesAsync();
        _service.Requests.Clear();

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Done, Is.EqualTo(1));
            Assert.That((await RenderOf(userId)).RenderedAt, Is.EqualTo(Start.UtcDateTime));
            Assert.That(_service.Requests, Is.Empty);
            Assert.That((await RenderOf(userId)).LoadoutKey, Is.EqualTo(key));
            Assert.That((await RenderOf(userId)).State, Is.EqualTo(UserSpartanRenderState.Done));
        }
    }

    [Test]
    public async Task ANewChoice_ReplacesTheFilesOfTheOldOne()
    {
        var (userId, _) = await UserAsync(Choice);
        await Source().RunAsync();
        var before = (await RenderOf(userId)).LoadoutKey;
        var row = await Context.UserSpartans.SingleAsync();
        row.ChoiceJson = JsonSerializer.Serialize(new SpartanChoice("Mark VII", null, null, "111"), SpartanService.Json);
        var render = await Context.UserSpartanRenders.SingleAsync();
        render.State = UserSpartanRenderState.Requested;
        await Context.SaveChangesAsync();
        _service.Loadouts.Clear();

        await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await RenderOf(userId)).LoadoutKey, Is.Not.EqualTo(before));
            Assert.That(_service.Loadouts, Has.Count.EqualTo(3));
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task AUserWithNoChoiceAndNoKnownSet_FailsWithThatSaid()
    {
        var (userId, _) = await UserAsync();

        var report = await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed), Is.EqualTo((0, 1)));
            Assert.That((render.State, render.Error), Is.EqualTo((UserSpartanRenderState.Failed, "No equipped Spartan is known for this account yet")));
        }
    }

    [Test]
    public async Task AnEquippedCoreTheRenderServiceLacks_FailsWithThatSaid()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Yoroi");

        await Source().RunAsync();

        Assert.That((await RenderOf(userId)).Error, Is.EqualTo("The render service does not have the equipped core yet"));
    }

    [Test]
    public async Task AChoiceTheCatalogNoLongerAllows_FailsWithItsProblems()
    {
        var (userId, _) = await UserAsync(new SpartanChoice("Gone"));

        await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(render.State, Is.EqualTo(UserSpartanRenderState.Failed));
            Assert.That(render.Error, Is.EqualTo("There is no core 'Gone'"));
        }
    }

    [Test]
    public async Task ARenderTheServiceRefuses_Fails_AndKeepsNoFiles()
    {
        _service.FailWith = HttpStatusCode.BadRequest;
        var (userId, _) = await UserAsync(Choice);

        var report = await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed), Is.EqualTo((0, 1)));
            Assert.That(render.State, Is.EqualTo(UserSpartanRenderState.Failed));
            Assert.That(render.Error, Is.Not.Empty);
            Assert.That(await FileNamesOf(userId), Is.Empty);
        }
    }

    [Test]
    public async Task AServiceThatCannotBeReached_PutsTheRenderBackForLater_NotFailed()
    {
        _service.Unreachable = true;
        var (userId, _) = await UserAsync(Choice);

        var report = await Source().RunAsync();
        var again = await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed, report.Retrying), Is.EqualTo((0, 0, 1)));
            Assert.That((render.State, render.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddMinutes(10))));
            Assert.That(again.Waiting, Is.Zero, "not due until the wait is over");
        }
    }

    [Test]
    public async Task AServiceThatCannotBeReached_WhileHaloIsThrottledLonger_WaitsForTheThrottle()
    {
        _service.Unreachable = true;
        _throttle.Wait(Start, TimeSpan.FromMinutes(45));
        var (userId, _) = await UserAsync(Choice);

        await Source().RunAsync();

        Assert.That((await RenderOf(userId)).RequestedAt, Is.EqualTo(Start.UtcDateTime.AddMinutes(45)));
    }

    [Test]
    public async Task AnEquippedSetWhoseLastCheckFailed_IsStillRendered()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);
        (await Context.XboxUserArmor.SingleAsync()).Status = XboxUserArmorStatus.Failed;
        await Context.SaveChangesAsync();

        var report = await Source().RunAsync();

        Assert.That((report.Done, (await RenderOf(userId)).State), Is.EqualTo((1, UserSpartanRenderState.Done)));
    }

    [Test]
    public async Task AnErrorLongerThan500Characters_IsCutTo500()
    {
        var armor = Enumerable.Range(0, 30).ToDictionary(i => $"region-{i}", _ => "piece");
        var (userId, _) = await UserAsync(new SpartanChoice("Mark VII", armor));

        await Source().RunAsync();

        Assert.That((await RenderOf(userId)).Error, Has.Length.EqualTo(500));
    }

    [Test]
    public async Task ASetTheServiceFails_FailsTheRender()
    {
        _service.SetFails = true;
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);

        await Source().RunAsync();

        Assert.That((await RenderOf(userId)).Error, Is.EqualTo("The render service could not render this Spartan"));
    }

    [Test]
    public async Task ARenderThatTakesTooLong_FailsAsTooSlow()
    {
        _options = new SpartanRenderOptions { Timeout = TimeSpan.FromMilliseconds(50) };
        _service.Hangs = true;
        var (userId, _) = await UserAsync(Choice);

        await Source().RunAsync();

        var render = await RenderOf(userId);
        Assert.That((render.State, render.Error), Is.EqualTo((UserSpartanRenderState.Failed, "The render took too long")));
    }

    [Test]
    public async Task AChoiceMadeWhileItRenders_AsksAgain_SoTheNextRunRendersIt()
    {
        var (userId, _) = await UserAsync(Choice);
        var first = true;
        _service.OnRequest = () =>
        {
            if (!first)
                return;
            first = false;
            var render = Context.UserSpartanRenders.Single();
            render.RequestedAt = Start.UtcDateTime.AddMinutes(1);
            Context.SaveChanges();
        };

        await Source().RunAsync();

        var after = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(after.State, Is.EqualTo(UserSpartanRenderState.Requested));
            Assert.That(after.RenderedAt, Is.EqualTo(Start.UtcDateTime));
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(3));
        }
    }

    [Test]
    public async Task ARun_TakesTheOldestRequestsFirst_UpToTheLimit()
    {
        _options = new SpartanRenderOptions { UsersPerRun = 2 };
        var newest = await UserAsync(Choice, requestedAt: Start.UtcDateTime.AddMinutes(-1));
        var oldest = await UserAsync(Choice, requestedAt: Start.UtcDateTime.AddMinutes(-30));
        var middle = await UserAsync(Choice, requestedAt: Start.UtcDateTime.AddMinutes(-10));

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Waiting, report.Done), Is.EqualTo((2, 2)));
            Assert.That((await RenderOf(oldest.UserId)).State, Is.EqualTo(UserSpartanRenderState.Done));
            Assert.That((await RenderOf(middle.UserId)).State, Is.EqualTo(UserSpartanRenderState.Done));
            Assert.That((await RenderOf(newest.UserId)).State, Is.EqualTo(UserSpartanRenderState.Requested));
        }
    }

    [Test]
    public async Task ALimitOfZero_StillRendersOne()
    {
        _options = new SpartanRenderOptions { UsersPerRun = 0 };
        await UserAsync(Choice);
        await UserAsync(Choice);

        var report = await Source().RunAsync();

        Assert.That(report.Waiting, Is.EqualTo(1));
    }

    [Test]
    public async Task ARenderTheJobBeganLongAgo_IsTakenAgain_ButARecentOneIsLeftAlone()
    {
        var stuck = await UserAsync(Choice, UserSpartanRenderState.Rendering, requestedAt: Start.UtcDateTime.AddHours(-3));
        var busy = await UserAsync(Choice, UserSpartanRenderState.Rendering, requestedAt: Start.UtcDateTime.AddMinutes(-30));

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Waiting, report.Done), Is.EqualTo((1, 1)));
            Assert.That((await RenderOf(stuck.UserId)).State, Is.EqualTo(UserSpartanRenderState.Done));
            Assert.That((await RenderOf(busy.UserId)).State, Is.EqualTo(UserSpartanRenderState.Rendering));
        }
    }

    [Test]
    public async Task AnEquippedSetThatChangedSinceItsRender_IsAskedForAgain_ButAChoiceOrAnUnchangedSetIsNot()
    {
        var done = Start.UtcDateTime.AddDays(-2);
        var changed = await UserAsync(null, UserSpartanRenderState.Done, renderedAt: done);
        await EquipAsync(changed.Xuid, changedAt: done.AddHours(1));
        var chosen = await UserAsync(Choice, UserSpartanRenderState.Done, renderedAt: done);
        await EquipAsync(chosen.Xuid, changedAt: done.AddHours(1));
        var unchanged = await UserAsync(null, UserSpartanRenderState.Done, renderedAt: done);
        await EquipAsync(unchanged.Xuid, changedAt: done.AddHours(-1));
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);
        _catalogs = new FakeCatalogs(null);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Marked, Is.EqualTo(1));
            Assert.That((await RenderOf(changed.UserId)).State, Is.EqualTo(UserSpartanRenderState.Requested));
            Assert.That((await RenderOf(changed.UserId)).RequestedAt, Is.EqualTo(Start.UtcDateTime));
            Assert.That((await RenderOf(chosen.UserId)).State, Is.EqualTo(UserSpartanRenderState.Done));
            Assert.That((await RenderOf(unchanged.UserId)).State, Is.EqualTo(UserSpartanRenderState.Done));
        }
    }

    [Test]
    public void TheLoadoutKey_IsTheSameWhateverTheOrderOfItsPieces_AndChangesWithAnyPartOfIt()
    {
        var armor = new Dictionary<string, string> { ["helmet"] = "A", ["glove"] = "B" };
        var reordered = new Dictionary<string, string> { ["glove"] = "B", ["helmet"] = "A" };
        var key = SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", reordered, [new(1, 1), new(2, 1)], "111"), 5), Is.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), null), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "222"), 5), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("D", armor, [new(2, 1), new(1, 1)], "111"), 5), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", null, null), null), Has.Length.EqualTo(64));
        }
    }

    [Test]
    public void TheJob_RunsEveryTwoMinutes_StartingInFive()
    {
        var trigger = (ISimpleTrigger)SpartanRenderJob.Definition.DefaultSchedule(TriggerBuilder.Create(), Start).Build();

        Assert.That((trigger.RepeatInterval, trigger.StartTimeUtc), Is.EqualTo((TimeSpan.FromMinutes(2), Start.AddMinutes(5))));
    }

    [Test]
    public async Task TheJob_RunsTheSource_AndReportsItsResult()
    {
        _options = new SpartanRenderOptions { Enabled = false };
        var job = new SpartanRenderJob(Source(), new FakeLogger<SpartanRenderJob>());
        var context = Substitute.For<IJobExecutionContext>();

        await job.Execute(context);

        var report = (JobRunReport)context.Result!;
        Assert.That((report.Result, report.Summary), Is.EqualTo((JobRunResult.Skipped, "The Spartan render job is turned off (SpartanRender:Enabled)")));
    }

    private static JobRunReport Report(Action<SpartanRenderReport> change, FakeLogger? logger = null)
    {
        var report = new SpartanRenderReport();
        change(report);
        return SpartanRenderJob.Report(report, logger ?? new FakeLogger());
    }

    [Test]
    public void TheReport_IsSkipped_WhenTheJobIsOffOrNothingWaits()
    {
        var off = Report(r => r.Disabled = true);
        var idle = Report(_ => { });

        using (Assert.EnterMultipleScope())
        {
            Assert.That((off.Result, off.Summary), Is.EqualTo((JobRunResult.Skipped, "The Spartan render job is turned off (SpartanRender:Enabled)")));
            Assert.That((idle.Result, idle.Summary), Is.EqualTo((JobRunResult.Skipped, "No Spartan render is waiting")));
        }
    }

    [Test]
    public void TheReport_Fails_WhenTheServiceIsDown_AndWarns()
    {
        var logger = new FakeLogger();

        var down = Report(r => { r.Waiting = 2; r.CatalogUnavailable = true; }, logger);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((down.Result, down.Summary), Is.EqualTo((JobRunResult.Failed, "The Spartan render service is not available; the renders wait")));
            Assert.That(logger.Collector.GetSnapshot().Select(l => l.Level), Is.EqualTo(new[] { Microsoft.Extensions.Logging.LogLevel.Warning }));
        }
    }

    [Test]
    public void TheReport_FailsOnlyWhenEveryRenderFailed()
    {
        var allFailed = Report(r => { r.Waiting = 2; r.Failed = 2; });
        var some = Report(r => { r.Waiting = 2; r.Done = 1; r.Failed = 1; });
        var all = Report(r => { r.Waiting = 1; r.Done = 1; });

        using (Assert.EnterMultipleScope())
        {
            Assert.That((allFailed.Result, allFailed.Summary), Is.EqualTo((JobRunResult.Failed, "0 rendered, 2 failed")));
            Assert.That((some.Result, some.Summary), Is.EqualTo((JobRunResult.Succeeded, "1 rendered, 1 failed")));
            Assert.That((all.Result, all.Summary), Is.EqualTo((JobRunResult.Succeeded, "1 rendered, 0 failed")));
        }
    }
}
