using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.Spartan;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Surprenant.Grunt.Core;
using Surprenant.Grunt.Models;
using Surprenant.Grunt.Models.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>What an Xbox account has equipped (Halo's item paths), turned into a Spartan the render service can render.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SpartanArmorMapperTests
{
    private static readonly SpartanCatalog Catalog = new(
    [
        new SpartanCatalogCore("Mark VII",
        [
            new("REGION", "helmet", 1, [new(1, "Aviator"), new(2, "Eaglestrike")]),
            new("REGION", "shoulder_left", 2, [new(1, "Twin"), new(2, "Solo")]),
            new("REGION", "shoulder_right", 3, [new(1, "Twin"), new(2, "Solo")]),
            new("REGION", "glove", 4, [new(1, "Grip")]),
            new("KIT", "kit", 5, [new(9, null)]),
        ]),
    ], ["111"], [], ["Reflection"]);

    private readonly IHaloInfiniteClient _halo = Substitute.For<IHaloInfiniteClient>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly List<string> _asked = [];
    private readonly List<TimeSpan> _waits = [];

    [TearDown]
    public void DisposeCache() => _cache.Dispose();

    private SpartanArmorMapper Mapper() => new(_halo, _cache)
    {
        Delay = (wait, _) =>
        {
            _waits.Add(wait);
            return Task.CompletedTask;
        },
    };

    private static HaloApiResultContainer<InGameItem, HaloApiErrorContainer> Found(string? title = null, int? style = null, int? stance = null) => new(
        new InGameItem
        {
            CommonData = new CommonItemData { Title = new DisplayString { Value = title! } },
            StyleId = style is { } s ? new IdentifierName { MIdentifier = s } : null!,
            StanceId = stance is { } t ? new IdentifierName { MIdentifier = t } : null!,
        }, null!);

    private static HaloApiResultContainer<InGameItem, HaloApiErrorContainer> Failed(int code, TimeSpan? retryAfter = null)
        => new(null!, new HaloApiErrorContainer { Code = code, RetryAfter = retryAfter });

    private void Serve(string path, HaloApiResultContainer<InGameItem, HaloApiErrorContainer> reply)
        => _halo.GameCmsGetItem(path, string.Empty).Returns(call =>
        {
            _asked.Add(call.ArgAt<string>(0));
            return reply;
        });

    private static XboxUserArmor Armor(Action<XboxUserArmor>? change = null)
    {
        var armor = new XboxUserArmor { CorePath = "cores/mk7.json" };
        change?.Invoke(armor);
        return armor;
    }

    [Test]
    public async Task ACoreTheCatalogHas_IsFoundByItsTitle_IgnoringCase_AndNamedAsTheCatalogNamesIt()
    {
        Serve("cores/mk7.json", Found(" mark vii "));

        var mapped = await Mapper().Map(Armor(), Catalog, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mapped!.Choice.Core, Is.EqualTo("Mark VII"));
            Assert.That(mapped.Choice.Armor, Is.Empty);
            Assert.That((mapped.Choice.Coating, mapped.Stance), Is.EqualTo(((string?)null, (int?)null)));
        }
    }

    [TestCase(null)]
    [TestCase("  ")]
    public async Task AnArmorWithNoCorePath_IsNotMapped_AndAsksHaloNothing(string? path)
    {
        var mapped = await Mapper().Map(Armor(a => a.CorePath = path), Catalog, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(mapped, Is.Null);
            Assert.That(_asked, Is.Empty);
        }
    }

    [Test]
    public async Task ACoreTheCatalogDoesNotHave_IsNotMapped()
    {
        Serve("cores/mk7.json", Found("Yoroi"));

        Assert.That(await Mapper().Map(Armor(), Catalog, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ACoreWithABlankTitle_IsNotMapped()
    {
        Serve("cores/mk7.json", Found("   "));

        Assert.That(await Mapper().Map(Armor(), Catalog, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task EachSlotsPiece_GoesToTheRegionThatHasAPieceOfThatTitle_AndSharedTitlesAreNarrowedByTheSlot()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("helmets/h.json", Found("Eaglestrike"));
        Serve("gloves/g.json", Found("Grip"));
        Serve("shoulders/l.json", Found("Twin"));
        Serve("shoulders/r.json", Found("Solo"));
        Serve("knees/k.json", Found("Nothing like it"));

        var mapped = await Mapper().Map(Armor(a =>
        {
            a.HelmetPath = "helmets/h.json";
            a.GlovePath = "gloves/g.json";
            a.LeftShoulderPadPath = "shoulders/l.json";
            a.RightShoulderPadPath = "shoulders/r.json";
            a.KneePadPath = "knees/k.json";
        }), Catalog, CancellationToken.None);

        Assert.That(mapped!.Choice.Armor, Is.EqualTo(new Dictionary<string, string>
        {
            ["helmet"] = "Eaglestrike",
            ["glove"] = "Grip",
            ["shoulder_left"] = "Twin",
            ["shoulder_right"] = "Solo",
        }));
    }

    [Test]
    public async Task APieceTitledForBothShoulders_GoesToTheShoulderOfItsSlot()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("shoulders/l.json", Found("Twin"));

        var mapped = await Mapper().Map(Armor(a => a.LeftShoulderPadPath = "shoulders/l.json"), Catalog, CancellationToken.None);

        Assert.That(mapped!.Choice.Armor, Is.EqualTo(new Dictionary<string, string> { ["shoulder_left"] = "Twin" }));
    }

    [Test]
    public async Task ACoatingInTheCatalog_ByItsStyleId_AndTheStanceOfTheActionPose_AreKept()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("coatings/c.json", Found(style: 111));
        Serve("poses/p.json", Found(stance: 39388885));

        var mapped = await Mapper().Map(Armor(a =>
        {
            a.CoatingPath = "coatings/c.json";
            a.ActionPosePath = "poses/p.json";
        }), Catalog, CancellationToken.None);

        Assert.That((mapped!.Choice.Coating, mapped.Stance), Is.EqualTo(("111", (int?)39388885)));
    }

    [Test]
    public async Task APiece_IsStoredAsTheCatalogNamesIt_NotAsHaloCasesIt()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("helmets/a.json", Found("AVIATOR"));

        var mapped = await Mapper().Map(Armor(a => a.HelmetPath = "helmets/a.json"), Catalog, CancellationToken.None);

        Assert.That(mapped!.Choice.Armor!["helmet"], Is.EqualTo("Aviator"));
    }

    [Test]
    public async Task AVisorInTheCatalog_ByItsTitle_IsKept_AndOneItDoesNotHaveIsLeftOut()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("visors/known.json", Found("reflection"));
        Serve("visors/other.json", Found("Mirage"));

        var known = await Mapper().Map(Armor(a => a.VisorPath = "visors/known.json"), Catalog, CancellationToken.None);
        var other = await Mapper().Map(Armor(a => a.VisorPath = "visors/other.json"), Catalog, CancellationToken.None);

        Assert.That((known!.Choice.Visor, other!.Choice.Visor), Is.EqualTo(("Reflection", (string?)null)));
    }

    [Test]
    public async Task ACoatingTheCatalogDoesNotHave_AndAStanceOfZero_AreLeftOut()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("coatings/c.json", Found(style: 999));
        Serve("poses/p.json", Found(stance: 0));

        var mapped = await Mapper().Map(Armor(a =>
        {
            a.CoatingPath = "coatings/c.json";
            a.ActionPosePath = "poses/p.json";
        }), Catalog, CancellationToken.None);

        Assert.That((mapped!.Choice.Coating, mapped.Stance), Is.EqualTo(((string?)null, (int?)null)));
    }

    [Test]
    public async Task AnItem_IsAskedForOnce_WhileItIsKept_WithItsPathTrimmed_AndWaitsHalfASecondBetweenCalls()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        var mapper = Mapper();

        await mapper.Map(Armor(a => a.CorePath = " /cores/mk7.json "), Catalog, CancellationToken.None);
        await mapper.Map(Armor(), Catalog, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(_asked, Is.EqualTo(new List<string> { "cores/mk7.json" }));
            Assert.That(_waits, Is.EqualTo(new List<TimeSpan> { TimeSpan.FromMilliseconds(500) }));
        }
    }

    [Test]
    public async Task AnItemHaloDoesNotHave_Is404_IsNotAProblem_AndIsRememberedToo()
    {
        _halo.GameCmsGetItem("cores/mk7.json", string.Empty).Returns(call =>
        {
            _asked.Add(call.ArgAt<string>(0));
            return Failed(404);
        });
        var mapper = Mapper();

        var first = await mapper.Map(Armor(), Catalog, CancellationToken.None);
        var second = await mapper.Map(Armor(), Catalog, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((first, second), Is.EqualTo(((MappedSpartan?)null, (MappedSpartan?)null)));
            Assert.That(_asked, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task HaloThrottling_Throws_WithTheWaitItAskedFor_OrAMinute()
    {
        _halo.GameCmsGetItem("cores/mk7.json", string.Empty).Returns(Failed(429, TimeSpan.FromSeconds(90)));
        _halo.GameCmsGetItem("cores/other.json", string.Empty).Returns(Failed(429));

        var asked = await Assert.ThrowsAsync<HaloAnswerException>(() => Mapper().Map(Armor(), Catalog, CancellationToken.None));
        var none = await Assert.ThrowsAsync<HaloAnswerException>(() => Mapper().Map(Armor(a => a.CorePath = "cores/other.json"), Catalog, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(asked.Message, Is.EqualTo("Halo is throttling (wait 00:01:30)"));
            Assert.That(none.Message, Is.EqualTo("Halo is throttling (wait 00:01:00)"));
            Assert.That((asked.Wait, none.Wait), Is.EqualTo(((TimeSpan?)TimeSpan.FromSeconds(90), (TimeSpan?)TimeSpan.FromMinutes(1))));
        }
    }

    [Test]
    public async Task HaloAnsweringWithAnotherError_OrNothing_Throws_AndIsNotRemembered()
    {
        _halo.GameCmsGetItem("cores/mk7.json", string.Empty).Returns(Failed(500));
        _halo.GameCmsGetItem("cores/none.json", string.Empty).Returns(Failed(0));

        var error = await Assert.ThrowsAsync<HaloAnswerException>(() => Mapper().Map(Armor(), Catalog, CancellationToken.None));
        var nothing = await Assert.ThrowsAsync<HaloAnswerException>(() => Mapper().Map(Armor(a => a.CorePath = "cores/none.json"), Catalog, CancellationToken.None));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Message, Is.EqualTo("Halo answered 500 for cores/mk7.json"));
            Assert.That(nothing.Message, Is.EqualTo("Halo gave no answer for cores/none.json"));
            Assert.That(_cache.TryGetValue("spartan-item:cores/mk7.json", out _), Is.False);
        }
    }

    [Test]
    public async Task APieceThatFitsOnlyAnotherSlotsRegion_DoesNotReplaceThePieceThatSlotFilled()
    {
        Serve("cores/mk7.json", Found("Mark VII"));
        Serve("h.json", Found("Aviator"));
        Serve("a.json", Found("Eaglestrike"));

        var mapped = await Mapper().Map(Armor(a =>
        {
            a.HelmetPath = "h.json";
            a.HelmetAttachmentPath = "a.json";
        }), Catalog, CancellationToken.None);

        Assert.That(mapped!.Choice.Armor, Is.EqualTo(new Dictionary<string, string> { ["helmet"] = "Aviator" }));
    }

    [Test]
    public async Task TheStanceOfAnArmor_IsThatOfItsPose_OrNone()
    {
        Serve("p.json", Found(stance: 12));

        var stance = await Mapper().StanceOf(Armor(a => a.ActionPosePath = "p.json"), CancellationToken.None);
        var none = await Mapper().StanceOf(Armor(), CancellationToken.None);

        Assert.That((stance, none), Is.EqualTo(((int?)12, (int?)null)));
    }

    private static readonly SpartanCatalogCore TwoHelmets = new("C",
    [
        new("REGION", "helmet", 1, [new(1, "Same")]),
        new("REGION", "helmet_attach", 2, [new(1, "Same")]),
        new("REGION", "chest", 3, [new(1, "Same")]),
        new("KIT", "kit", 4, [new(1, "Same")]),
    ]);

    [Test]
    public void RegionsOf_NarrowsSeveralRegionsByTheMostHintsThatStillMatch()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(SpartanArmorMapper.RegionsOf(TwoHelmets, "Same", ["helmet", "attach"]), Is.EqualTo(new List<string> { "helmet_attach" }));
            Assert.That(SpartanArmorMapper.RegionsOf(TwoHelmets, "Same", ["helmet"]), Is.EqualTo(new List<string> { "helmet", "helmet_attach" }));
            Assert.That(SpartanArmorMapper.RegionsOf(TwoHelmets, "Same", ["wrist"]), Is.Empty, "a guess between regions is worse than the default piece");
            Assert.That(SpartanArmorMapper.RegionsOf(TwoHelmets, "Absent", ["helmet"]), Is.Empty);
        }
    }
}
