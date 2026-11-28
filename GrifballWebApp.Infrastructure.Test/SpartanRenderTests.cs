using SpartanCatalog = GrifballWebApp.Application.Spartan.SpartanCatalog;
using SpartanCatalogCore = GrifballWebApp.Application.Spartan.SpartanCatalogCore;
using SpartanCatalogRegion = GrifballWebApp.Application.Spartan.SpartanCatalogRegion;
using SpartanCatalogPiece = GrifballWebApp.Application.Spartan.SpartanCatalogPiece;
using SpartanCatalogWeapon = GrifballWebApp.Application.Spartan.SpartanCatalogWeapon;
using SpartanCatalogPose = GrifballWebApp.Application.Spartan.SpartanCatalogPose;
using System.Net;
using System.Text;
using System.Text.Json;
using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Spartan;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
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
        [new SpartanCatalogCore("Mark VII", [new("REGION", "helmet", 1, [new(1, "Aviator")])])], ["111"], [], ["Reflection"]);
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
               {"name":"glb","state":"done","output":"glb","url":"/sets/set1/glb"},
               {"name":"stance-body","state":"done","output":"png","url":"/sets/set1/stance-body"},
               {"name":"stance-head","state":"done","output":"png","url":"/sets/set1/stance-head"},
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
        return new SpartanRenderSource(Context, mapper, _catalogs, client, Options.Create(_options), _time, _throttle, NullLogger<SpartanRenderSource>.Instance);
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

    private async Task EquipAsync(long xuid, DateTime? changedAt = null, string corePath = "cores/mk7.json", string? weaponCoatings = null, string posePath = "poses/p.json")
    {
        Context.XboxUserArmor.Add(new XboxUserArmor
        {
            XboxUserID = xuid, Status = XboxUserArmorStatus.Found, CoreId = "mk7", CorePath = corePath, ActionPosePath = posePath, WeaponCoatingsJson = weaponCoatings,
            CheckedAt = Start.UtcDateTime, ChangedAt = changedAt ?? Start.UtcDateTime.AddDays(-1),
        });
        await Context.SaveChangesAsync();
    }

    private void Serve(string path, string? title = null, int? stance = null, int? style = null, int? weapon = null)
        => _halo.GameCmsGetItem(path, string.Empty).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(
            new InGameItem
            {
                StyleId = style is { } y ? new IdentifierName { MIdentifier = y } : null!,
                WeaponTagId = weapon,
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
        var glb = await Context.UserSpartanFiles.AsNoTracking().SingleAsync(f => f.UserId == userId && f.Name == "glb");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed), Is.EqualTo((1, 0)));
            Assert.That(await FileNamesOf(userId), Is.EqualTo(new List<string> { "glb", "neutral-body", "neutral-head" }));
            Assert.That((head.ContentType, Encoding.UTF8.GetString(head.Content)), Is.EqualTo(("image/png", "png-head")));
            Assert.That((glb.ContentType, Encoding.UTF8.GetString(glb.Content)), Is.EqualTo(("model/gltf-binary", "glb-body")));
            Assert.That((render.State, render.RenderedAt, render.Error), Is.EqualTo((UserSpartanRenderState.Done, Start.UtcDateTime, (string?)null)));
            Assert.That(render.LoadoutKey, Has.Length.EqualTo(64));
            Assert.That(_service.Loadouts, Has.Count.EqualTo(3));
            Assert.That(_service.Loadouts[0], Does.Contain("\"core\":\"Mark VII\"").And.Contain("\"helmet\":\"Aviator\""));
        }
    }

    [Test]
    public async Task TheDamageLayerIsLeftOff_UnlessTheUserKeepsIt()
    {
        await UserAsync(Choice);
        await Source().RunAsync();
        Assert.That(_service.Loadouts, Has.Count.EqualTo(3).And.All.Contain("\"disableDamage\":true"));

        _service.Loadouts.Clear();
        await UserAsync(Choice with { DisableDamage = false });
        await Source().RunAsync();
        Assert.That(_service.Loadouts, Has.Count.EqualTo(3).And.All.Contain("\"disableDamage\":false"));
    }

    [Test]
    public async Task AVisor_IsSentWithEveryNeutralLoadout()
    {
        await UserAsync(Choice with { Visor = "Reflection" });

        await Source().RunAsync();

        Assert.That(_service.Loadouts, Has.Count.EqualTo(3).And.All.Contain("\"visor\":\"Reflection\""));
    }

    [Test]
    public async Task AnEquippedSet_IsRenderedInItsStance_FiveFilesFromTheSet()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Done, Is.EqualTo(1));
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(5));
            Assert.That(_service.Loadouts.Single(), Does.Contain("\"stance\":77").And.Contain("\"core\":\"Mark VII\""));
            Assert.That((await Context.UserSpartanFiles.AsNoTracking().SingleAsync(f => f.UserId == userId && f.Name == "glb")).ContentType, Is.EqualTo("model/gltf-binary"));
            Assert.That((await RenderOf(userId)).State, Is.EqualTo(UserSpartanRenderState.Done));
        }
    }

    private static readonly SpartanCatalog WithCommando = Catalog with
    {
        Weapons = [new SpartanCatalogWeapon("VK78 Commando Rifle", "WeaponCommando", [-40348340], ["5", "6"])],
    };

    private static readonly SpartanCatalog WithGrifballWeapons = WithCommando with
    {
        Weapons =
        [
            .. WithCommando.Weapons!,
            new SpartanCatalogWeapon("Gravity Hammer", "WeaponGravityHammer", [32486], ["7", "8"]),
            new SpartanCatalogWeapon("Energy Sword", null, [44604], ["9"]),
        ],
        Poses =
        [
            new SpartanCatalogPose("Ruthless War Club", -1887947697, "Gravity Hammer"),
            new SpartanCatalogPose("Captain's Pride", -1420523120, "Energy Sword"),
            new SpartanCatalogPose("Noble Command", 1756613951, "VK78 Commando Rifle"),
            new SpartanCatalogPose("Wave", 12, null),
        ],
    };

    [Test]
    public async Task EveryModel_AlsoCarriesTheHammerAndSwordPoses_EachWeaponInThePlayersCoating_ElseTheEquippedOne()
    {
        _catalogs = new FakeCatalogs(WithGrifballWeapons);
        var (_, xuid) = await UserAsync(Choice with { WeaponCoatings = new Dictionary<string, string> { ["Energy Sword"] = "9" } });
        await EquipAsync(xuid, weaponCoatings: """{"WeaponGravityHammer":"coatings/hammer.json"}""");
        Serve("poses/p.json", stance: 77, weapon: -40348340);
        Serve("coatings/hammer.json", style: 8);

        await Source().RunAsync();

        var set = JsonDocument.Parse(_service.Loadouts.Single()).RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(set.GetProperty("extraPoses").EnumerateArray().Select(p => p.GetInt32()), Is.EquivalentTo(new[] { -1887947697, -1420523120 }), "the Grifball weapons' poses, not the others");
            Assert.That(set.GetProperty("weaponCoatings").GetProperty("Gravity Hammer").GetString(), Is.EqualTo("8"), "the hammer equipped in Halo");
            Assert.That(set.GetProperty("weaponCoatings").GetProperty("Energy Sword").GetString(), Is.EqualTo("9"), "the sword as the player chose it");
        }
    }

    [Test]
    public async Task AnEquippedSet_HoldsTheWeaponOfItsPose_InTheCoatingEquippedOnThatWeapon()
    {
        _catalogs = new FakeCatalogs(WithCommando);
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid, weaponCoatings: """{"WeaponCommando":"coatings/w.json"}""");
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77, weapon: -40348340);
        Serve("coatings/w.json", style: 6);

        await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_service.Loadouts.Single(), Does.Contain("\"weapon\":\"VK78 Commando Rifle\"").And.Contain("\"weaponCoating\":\"6\""));
            Assert.That((await RenderOf(userId)).Weapon, Is.EqualTo("VK78 Commando Rifle"), "the profile offers that weapon's coatings");
        }
    }

    [Test]
    public async Task AChoice_CoatsTheWeaponAsThePlayerChose_OverTheEquippedCoating()
    {
        _catalogs = new FakeCatalogs(WithCommando);
        var (_, xuid) = await UserAsync(Choice with { WeaponCoatings = new Dictionary<string, string> { ["VK78 Commando Rifle"] = "5" } });
        await EquipAsync(xuid, weaponCoatings: """{"WeaponCommando":"coatings/w.json"}""");
        Serve("poses/p.json", stance: 77, weapon: -40348340);
        Serve("coatings/w.json", style: 6);

        await Source().RunAsync();

        Assert.That(_service.Loadouts.Single(), Does.Contain("\"weapon\":\"VK78 Commando Rifle\"").And.Contain("\"weaponCoating\":\"5\""));
    }

    [Test]
    public async Task AChoiceWithNoEquippedSet_StillCarriesTheHammerAndSwordPoses_InTheirDefaultCoatings()
    {
        _catalogs = new FakeCatalogs(WithGrifballWeapons);
        await UserAsync(Choice);

        await Source().RunAsync();

        // No pose of their own: the neutral loadouts, the model among them carrying the extra poses, the pictures not
        var loadouts = _service.Loadouts.Select(l => JsonDocument.Parse(l).RootElement).ToList();
        var set = loadouts.Single(l => l.GetProperty("output").GetString() == "glb");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(loadouts.Where(l => l.GetProperty("output").GetString() == "png").All(l => l.TryGetProperty("extraPoses", out var p) is false || p.ValueKind == JsonValueKind.Null), "pictures carry no extra poses");
            Assert.That(set.GetProperty("extraPoses").GetArrayLength(), Is.EqualTo(2));
            Assert.That(set.TryGetProperty("weaponCoatings", out var coatings) && coatings.ValueKind == JsonValueKind.Object && coatings.EnumerateObject().Any(), Is.False);
        }
    }

    [Test]
    public async Task AChoice_IsRenderedWhenHaloCannotAnswerForAnEquippedWeaponCoating_ThatWeaponInItsDefault()
    {
        _catalogs = new FakeCatalogs(WithGrifballWeapons);
        var (userId, xuid) = await UserAsync(Choice);
        await EquipAsync(xuid, weaponCoatings: """{"WeaponGravityHammer":"coatings/hammer.json"}""");
        Serve("poses/p.json", stance: 77);
        _halo.GameCmsGetItem("coatings/hammer.json", string.Empty).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(
            null!, new HaloApiErrorContainer { Code = 503 }));

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed, report.Retrying), Is.EqualTo((1, 0, 0)));
            Assert.That(_service.Loadouts.Single(), Does.Not.Contain("Gravity Hammer\":"));
            Assert.That(await FileNamesOf(userId), Is.Not.Empty);
        }
    }

    [Test]
    public async Task AWeaponTheCatalogDoesNotHave_LeavesTheHandsEmpty_AndAnEquippedCoatingItDoesNotHave_TheDefault()
    {
        _catalogs = new FakeCatalogs(WithCommando);
        var (_, unknown) = await UserAsync();
        await EquipAsync(unknown);
        var (_, odd) = await UserAsync();
        await EquipAsync(odd, weaponCoatings: """{"WeaponCommando":"coatings/odd.json"}""", posePath: "poses/commando.json");
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77, weapon: 999);
        Serve("poses/commando.json", stance: 78, weapon: -40348340);
        Serve("coatings/odd.json", style: 42);

        await Source().RunAsync();

        var empty = _service.Loadouts.Single(l => l.Contains("\"stance\":77"));
        var held = _service.Loadouts.Single(l => l.Contains("\"stance\":78"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty, Does.Not.Contain("\"weapon\":\"").And.Not.Contain("\"weaponCoating\":\""));
            Assert.That(held, Does.Contain("\"weapon\":\"VK78 Commando Rifle\"").And.Not.Contain("\"weaponCoating\":\""));
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
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(5));
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
            Assert.That(report.Lines, Is.EqualTo(new[] { $"User (#{userId}): failed, No equipped Spartan is known for this account yet" }));
        }
    }

    [Test]
    public async Task AnEquippedCoreTheRenderServiceLacks_WaitsToTryAgain_WithThatSaid()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Yoroi");

        await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(render.Error, Is.EqualTo("The render service does not have the equipped core yet"));
            Assert.That((render.State, render.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddMinutes(10))));
        }
    }

    [Test]
    public async Task AUserWhoShowsTheirEquippedSet_AndHasNoRenderYet_IsQueuedAndRendered()
    {
        var (userId, xuid) = await UserAsync();
        Context.UserSpartanRenders.RemoveRange(await Context.UserSpartanRenders.ToListAsync());
        await Context.SaveChangesAsync();
        await EquipAsync(xuid);
        Serve("cores/mk7.json", "Mark VII");
        Serve("poses/p.json", stance: 77);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Marked, report.Done), Is.EqualTo((1, 1)));
            Assert.That((await RenderOf(userId)).State, Is.EqualTo(UserSpartanRenderState.Done));
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(5));
        }
    }

    [Test]
    public async Task AUserWithNoEquippedSetAndNoRender_IsNotQueued()
    {
        await UserAsync();
        Context.UserSpartanRenders.RemoveRange(await Context.UserSpartanRenders.ToListAsync());
        await Context.SaveChangesAsync();

        var report = await Source().RunAsync();

        Assert.That((report.Marked, report.Waiting), Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task HaloThrottling_WhileMappingTheEquippedSet_PutsTheRenderBack_AndHoldsTheThrottle()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        _halo.GameCmsGetItem("cores/mk7.json", string.Empty).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(
            null!, new HaloApiErrorContainer { Code = 429, RetryAfter = TimeSpan.FromMinutes(30) }));

        var report = await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Failed, report.Retrying), Is.EqualTo((0, 1)));
            Assert.That((render.State, render.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddMinutes(30))));
            Assert.That(_throttle.Remaining(Start), Is.EqualTo(TimeSpan.FromMinutes(30)));
        }
    }

    [Test]
    public async Task AHaloError_WithNoWaitInIt_PutsTheRenderBackAfterTheUsualDelay()
    {
        var (userId, xuid) = await UserAsync();
        await EquipAsync(xuid);
        _halo.GameCmsGetItem("cores/mk7.json", string.Empty).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(
            null!, new HaloApiErrorContainer { Code = 500 }));

        var report = await Source().RunAsync();

        var render = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Failed, report.Retrying), Is.EqualTo((0, 1)));
            Assert.That(render.State, Is.EqualTo(UserSpartanRenderState.Requested));
            Assert.That(_throttle.Remaining(Start), Is.Null, "no wait was given, so none is held");
        }
    }

    [Test]
    public async Task AUsersOwnChoice_IsRenderedWhileHaloIsDown_WithoutTheirStance()
    {
        var (userId, xuid) = await UserAsync(Choice);
        await EquipAsync(xuid);
        _halo.GameCmsGetItem("poses/p.json", string.Empty).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(
            null!, new HaloApiErrorContainer { Code = 503 }));

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Failed, report.Retrying), Is.EqualTo((1, 0, 0)));
            Assert.That(await FileNamesOf(userId), Has.Count.EqualTo(3));
            await _halo.DidNotReceive().GameCmsGetItem("cores/mk7.json", string.Empty);
        }
    }

    [Test]
    public async Task AFailureWhileStoringTheFiles_PutsTheRenderBack_AndTheNextUserStillRenders()
    {
        var (first, _) = await UserAsync(Choice);
        var (second, _) = await UserAsync(Choice);
#pragma warning disable EF1003 // DDL cannot take parameters; the id is a number this test made
        await Context.Database.ExecuteSqlRawAsync("CREATE TRIGGER no_files BEFORE INSERT ON UserSpartanFiles WHEN NEW.UserId = " + first + " BEGIN SELECT RAISE(ABORT, 'boom'); END");
#pragma warning restore EF1003

        var report = await Source().RunAsync();

        var render = await RenderOf(first);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Done, report.Retrying), Is.EqualTo((1, 1)));
            Assert.That((render.State, render.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddMinutes(10))));
            Assert.That(await FileNamesOf(first), Is.Empty);
            Assert.That(await FileNamesOf(second), Has.Count.EqualTo(3));
            Assert.That(report.Lines, Is.EquivalentTo(new[] { $"User (#{first}): will try again, The render could not be saved", $"User (#{second}): rendered" }));
        }
    }

    [Test]
    public async Task TheReport_NamesTheUser_ByTheirDisplayName()
    {
        var user = User.Seed(userName: "alice-login", displayName: "Alice", xboxUser: XboxUser.FromXbox(990, "alicegt"));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        Context.UserSpartans.Add(new UserSpartan { UserId = user.Id, ChoiceJson = JsonSerializer.Serialize(Choice, SpartanService.Json), UpdatedAt = Start.UtcDateTime });
        Context.UserSpartanRenders.Add(new UserSpartanRender { UserId = user.Id, State = UserSpartanRenderState.Requested, RequestedAt = Start.UtcDateTime.AddMinutes(-1) });
        await Context.SaveChangesAsync();

        var report = await Source().RunAsync();

        Assert.That(report.Lines, Is.EqualTo(new[] { $"Alice (#{user.Id}): rendered" }));
    }

    [TestCase(HttpStatusCode.ServiceUnavailable)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task AServerSideRenderFailure_IsRetriedLater_NotFailedForGood(HttpStatusCode status)
    {
        _service.FailWith = status;
        var (userId, _) = await UserAsync(Choice);

        var report = await Source().RunAsync();

        var after = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Failed, report.Retrying), Is.EqualTo((0, 1)));
            Assert.That(after.State, Is.EqualTo(UserSpartanRenderState.Requested));
            Assert.That(after.RequestedAt, Is.GreaterThan(Start.UtcDateTime));
        }
    }

    [Test]
    public async Task ARejectedRequest_ShowsThePlayerOnlyTheServicesMessage()
    {
        _service.FailWith = HttpStatusCode.BadRequest;
        var (userId, _) = await UserAsync(Choice);

        await Source().RunAsync();

        var after = await RenderOf(userId);
        Assert.That(after.State, Is.EqualTo(UserSpartanRenderState.Failed));
    }

    [Test]
    public async Task AFailure_DoesNotOverwriteARequestMadeWhileItRendered()
    {
        _service.FailWith = HttpStatusCode.BadRequest;
        var (userId, _) = await UserAsync(Choice);
        var newer = Start.UtcDateTime.AddMinutes(1);
        _service.OnRequest = () =>
        {
            var render = Context.UserSpartanRenders.Single();
            render.State = UserSpartanRenderState.Requested;
            render.RequestedAt = newer;
            Context.SaveChanges();
        };

        var report = await Source().RunAsync();

        var after = await RenderOf(userId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Failed, Is.EqualTo(1));
            Assert.That((after.State, after.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, newer)));
        }
    }

    [Test]
    public async Task ClearingTheChoice_WithNoEquippedSet_RemovesTheOldPictures()
    {
        var (userId, _) = await UserAsync();
        Context.UserSpartanFiles.Add(new UserSpartanFile { UserId = userId, Name = "neutral-body", ContentType = "image/png", Content = [1] });
        await Context.SaveChangesAsync();

        await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((await RenderOf(userId)).State, Is.EqualTo(UserSpartanRenderState.Failed));
            Assert.That(await FileNamesOf(userId), Is.Empty);
        }
    }

    [Test]
    public async Task AChoiceTheCatalogNoLongerAllows_KeepsThePicturesItHad()
    {
        var (userId, _) = await UserAsync(new SpartanChoice("Gone"));
        Context.UserSpartanFiles.Add(new UserSpartanFile { UserId = userId, Name = "neutral-body", ContentType = "image/png", Content = [1] });
        await Context.SaveChangesAsync();

        await Source().RunAsync();

        Assert.That(await FileNamesOf(userId), Is.EqualTo(new List<string> { "neutral-body" }));
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
    public async Task AFailedRender_IsAskedForAgain_WhenTheSetChangedAfterIt()
    {
        var failedAt = Start.UtcDateTime.AddDays(-1);
        var later = await UserAsync(null, UserSpartanRenderState.Failed, requestedAt: failedAt);
        await EquipAsync(later.Xuid, changedAt: failedAt.AddHours(1));
        var earlier = await UserAsync(null, UserSpartanRenderState.Failed, requestedAt: failedAt);
        await EquipAsync(earlier.Xuid, changedAt: failedAt.AddHours(-1));
        _catalogs = new FakeCatalogs(null);

        var report = await Source().RunAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Marked, Is.EqualTo(1));
            Assert.That((await RenderOf(later.UserId)).State, Is.EqualTo(UserSpartanRenderState.Requested));
            Assert.That((await RenderOf(earlier.UserId)).State, Is.EqualTo(UserSpartanRenderState.Failed));
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
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111", "Reflection"), 5), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("D", armor, [new(2, 1), new(1, 1)], "111"), 5), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5, "VK78 Commando Rifle"), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5, "VK78 Commando Rifle", "6"),
                Is.Not.EqualTo(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5, "VK78 Commando Rifle")));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5, extraPoses: [1]), Is.Not.EqualTo(key));
            Assert.That(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5, extraPoses: [1], weaponCoatings: new Dictionary<string, string> { ["Energy Sword"] = "9" }),
                Is.Not.EqualTo(SpartanRenderSource.KeyOf(new SpartanChoice("C", armor, [new(2, 1), new(1, 1)], "111"), 5, extraPoses: [1])));
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

    [Test]
    public void TheReport_NamesWhoseRenderEachWas()
    {
        var report = Report(r =>
        {
            r.Waiting = 2;
            r.Done = 1;
            r.Failed = 1;
            r.Lines.Add("Alice (#4): rendered");
            r.Lines.Add("Bob (#9): failed, The render took too long");
        });

        Assert.That(report.Summary, Is.EqualTo("1 rendered, 1 failed. Alice (#4): rendered; Bob (#9): failed, The render took too long"));
    }

    [Test]
    public void TheReport_CapsTheSummary_SoManyLongErrorsCannotMakeItHuge()
    {
        var report = Report(r =>
        {
            r.Waiting = 20;
            r.Failed = 20;
            for (var i = 0; i < 20; i++)
                r.Lines.Add($"User (#{i}): failed, {new string('x', 500)}");
        });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Summary, Has.Length.LessThanOrEqualTo(1000));
            Assert.That(report.Summary, Does.StartWith("0 rendered, 20 failed. User (#0): failed, xxx").And.EndWith("…"));
        }
    }

    [Test]
    public void TheReport_CountsTheRendersToTryAgain_AndNamesThem()
    {
        var report = Report(r =>
        {
            r.Waiting = 2;
            r.Done = 1;
            r.Retrying = 1;
            r.Lines.Add("Alice (#4): rendered");
            r.Lines.Add("Bob (#9): will try again, The render service is not answering");
        });

        Assert.That(report.Summary, Is.EqualTo("1 rendered, 0 failed, 1 to try again. Alice (#4): rendered; Bob (#9): will try again, The render service is not answering"));
    }
}
