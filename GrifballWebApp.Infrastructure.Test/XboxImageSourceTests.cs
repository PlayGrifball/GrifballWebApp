using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;
using GrunUser = Surprenant.Grunt.Models.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// XboxImageSource: the gamer pictures (Halo's Users call, then the picture's URL downloaded) and Halo emblems (the equipped
/// emblem, the mapping, the image from Waypoint) kept for every Xbox account, on a real (temp-file SQLite) GrifballContext with Halo
/// and the downloader substituted and the throttle recorded instead of waited.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxImageSourceTests : SqliteContextFixture
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];
    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();
    private readonly IXboxImageDownloader _downloader = Substitute.For<IXboxImageDownloader>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private readonly List<TimeSpan> _waits = [];

    private XboxImageSource Source(bool enabled = true)
        => new(Context, _halo, _downloader, Options.Create(new XboxImagesOptions { Enabled = enabled, ProfileCallsPerMinute = 3, EmblemCallsPerMinute = 60 }), _time)
        {
            Delay = (wait, _) =>
            {
                _waits.Add(wait);
                return Task.CompletedTask;
            },
        };

    private async Task AddAccountsAsync(params long[] xuids)
    {
        foreach (var xuid in xuids)
            Context.XboxUsers.Add(XboxUser.FromXbox(xuid, $"Player{xuid}"));
        await Context.SaveChangesAsync();
    }

    private static HaloApiResultContainer<T, HaloApiErrorContainer> Ok<T>(T result) => new(result, null!);
    private static HaloApiResultContainer<T, HaloApiErrorContainer> Error<T>(int code, TimeSpan? retryAfter = null)
        => new(default!, new HaloApiErrorContainer { Code = code, RetryAfter = retryAfter ?? TimeSpan.Zero });

    private void GivenUsers(params (long Xuid, Gamerpic? Pic)[] users)
        => _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(call =>
        {
            var asked = call.Arg<IEnumerable<string>>().ToHashSet();
            return Ok(users.Where(u => asked.Contains(u.Xuid.ToString()))
                .Select(u => new GrunUser { xuid = u.Xuid.ToString(), gamertag = $"Player{u.Xuid}", gamerpic = u.Pic! }).ToList());
        });

    private async Task<XboxUserImage> RowAsync(long xuid, XboxUserImageKind kind)
    {
        Context.ChangeTracker.Clear();
        return await Context.XboxUserImages.AsNoTracking().SingleAsync(i => i.XboxUserID == xuid && i.Kind == kind);
    }

    [Test]
    public async Task GamerPictures_StoresTheLargePicture_OfAnAccountWithoutOne()
    {
        await AddAccountsAsync(1);
        GivenUsers((1, new Gamerpic { small = "https://x/s.png", large = "https://x/l.png", xlarge = "https://x/xl.png" }));
        _downloader.DownloadAsync("https://x/l.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));

        var report = await Source().RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Due, report.Stored, report.Failed), Is.EqualTo((1, 1, 0)));
            Assert.That((row.Status, row.Source, row.ContentType, row.Content), Is.EqualTo((XboxUserImageStatus.Found, "https://x/l.png", "image/png", Png)));
            Assert.That(row.CheckedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime));
        }
    }

    [Test]
    public async Task GamerPictures_AskHaloTwentyAccountsToACall_SpacedToTheCallsAMinute()
    {
        await AddAccountsAsync(Enumerable.Range(1, 45).Select(i => (long)i).ToArray());
        GivenUsers();

        var report = await Source().RunGamerPicturesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Checked, Is.EqualTo(45));
            Assert.That(_waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20) }), "three calls, a wait before the second and the third");
        }
        await _halo.Received(3).Users(Arg.Any<IEnumerable<string>>());
    }

    [Test]
    public async Task GamerPictures_AnAccountHaloGaveNoPictureFor_IsNotFound_AndNotDueAgainSoon()
    {
        await AddAccountsAsync(1);
        GivenUsers((1, null));
        var source = Source();

        var first = await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(13));
        var later = await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(2));
        var again = await source.RunGamerPicturesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.NotFound, Is.EqualTo(1));
            Assert.That(later.Due, Is.Zero, "checked within the last 14 days");
            Assert.That(again.Due, Is.EqualTo(1), "14 days on, checked again");
        }
    }

    [Test]
    public async Task GamerPictures_ASourceNotChanged_KeepsTheBytes_WithoutDownloading()
    {
        await AddAccountsAsync(1);
        GivenUsers((1, new Gamerpic { large = "https://x/l.png" }));
        _downloader.DownloadAsync("https://x/l.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        var source = Source();
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(15));

        var report = await source.RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Unchanged, report.Stored), Is.EqualTo((1, 0)));
            Assert.That(row.CheckedAt, Is.EqualTo(_time.GetUtcNow().UtcDateTime), "checked again");
        }
        await _downloader.Received(1).DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GamerPictures_ANewPicture_ReplacesTheBytes()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenUsers((1, new Gamerpic { large = "https://x/old.png" }));
        _downloader.DownloadAsync("https://x/old.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenUsers((1, new Gamerpic { large = "https://x/new.jpg" }));
        _downloader.DownloadAsync("https://x/new.jpg", Arg.Any<CancellationToken>()).Returns(new DownloadedImage([9, 9], "image/jpeg"));

        await source.RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        Assert.That((row.Source, row.ContentType, row.Content), Is.EqualTo(("https://x/new.jpg", "image/jpeg", new byte[] { 9, 9 })));
    }

    [Test]
    public async Task GamerPictures_ADownloadThatFails_IsFailed_AndTriedAgainAfterSixHours()
    {
        await AddAccountsAsync(1);
        GivenUsers((1, new Gamerpic { large = "https://x/l.png" }));
        _downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<DownloadedImage>(_ => throw new InvalidOperationException("The picture is too large"));
        var source = Source();

        var first = await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromHours(5));
        var soon = await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromHours(2));
        var later = await source.RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first.Failed, Is.EqualTo(1));
            Assert.That(soon.Due, Is.Zero);
            Assert.That(later.Due, Is.EqualTo(1));
            Assert.That((row.Status, row.Error, row.Content), Is.EqualTo((XboxUserImageStatus.Failed, "The picture is too large", (byte[]?)null)));
        }
    }

    [Test]
    public async Task GamerPictures_AFailedRefresh_KeepsTheImageAlreadyStored()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenUsers((1, new Gamerpic { large = "https://x/old.png" }));
        _downloader.DownloadAsync("https://x/old.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenUsers((1, new Gamerpic { large = "https://x/new.png" }));
        _downloader.DownloadAsync("https://x/new.png", Arg.Any<CancellationToken>()).Returns<DownloadedImage>(_ => throw new InvalidOperationException("answered 500"));

        await source.RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        Assert.That((row.Status, row.Content, row.Error), Is.EqualTo((XboxUserImageStatus.Found, Png, "answered 500")), "the picture it had is still shown");
    }

    [Test]
    public async Task GamerPictures_Throttled_StopsAtOnce_StoresNothing_AndLeavesThemDue()
    {
        await AddAccountsAsync(Enumerable.Range(1, 30).Select(i => (long)i).ToArray());
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(Error<List<GrunUser>>(429, TimeSpan.FromMinutes(5)));

        var report = await Source().RunGamerPicturesAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ThrottledFor, Is.EqualTo(TimeSpan.FromMinutes(5)));
            Assert.That(await Context.XboxUserImages.AnyAsync(), Is.False);
            Assert.That(await Source().CountDueAsync(XboxUserImageKind.GamerPicture, default), Is.EqualTo(30));
        }
        await _halo.Received(1).Users(Arg.Any<IEnumerable<string>>());
    }

    [Test]
    public async Task GamerPictures_AHaloErrorOnTheCall_FailsEveryAccountOfIt()
    {
        await AddAccountsAsync(1, 2);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(Error<List<GrunUser>>(503));

        var report = await Source().RunGamerPicturesAsync();

        var row = await RowAsync(2, XboxUserImageKind.GamerPicture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Failed, Is.EqualTo(2));
            Assert.That((row.Status, row.Error), Is.EqualTo((XboxUserImageStatus.Failed, "Halo answered 503")));
        }
    }

    [Test]
    public async Task Disabled_AsksNothing()
    {
        await AddAccountsAsync(1);

        var pictures = await Source(enabled: false).RunGamerPicturesAsync();
        var emblems = await Source(enabled: false).RunEmblemsAsync();

        Assert.That((pictures.Checked, emblems.Checked), Is.EqualTo((0, 0)));
        await _halo.DidNotReceiveWithAnyArgs().Users(default!);
        await _halo.DidNotReceiveWithAnyArgs().EconomyPlayerPublicCustomization(default!);
    }

    // ---- emblems ----

    private static Dictionary<string, Dictionary<string, EmblemImageAssets>> Mapping() => new()
    {
        ["013-001-aaaa"] = new() { ["7"] = new EmblemImageAssets { EmblemCmsPath = "images/emblems/a7.png", NameplateCmsPath = "images/nameplates/a7.png" } },
    };

    private void GivenMapping() => _halo.GameCmsGetEmblemMapping().Returns(Ok(Mapping()));

    private void GivenEmblem(long xuid, string? path, int configuration = 7)
        => _halo.EconomyPlayerPublicCustomization($"xuid({xuid})").Returns(Ok(new CustomizationData
        {
            Appearance = new Appearance { Emblem = path is null ? null! : new Emblem { EmblemPath = path, ConfigurationId = configuration } },
        }));

    [Test]
    public async Task Emblems_StoresTheMappedImage_OfTheEquippedEmblem()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage("images/emblems/a7.png").Returns(Ok(Png));

        var report = await Source().RunEmblemsAsync();

        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Due, report.Stored), Is.EqualTo((1, 1)));
            Assert.That((row.Status, row.Source, row.ContentType, row.Content), Is.EqualTo((XboxUserImageStatus.Found, "images/emblems/a7.png", "image/png", Png)));
        }
    }

    [Test]
    public async Task Emblems_TwoAccountsWithTheSameEmblem_DownloadItOnce()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        GivenEmblem(2, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage("images/emblems/a7.png").Returns(Ok(Png));

        var report = await Source().RunEmblemsAsync();

        Assert.That(report.Stored, Is.EqualTo(2));
        await _halo.Received(1).GameCmsWaypointGetImage(Arg.Any<string>());
        Assert.That(_waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(1) }), "a wait between the two accounts' calls");
    }

    private void GivenItem(string itemPath, string? imagePath)
        => _halo.GameCmsGetItem(itemPath, string.Empty).Returns(Ok(new InGameItem
        {
            CommonData = new CommonItemData
            {
                DisplayPath = imagePath is null ? null! : new DisplayPath { Media = new Media { MediaUrl = new Surprenant.Grunt.Models.HaloInfinite.ApiIngress.ApiEndpoint { Path = imagePath } } },
            },
        }));

    [Test]
    public async Task Emblems_OneNotInTheMapping_TakesTheImageItsItemGives()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenItem("Inventory/Spartan/Emblems/3627007-SpartanEmblem.json", "progression/Inventory/Emblems/3627007-SpartanEmblem-SM.png");
        _halo.GameCmsGetImage("progression/Inventory/Emblems/3627007-SpartanEmblem-SM.png").Returns(Ok(Png));

        var report = await Source().RunEmblemsAsync();

        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((report.Stored, report.NotFound), Is.EqualTo((1, 0)));
            Assert.That((row.Status, row.Source, row.Content), Is.EqualTo((XboxUserImageStatus.Found, "progression/Inventory/Emblems/3627007-SpartanEmblem-SM.png", Png)));
        }
        await _halo.DidNotReceiveWithAnyArgs().GameCmsWaypointGetImage(default!);
    }

    [Test]
    public async Task Emblems_TheItemCall_WaitsItsTurnLikeTheOtherCalls()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenItem("Inventory/Spartan/Emblems/3627007-SpartanEmblem.json", "progression/Inventory/Emblems/3627007-SpartanEmblem-SM.png");
        _halo.GameCmsGetImage(Arg.Any<string>()).Returns(Ok(Png));

        await Source().RunEmblemsAsync();

        Assert.That(_waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(1) }), "a wait between the account's customization call and its item call");
    }

    [Test]
    public async Task Emblems_AMappedImageAndAnItemImageAtTheSamePath_AreNotMixedUp()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        GivenEmblem(2, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenItem("Inventory/Spartan/Emblems/3627007-SpartanEmblem.json", "images/emblems/a7.png");
        byte[] other = [0x89, 0x50, 0x4E, 0x47, 0x99];
        _halo.GameCmsWaypointGetImage("images/emblems/a7.png").Returns(Ok(Png));
        _halo.GameCmsGetImage("images/emblems/a7.png").Returns(Ok(other));

        await Source().RunEmblemsAsync();

        var mapped = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        var item = await RowAsync(2, XboxUserImageKind.HaloEmblem);
        Assert.That((mapped.Content, item.Content), Is.EqualTo((Png, other)));
    }

    [Test]
    public async Task Emblems_TwoAccountsWithTheSameUnmappedEmblem_AskForItsItemAndImageOnce()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenEmblem(2, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenItem("Inventory/Spartan/Emblems/3627007-SpartanEmblem.json", "progression/Inventory/Emblems/3627007-SpartanEmblem-SM.png");
        _halo.GameCmsGetImage(Arg.Any<string>()).Returns(Ok(Png));

        var report = await Source().RunEmblemsAsync();

        Assert.That(report.Stored, Is.EqualTo(2));
        await _halo.Received(1).GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>());
        await _halo.Received(1).GameCmsGetImage(Arg.Any<string>());
    }

    [Test]
    public async Task Emblems_OneNotInTheMapping_WhoseItemHasNoImage_IsNotFound()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenItem("Inventory/Spartan/Emblems/3627007-SpartanEmblem.json", null);

        var report = await Source().RunEmblemsAsync();

        Assert.That((report.NotFound, report.Failed), Is.EqualTo((1, 0)));
        await _halo.DidNotReceiveWithAnyArgs().GameCmsGetImage(default!);
    }

    [Test]
    public async Task Emblems_AnItemWithNoCommonData_OrNoMedia_IsNotFound()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/bare.json");
        GivenEmblem(2, "/Inventory/Spartan/Emblems/nomedia.json");
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/bare.json", string.Empty).Returns(Ok(new InGameItem { CommonData = null! }));
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/nomedia.json", string.Empty).Returns(Ok(new InGameItem
        {
            CommonData = new CommonItemData { DisplayPath = new DisplayPath { Media = new Media { MediaUrl = null! } } },
        }));

        var report = await Source().RunEmblemsAsync();

        Assert.That((report.NotFound, report.Failed), Is.EqualTo((2, 0)));
    }

    [Test]
    public async Task Emblems_AnItemWithAMissingLinkOnTheWayToItsImage_IsNotFound()
    {
        await AddAccountsAsync(1, 2, 3);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/nodisplay.json");
        GivenEmblem(2, "/Inventory/Spartan/Emblems/nomedia2.json");
        GivenEmblem(3, "/Inventory/Spartan/Emblems/nopath.json");
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/nodisplay.json", string.Empty).Returns(Ok(new InGameItem { CommonData = new CommonItemData { DisplayPath = null! } }));
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/nomedia2.json", string.Empty).Returns(Ok(new InGameItem { CommonData = new CommonItemData { DisplayPath = new DisplayPath { Media = null! } } }));
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/nopath.json", string.Empty).Returns(Ok(new InGameItem
        {
            CommonData = new CommonItemData { DisplayPath = new DisplayPath { Media = new Media { MediaUrl = new Surprenant.Grunt.Models.HaloInfinite.ApiIngress.ApiEndpoint { Path = null! } } } },
        }));

        var report = await Source().RunEmblemsAsync();

        Assert.That((report.NotFound, report.Failed), Is.EqualTo((3, 0)));
    }

    [Test]
    public async Task Emblems_AnItemImageOverTheSizeLimit_IsFailed()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        GivenItem("Inventory/Spartan/Emblems/3627007-SpartanEmblem.json", "progression/Inventory/Emblems/3627007-SpartanEmblem-SM.png");
        _halo.GameCmsGetImage(Arg.Any<string>()).Returns(Ok(new byte[XboxUserImage.MaxContentBytes + 1]));

        var report = await Source().RunEmblemsAsync();

        Assert.That((report.Stored, report.Failed), Is.EqualTo((0, 1)));
    }

    [Test]
    public async Task Emblems_AnItemCallThatAnswersNothing_IsFailed_AndOneThrottledWithoutAWait_WaitsAMinute()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/silent.json");
        _halo.GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>()).Returns(new HaloApiResultContainer<InGameItem, HaloApiErrorContainer>(null!, null!));

        var failed = await Source().RunEmblemsAsync();
        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);

        Assert.That((failed.Failed, row.Error), Is.EqualTo((1, "The emblem item: Halo gave no answer")));

        _halo.GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>()).Returns(Error<InGameItem>(429));
        _time.Advance(TimeSpan.FromHours(7));
        var throttled = await Source().RunEmblemsAsync();

        Assert.That(throttled.ThrottledFor, Is.EqualTo(TimeSpan.FromMinutes(1)));
    }

    [Test]
    public async Task Emblems_AMappedEmblemWithoutTheEquippedConfiguration_FallsBackToItsItem()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json", configuration: 9);
        _halo.GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>()).Returns(Error<InGameItem>(404));

        var report = await Source().RunEmblemsAsync();

        Assert.That(report.NotFound, Is.EqualTo(1));
    }

    [Test]
    public async Task Emblems_AnItemHalo404s_IsNotFound_AndOneItemCallFailing_IsFailed()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/gone.json");
        GivenEmblem(2, "/Inventory/Spartan/Emblems/down.json");
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/gone.json", string.Empty).Returns(Error<InGameItem>(404));
        _halo.GameCmsGetItem("Inventory/Spartan/Emblems/down.json", string.Empty).Returns(Error<InGameItem>(503));

        var report = await Source().RunEmblemsAsync();

        Assert.That((report.NotFound, report.Failed), Is.EqualTo((1, 1)));
    }

    [Test]
    public async Task Emblems_TheItemCallThrottled_StopsAtOnce_AndRecordsNothingForThatAccount()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/Inventory/Spartan/Emblems/3627007-SpartanEmblem.json");
        _halo.GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>()).Returns(Error<InGameItem>(429, TimeSpan.FromSeconds(30)));

        var report = await Source().RunEmblemsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ThrottledFor, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(await Context.XboxUserImages.CountAsync(i => i.Kind == XboxUserImageKind.HaloEmblem), Is.Zero);
        }
        await _halo.DidNotReceive().EconomyPlayerPublicCustomization("xuid(2)");
    }

    [Test]
    public async Task Emblems_OneNotInTheMapping_IsNotFound()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-unknown.json");
        GivenEmblem(2, "/inventory/armor/emblems/013-001-aaaa.json", configuration: 99);
        _halo.GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>()).Returns(Error<InGameItem>(404));

        var report = await Source().RunEmblemsAsync();

        Assert.That(report.NotFound, Is.EqualTo(2));
        await _halo.DidNotReceiveWithAnyArgs().GameCmsWaypointGetImage(default!);
    }

    [Test]
    public async Task Emblems_NoEmblemEquipped_OrNoAppearance_IsNotFound()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, null);
        _halo.EconomyPlayerPublicCustomization("xuid(2)").Returns(Error<CustomizationData>(404));

        var report = await Source().RunEmblemsAsync();

        Assert.That((report.NotFound, report.Failed), Is.EqualTo((2, 0)));
    }

    [Test]
    public async Task Emblems_AnImageThatIsNotAPicture_IsFailed()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage(Arg.Any<string>()).Returns(Ok(new byte[] { 1, 2, 3 }));

        var report = await Source().RunEmblemsAsync();

        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        Assert.That((report.Failed, row.Status, row.Content), Is.EqualTo((1, XboxUserImageStatus.Failed, (byte[]?)null)));
    }

    [Test]
    public async Task Emblems_Throttled_StopsAtOnce_AndRecordsNothingForThatAccount()
    {
        await AddAccountsAsync(1, 2, 3);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.EconomyPlayerPublicCustomization("xuid(2)").Returns(Error<CustomizationData>(429, TimeSpan.FromSeconds(30)));
        _halo.GameCmsWaypointGetImage(Arg.Any<string>()).Returns(Ok(Png));

        var report = await Source().RunEmblemsAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ThrottledFor, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(await Context.XboxUserImages.Select(i => i.XboxUserID).ToListAsync(), Is.EqualTo(new long[] { 1 }), "account 1 kept; 2 and 3 still due");
        }
        await _halo.DidNotReceive().EconomyPlayerPublicCustomization("xuid(3)");
    }

    [Test]
    public async Task Emblems_NoneDue_AsksHaloNothing()
    {
        var report = await Source().RunEmblemsAsync();

        Assert.That(report.Due, Is.Zero);
        await _halo.DidNotReceiveWithAnyArgs().GameCmsGetEmblemMapping();
    }

    [Test]
    public async Task Emblems_TheMappingMissing_FailsTheRun_WithoutRecordingAnyAccount()
    {
        await AddAccountsAsync(1);
        _halo.GameCmsGetEmblemMapping().Returns(Error<Dictionary<string, Dictionary<string, EmblemImageAssets>>>(503));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Source().RunEmblemsAsync());

        Context.ChangeTracker.Clear();
        Assert.That((failure!.Message, await Context.XboxUserImages.AnyAsync()), Is.EqualTo(("The emblem mapping isn't available: Halo answered 503", false)));
    }

    [Test]
    public async Task GamerPictures_AHaloCallThatThrows_FailsItsAccounts_WithTheMessage()
    {
        await AddAccountsAsync(1);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns<HaloApiResultContainer<List<GrunUser>, HaloApiErrorContainer>>(_ => throw new HttpRequestException("gateway down"));

        var report = await Source().RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        Assert.That((report.Failed, row.Error), Is.EqualTo((1, "gateway down")));
    }

    [Test]
    public async Task Emblems_AnAppearanceCallThatThrows_OrAnErrorBesides404_IsFailed()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        _halo.EconomyPlayerPublicCustomization("xuid(1)").Returns<HaloApiResultContainer<CustomizationData, HaloApiErrorContainer>>(_ => throw new HttpRequestException("gateway down"));
        _halo.EconomyPlayerPublicCustomization("xuid(2)").Returns(Error<CustomizationData>(500));

        var report = await Source().RunEmblemsAsync();

        var first = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        var second = await RowAsync(2, XboxUserImageKind.HaloEmblem);
        Assert.That((report.Failed, first.Error, second.Error), Is.EqualTo((2, "gateway down", "Halo answered 500")));
    }

    [Test]
    public async Task Emblems_TheMappingThrottled_StopsTheRun_BeforeAnyAccount()
    {
        await AddAccountsAsync(1);
        _halo.GameCmsGetEmblemMapping().Returns(Error<Dictionary<string, Dictionary<string, EmblemImageAssets>>>(429, TimeSpan.FromSeconds(45)));

        var report = await Source().RunEmblemsAsync();

        Assert.That(report.ThrottledFor, Is.EqualTo(TimeSpan.FromSeconds(45)));
        await _halo.DidNotReceiveWithAnyArgs().EconomyPlayerPublicCustomization(default!);
    }

    [Test]
    public async Task Emblems_TheImageNotAnswered_IsFailed_WithWhatHaloSaid()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage(Arg.Any<string>()).Returns(Error<byte[]>(502));

        await Source().RunEmblemsAsync();

        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        Assert.That((row.Status, row.Error), Is.EqualTo((XboxUserImageStatus.Failed, "The emblem image: Halo answered 502")));
    }

    [Test]
    public async Task Emblems_TheSameEmblemOnTheNextCheck_IsKept_WithoutDownloading()
    {
        await AddAccountsAsync(1);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage("images/emblems/a7.png").Returns(Ok(Png));
        var source = Source();
        await source.RunEmblemsAsync();
        _time.Advance(TimeSpan.FromDays(15));

        var report = await source.RunEmblemsAsync();

        Assert.That((report.Unchanged, report.Stored), Is.EqualTo((1, 0)));
        await _halo.Received(1).GameCmsWaypointGetImage(Arg.Any<string>());
    }

    [Test]
    public void TheDownloaderClient_HasATimeout_AndAUserAgent()
    {
        using var client = new HttpClient();

        XboxImageDownloader.Configure(client);

        Assert.That((client.Timeout, client.DefaultRequestHeaders.UserAgent.ToString()), Is.EqualTo((TimeSpan.FromSeconds(30), "GrifballWebApp/1.0 (gamer pictures)")));
    }

    [Test]
    public async Task GamerPictures_AnAnswerWithNeitherResultNorCode_IsFailed_AsNoAnswer()
    {
        await AddAccountsAsync(1);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(Error<List<GrunUser>>(0));

        await Source().RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        Assert.That(row.Error, Is.EqualTo("Halo gave no answer"));
    }

    [Test]
    public async Task AFailureMessageOverTwoHundredCharacters_IsCut()
    {
        await AddAccountsAsync(1);
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns<HaloApiResultContainer<List<GrunUser>, HaloApiErrorContainer>>(_ => throw new HttpRequestException(new string('x', 300)));

        await Source().RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        Assert.That(row.Error, Has.Length.EqualTo(200));
    }

    [Test]
    public async Task GamerPictures_OneStoredAndOneFailed_CountsBoth()
    {
        await AddAccountsAsync(1, 2);
        GivenUsers((1, new Gamerpic { large = "https://x/1.png" }), (2, new Gamerpic { large = "https://x/2.png" }));
        _downloader.DownloadAsync("https://x/1.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        _downloader.DownloadAsync("https://x/2.png", Arg.Any<CancellationToken>()).Returns<DownloadedImage>(_ => throw new InvalidOperationException("answered 500"));

        var report = await Source().RunGamerPicturesAsync();

        Assert.That((report.Stored, report.Failed), Is.EqualTo((1, 1)));
    }

    [Test]
    public async Task Emblems_AnAppearanceWithoutAnEmblem_OrAMappingWithoutAPath_IsNotFound()
    {
        await AddAccountsAsync(1, 2);
        _halo.GameCmsGetEmblemMapping().Returns(Ok(new Dictionary<string, Dictionary<string, EmblemImageAssets>>
        {
            ["013-001-aaaa"] = new() { ["7"] = new EmblemImageAssets { EmblemCmsPath = " " } },
        }));
        _halo.EconomyPlayerPublicCustomization("xuid(1)").Returns(Ok(new CustomizationData { Appearance = null! }));
        GivenEmblem(2, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsGetItem(Arg.Any<string>(), Arg.Any<string>()).Returns(Error<InGameItem>(404));

        var report = await Source().RunEmblemsAsync();

        Assert.That(report.NotFound, Is.EqualTo(2));
    }

    [Test]
    public async Task XboxImageService_GivesAStoredImageWithoutAType_AsBinary_AndTheAccountOfARow()
    {
        await AddAccountsAsync(1);
        Context.XboxUserImages.Add(new XboxUserImage { XboxUserID = 1, Kind = XboxUserImageKind.GamerPicture, Status = XboxUserImageStatus.Found, Content = Png, CheckedAt = _time.GetUtcNow().UtcDateTime });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var image = await new XboxImageService(Context).GetAsync(1, XboxUserImageKind.GamerPicture);
        var row = await Context.XboxUserImages.Include(i => i.XboxUser).SingleAsync();

        Assert.That((image!.ContentType, row.XboxUser.XboxUserID), Is.EqualTo(("application/octet-stream", 1L)));
    }

    [Test]
    public async Task GamerPictures_AFailedRefreshOfAStoredPicture_IsTriedAgainAfterSixHours_NotFourteenDays()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenUsers((1, new Gamerpic { large = "https://x/old.png" }));
        _downloader.DownloadAsync("https://x/old.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenUsers((1, new Gamerpic { large = "https://x/new.png" }));
        _downloader.DownloadAsync("https://x/new.png", Arg.Any<CancellationToken>()).Returns<DownloadedImage>(_ => throw new InvalidOperationException("answered 500"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromHours(7));

        var again = await source.RunGamerPicturesAsync();

        Assert.That(again.Due, Is.EqualTo(1));
    }

    [Test]
    public async Task GamerPictures_AccountsWithoutARow_GoBeforeAccountsThatKeepFailing()
    {
        await AddAccountsAsync(1, 2);
        var options = new XboxImagesOptions { Enabled = true, ProfileCallsPerMinute = 3, GamerPictureAccountsPerRun = 1 };
        var source = new XboxImageSource(Context, _halo, _downloader, Options.Create(options), _time) { Delay = (_, _) => Task.CompletedTask };
        GivenUsers((1, new Gamerpic { large = "https://x/1.png" }), (2, new Gamerpic { large = "https://x/2.png" }));
        _downloader.DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<DownloadedImage>(_ => throw new InvalidOperationException("answered 403"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromHours(7));

        await source.RunGamerPicturesAsync();

        Context.ChangeTracker.Clear();
        Assert.That(await Context.XboxUserImages.Select(i => i.XboxUserID).OrderBy(x => x).ToListAsync(), Is.EqualTo(new long[] { 1, 2 }));
    }

    [Test]
    public async Task Emblems_AThrottledImageCall_StopsTheRun_AndRecordsNothingForTheAccount()
    {
        await AddAccountsAsync(1, 2);
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        GivenEmblem(2, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage(Arg.Any<string>()).Returns(Error<byte[]>(429, TimeSpan.FromMinutes(3)));

        var report = await Source().RunEmblemsAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.ThrottledFor, Is.EqualTo(TimeSpan.FromMinutes(3)));
            Assert.That((report.Checked, report.Failed), Is.EqualTo((0, 0)));
            Assert.That(await Context.XboxUserImages.CountAsync(), Is.Zero);
            await _halo.Received(1).GameCmsWaypointGetImage(Arg.Any<string>());
        }
    }

    [Test]
    public async Task Emblems_AMappingThatNoLongerHasTheEmblem_KeepsTheStoredImage()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage("images/emblems/a7.png").Returns(Ok(Png));
        await source.RunEmblemsAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenEmblem(1, "/inventory/armor/emblems/013-001-unknown.json");

        await source.RunEmblemsAsync();

        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        Assert.That((row.Status, row.Content), Is.EqualTo((XboxUserImageStatus.Found, Png)));
    }

    [Test]
    public async Task Run_CountsEveryAccountDue_NotJustTheBatch()
    {
        await AddAccountsAsync(1, 2, 3);
        var options = new XboxImagesOptions { Enabled = true, ProfileCallsPerMinute = 3, GamerPictureAccountsPerRun = 2 };
        var source = new XboxImageSource(Context, _halo, _downloader, Options.Create(options), _time) { Delay = (_, _) => Task.CompletedTask };
        GivenUsers();

        var report = await source.RunGamerPicturesAsync();

        Assert.That((report.Due, report.Checked), Is.EqualTo((3, 2)));
    }

    [Test]
    public async Task GamerPictures_ADownloadThatTimesOut_IsFailed_AndTheChunkStillSaves()
    {
        await AddAccountsAsync(1, 2);
        GivenUsers((1, new Gamerpic { large = "https://x/1.png" }), (2, new Gamerpic { large = "https://x/2.png" }));
        _downloader.DownloadAsync("https://x/1.png", Arg.Any<CancellationToken>()).Returns<DownloadedImage>(_ => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"));
        _downloader.DownloadAsync("https://x/2.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));

        var report = await Source().RunGamerPicturesAsync();

        Assert.That((report.Failed, report.Stored), Is.EqualTo((1, 1)));
    }

    [Test]
    public async Task GamerPictures_AUrlLongerThanTheSourceColumn_IsFailed_WithoutDownloading()
    {
        await AddAccountsAsync(1);
        var url = "https://x/" + new string('a', 500);
        GivenUsers((1, new Gamerpic { large = url }));

        var report = await Source().RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.Failed, Is.EqualTo(1));
            Assert.That(row.Error, Is.EqualTo("The picture's address is too long"));
            await _downloader.DidNotReceive().DownloadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }

    [Test]
    public async Task GamerPictures_AnAccountHalosAnswerOmits_KeepsTheStoredPicture_UntilTheNextCheck()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenUsers((1, new Gamerpic { large = "https://x/old.png" }));
        _downloader.DownloadAsync("https://x/old.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenUsers();

        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromHours(7));
        var again = await source.RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((row.Status, row.Content), Is.EqualTo((XboxUserImageStatus.Found, Png)));
            Assert.That(again.Due, Is.Zero, "not re-checked every few hours");
        }
    }

    [Test]
    public async Task GamerPictures_AnAccountHaloAnswersWithoutAPicture_LosesTheStoredOne()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenUsers((1, new Gamerpic { large = "https://x/old.png" }));
        _downloader.DownloadAsync("https://x/old.png", Arg.Any<CancellationToken>()).Returns(new DownloadedImage(Png, "image/png"));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenUsers((1, null));

        await source.RunGamerPicturesAsync();

        var row = await RowAsync(1, XboxUserImageKind.GamerPicture);
        Assert.That((row.Status, row.Content), Is.EqualTo((XboxUserImageStatus.NotFound, (byte[]?)null)));
    }

    [Test]
    public async Task Emblems_AnAccountWithNoEquippedEmblem_LosesTheStoredOne()
    {
        await AddAccountsAsync(1);
        var source = Source();
        GivenMapping();
        GivenEmblem(1, "/inventory/armor/emblems/013-001-aaaa.json");
        _halo.GameCmsWaypointGetImage("images/emblems/a7.png").Returns(Ok(Png));
        await source.RunEmblemsAsync();
        _time.Advance(TimeSpan.FromDays(15));
        GivenEmblem(1, null);

        await source.RunEmblemsAsync();

        var row = await RowAsync(1, XboxUserImageKind.HaloEmblem);
        Assert.That((row.Status, row.Content), Is.EqualTo((XboxUserImageStatus.NotFound, (byte[]?)null)));
    }

    [Test]
    public async Task GamerPictures_AThrottle_IsRemembered_SoTheNextRunDoesNotCallHalo()
    {
        await AddAccountsAsync(1);
        var source = Source();
        _halo.Users(Arg.Any<IEnumerable<string>>()).Returns(Error<List<GrunUser>>(429, TimeSpan.FromMinutes(10)));
        await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromMinutes(5));

        var during = await source.RunGamerPicturesAsync();
        _time.Advance(TimeSpan.FromMinutes(6));
        var after = await source.RunGamerPicturesAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((during.Due, during.Checked, during.ThrottledFor), Is.EqualTo((1, 0, (TimeSpan?)TimeSpan.FromMinutes(5))));
            await _halo.Received(2).Users(Arg.Any<IEnumerable<string>>());
            Assert.That(after.ThrottledFor, Is.EqualTo(TimeSpan.FromMinutes(10)), "asked again once the wait was over");
        }
    }

    [Test]
    public async Task Emblems_AThrottle_IsRemembered_SoTheNextRunDoesNotCallHalo()
    {
        await AddAccountsAsync(1);
        var source = Source();
        _halo.GameCmsGetEmblemMapping().Returns(Error<Dictionary<string, Dictionary<string, EmblemImageAssets>>>(429, TimeSpan.FromMinutes(10)));
        await source.RunEmblemsAsync();
        _time.Advance(TimeSpan.FromMinutes(5));

        var during = await source.RunEmblemsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(during.ThrottledFor, Is.EqualTo(TimeSpan.FromMinutes(5)));
            await _halo.Received(1).GameCmsGetEmblemMapping();
        }
    }

    // ---- pure helpers ----

    [Test]
    public void SniffContentType_ReadsPngJpegAndWebp_AndNothingElse()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(XboxImageSource.SniffContentType([0x89, 0x50, 0x4E, 0x47, 0]), Is.EqualTo("image/png"));
            Assert.That(XboxImageSource.SniffContentType([0xFF, 0xD8, 0xFF, 0xE0]), Is.EqualTo("image/jpeg"));
            Assert.That(XboxImageSource.SniffContentType([0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50]), Is.EqualTo("image/webp"));
            Assert.That(XboxImageSource.SniffContentType([0x3C, 0x73, 0x76, 0x67]), Is.Null, "an SVG is not served");
            Assert.That(XboxImageSource.SniffContentType([]), Is.Null);
            Assert.That(XboxImageSource.SniffContentType([0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x41, 0x56, 0x45]), Is.Null, "RIFF but not WebP");
        }
    }

    [Test]
    public void PictureUrlOf_PrefersLarge_ThenTheNearestSize()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(XboxImageSource.PictureUrlOf(new Gamerpic { small = "s", medium = "m", large = "l", xlarge = "x" }), Is.EqualTo("l"));
            Assert.That(XboxImageSource.PictureUrlOf(new Gamerpic { small = "s", medium = " ", xlarge = "x" }), Is.EqualTo("x"));
            Assert.That(XboxImageSource.PictureUrlOf(new Gamerpic { small = "s" }), Is.EqualTo("s"));
            Assert.That(XboxImageSource.PictureUrlOf(null), Is.Null);
        }
    }

    // ---- reading them ----

    [Test]
    public async Task XboxImageService_FindsTheXboxAccountOfAUser_OrNothing()
    {
        var linked = GrifballWebApp.Domain.Players.User.Seed(userName: "linked", xboxUser: XboxUser.FromXbox(77, "Linked"));
        var unlinked = GrifballWebApp.Domain.Players.User.Seed(userName: "unlinked");
        Context.Users.AddRange(linked, unlinked);
        await Context.SaveChangesAsync();
        var service = new XboxImageService(Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await service.GetXboxUserIdOfAsync(linked.Id), Is.EqualTo(77L));
            Assert.That(await service.GetXboxUserIdOfAsync(unlinked.Id), Is.Null, "no Xbox account linked");
            Assert.That(await service.GetXboxUserIdOfAsync(99999), Is.Null, "no such user");
        }
    }

    [Test]
    public async Task XboxImageService_GivesOnlyAStoredImage_OfTheKindAsked()
    {
        await AddAccountsAsync(1, 2);
        Context.XboxUserImages.Add(new XboxUserImage { XboxUserID = 1, Kind = XboxUserImageKind.GamerPicture, Status = XboxUserImageStatus.Found, Content = Png, ContentType = "image/png", CheckedAt = _time.GetUtcNow().UtcDateTime });
        Context.XboxUserImages.Add(new XboxUserImage { XboxUserID = 2, Kind = XboxUserImageKind.GamerPicture, Status = XboxUserImageStatus.NotFound, CheckedAt = _time.GetUtcNow().UtcDateTime });
        await Context.SaveChangesAsync();
        var service = new XboxImageService(Context);

        var found = await service.GetAsync(1, XboxUserImageKind.GamerPicture);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((found?.Content, found?.ContentType), Is.EqualTo((Png, "image/png")));
            Assert.That(await service.GetAsync(1, XboxUserImageKind.HaloEmblem), Is.Null, "the other kind");
            Assert.That(await service.GetAsync(2, XboxUserImageKind.GamerPicture), Is.Null, "none stored");
            Assert.That(await service.GetAsync(3, XboxUserImageKind.GamerPicture), Is.Null, "no such account");
        }
    }
    [Test]
    public async Task ANewUser_WhoChoseTheEmblem_KeepsIt_AndOneWhoChoseNothingGetsTheSpartan()
    {
        var emblem = GrifballWebApp.Domain.Players.User.Seed(userName: "emblem");
        emblem.ChooseProfilePicture(ProfilePictureChoice.Emblem);
        var none = GrifballWebApp.Domain.Players.User.Seed(userName: "none");
        Context.Users.AddRange(emblem, none);
        await Context.SaveChangesAsync();

        var stored = await Context.Users.AsNoTracking().Where(u => u.Id == emblem.Id || u.Id == none.Id).ToDictionaryAsync(u => u.Id, u => u.ProfilePicture);

        Assert.That((stored[emblem.Id], stored[none.Id]), Is.EqualTo((ProfilePictureChoice.Emblem, ProfilePictureChoice.Spartan)));
    }

    [Test]
    public async Task XboxImageService_ShowsTheEmblemUnlessTheUserChoseTheGamerPicture_AndFallsBackToTheOther()
    {
        await AddAccountsAsync(1, 2, 3, 4);
        var now = _time.GetUtcNow().UtcDateTime;
        foreach (var (xuid, kind) in new[] { (1L, XboxUserImageKind.GamerPicture), (1L, XboxUserImageKind.HaloEmblem), (2L, XboxUserImageKind.GamerPicture), (3L, XboxUserImageKind.HaloEmblem) })
            Context.XboxUserImages.Add(new XboxUserImage { XboxUserID = xuid, Kind = kind, Status = XboxUserImageStatus.Found, Content = Png, ContentType = "image/png", CheckedAt = now });
        var chose = GrifballWebApp.Domain.Players.User.Seed(userName: "chose", xboxUser: Context.XboxUsers.Local.First(x => x.XboxUserID == 4));
        var user = GrifballWebApp.Domain.Players.User.Seed(userName: "user", xboxUser: Context.XboxUsers.Local.First(x => x.XboxUserID == 1));
        user.ChooseProfilePicture(ProfilePictureChoice.GamerPicture);
        Context.Users.AddRange(chose, user);
        await Context.SaveChangesAsync();
        var service = new XboxImageService(Context);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await service.GetShownKindAsync(1), Is.EqualTo(XboxUserImageKind.GamerPicture), "their user chose it");
            Assert.That(await service.GetShownKindAsync(1, ProfilePictureChoice.Emblem), Is.EqualTo(XboxUserImageKind.HaloEmblem), "a choice given wins");
            Assert.That(await service.GetShownKindAsync(2), Is.EqualTo(XboxUserImageKind.GamerPicture), "no user and no emblem");
            Assert.That(await service.GetShownKindAsync(3), Is.EqualTo(XboxUserImageKind.HaloEmblem), "no user: the emblem");
            Assert.That(await service.GetShownKindAsync(4), Is.Null, "nothing stored");
        }
    }

}

[TestFixture]
public class XboxImageDownloaderTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Image(byte[] bytes, string type = "image/png")
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        return response;
    }

    private static XboxImageDownloader Downloader(Handler handler) => new(new HttpClient(handler));

    [Test]
    public async Task DownloadAsync_AnImageOverHttps_GivesItsBytesAndType()
    {
        var image = await Downloader(new Handler(_ => Image(Png, "image/PNG"))).DownloadAsync("https://images.example/pic.png", default);

        Assert.That((image.Content, image.ContentType), Is.EqualTo((Png, "image/png")));
    }

    [Test]
    public async Task DownloadAsync_AnImageThatIsNotPngJpegOrWebp_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(new Handler(_ => Image("<svg onload=alert(1)/>"u8.ToArray(), "image/svg+xml"))).DownloadAsync("https://x/p", default));

        Assert.That(failure!.Message, Is.EqualTo("The picture isn't a PNG, JPEG or WebP"));
    }

    [Test]
    public async Task DownloadAsync_AnAnswerWithoutAType_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(new Handler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent([1]) })).DownloadAsync("https://x/p", default));

        Assert.That(failure!.Message, Is.EqualTo("The picture's address didn't serve an image"));
    }

    [Test]
    public async Task DownloadAsync_NotHttps_IsRefused_WithoutAnyRequest()
    {
        var handler = new Handler(_ => Image([1]));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(handler).DownloadAsync("http://images.example/pic.png", default));

        Assert.That((failure!.Message, handler.Requests), Is.EqualTo(("The picture's address isn't an https URL", 0)));
    }

    [Test]
    public async Task DownloadAsync_AnAnswerThatIsNotAnImage_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(new Handler(_ => Image([1], "text/html"))).DownloadAsync("https://x/p", default));

        Assert.That(failure!.Message, Is.EqualTo("The picture's address didn't serve an image"));
    }

    [Test]
    public async Task DownloadAsync_AnErrorStatus_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(new Handler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.NotFound))).DownloadAsync("https://x/p", default));

        Assert.That(failure!.Message, Is.EqualTo("The picture's address answered 404"));
    }

    private sealed class UnsizedContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    [Test]
    public async Task DownloadAsync_TooLargeWithoutALength_IsRefusedWhileReading()
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new UnsizedContent(new byte[XboxUserImage.MaxContentBytes + 1]) };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(new Handler(_ => response)).DownloadAsync("https://x/p", default));

        Assert.That(failure!.Message, Is.EqualTo("The picture is too large"));
    }

    [Test]
    public async Task DownloadAsync_TooLarge_IsRefused()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Downloader(new Handler(_ => Image(new byte[XboxUserImage.MaxContentBytes + 1]))).DownloadAsync("https://x/p", default));

        Assert.That(failure!.Message, Is.EqualTo("The picture is too large"));
    }
}
