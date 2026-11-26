using GrifballWebApp.Application.Spartan;

namespace GrifballWebApp.Application.Test;

/// <summary>A player's Spartan is accepted only when the render service's catalog lists everything it names.</summary>
[TestFixture]
public class SpartanChoiceValidatorTests
{
    private static readonly SpartanCatalog Catalog = new(
        [new SpartanCatalogCore("Mark VII",
        [
            new SpartanCatalogRegion("REGION", "helmet", 100, [new(1, "Mark VII"), new(2, "Eaglestrike")]),
            new SpartanCatalogRegion("KIT", "kit", 200, [new(7, null)]),
        ])],
        ["111", "222"], ["Plain"], ["Chimera"]);

    private static IReadOnlyList<string> Check(SpartanChoice choice) => SpartanChoiceValidator.Validate(choice, Catalog);

    [Test]
    public void ACoreAlone_IsAccepted() => Assert.That(Check(new SpartanChoice("Mark VII")), Is.Empty);

    [Test]
    public void EverythingListed_IsAccepted() => Assert.That(Check(new SpartanChoice("Mark VII",
        new Dictionary<string, string> { ["helmet"] = "Eaglestrike" }, [new(200, 7)], "222", "Plain")), Is.Empty);

    [Test]
    public void AnUnknownCore_IsRefused_AndNothingElseIsChecked()
        => Assert.That(Check(new SpartanChoice("Nope", new Dictionary<string, string> { ["x"] = "y" })),
            Is.EqualTo(new List<string> { "There is no core 'Nope'" }));

    [Test]
    public void ACoreStillImporting_IsToldToWait()
        => Assert.That(Check(new SpartanChoice("Chimera")).Single(), Does.Contain("still being prepared"));

    [Test]
    public void AnUnknownRegion_AndAnUnknownPiece_AreEachNamed()
    {
        var problems = Check(new SpartanChoice("Mark VII", new Dictionary<string, string> { ["knee"] = "A", ["helmet"] = "B" }));
        Assert.That(problems, Is.EquivalentTo(new List<string>
        {
            "The core 'Mark VII' has no region 'knee'",
            "The region 'helmet' of 'Mark VII' has no piece 'B'",
        }));
    }

    [Test]
    public void AKitRegion_IsNotReachableByName()
        => Assert.That(Check(new SpartanChoice("Mark VII", new Dictionary<string, string> { ["kit"] = "A" })).Single(),
            Does.Contain("no region 'kit'"));

    [Test]
    public void APartMustExist_AndARegionMayBeGivenOnce()
    {
        var problems = Check(new SpartanChoice("Mark VII", null, [new(200, 9), new(100, 1), new(100, 2)]));
        Assert.That(problems, Is.EquivalentTo(new List<string>
        {
            "The core 'Mark VII' has no piece 9 in the region 200",
            "The region 100 is given more than once in the parts",
        }));
    }

    [Test]
    public void AnUnknownCoatingAndVisor_AreNamed()
        => Assert.That(Check(new SpartanChoice("Mark VII", Coating: "9", Visor: "Shiny")), Is.EquivalentTo(new List<string>
        {
            "There is no coating '9'",
            "There is no visor 'Shiny'",
        }));
}
