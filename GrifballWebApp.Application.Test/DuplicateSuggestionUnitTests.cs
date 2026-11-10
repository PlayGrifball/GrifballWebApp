using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using GrifballWebApp.Application.Historical;

namespace GrifballWebApp.Application.Test;

/// <summary>DuplicateSuggestionTests's unit tests: no database, host, file or network. Its other tests stay in
/// GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DuplicateSuggestionUnitTests
{
    private static readonly string[] SpellingsOfOneName = ["Mr_Epic Beast", "mr epic beast#0420", "MrEpicBeast"];
    private static readonly string[] OneLooseKey = ["MREPICBEAST"];
    private static readonly int[] NameLengths = [3, 4, 5, 7, 8, 15];
    private static readonly int[] AllowedEdits = [0, 0, 1, 1, 2, 2];

    [TestCase("Name#1234", "Name")]
    [TestCase("Name #12", "Name")]
    [TestCase("Name", "Name")]
    [TestCase("#123", "#123")]
    public void WithoutSuffix_DropsAModernSuffix(string gamertag, string expected)
        => Assert.That(HistoricalKeys.WithoutSuffix(gamertag), Is.EqualTo(expected));

    [Test]
    public void LooseKey_IgnoresCaseSpacesPunctuationAndSuffix()
        => Assert.That(SpellingsOfOneName.Select(HistoricalKeys.LooseKey).Distinct(), Is.EqualTo(OneLooseKey));

    [TestCase("ABCDEFGH", "ABCDEFGH", 2, 0)]
    [TestCase("ABCDEFGH", "ABXDEFGH", 2, 1)]
    [TestCase("ABCDEFGH", "ABXDEFG", 2, 2)]
    [TestCase("ABCDEFGH", "XYZDEFGH", 2, null)]
    [TestCase("ABC", "ABD", 0, null)]
    [TestCase("ABC", "ABCDEF", 2, null)]
    public void Distance_IsLevenshtein_UpToTheMax(string a, string b, int max, int? expected)
        => Assert.That(DuplicateSuggestionService.Distance(a, b, max), Is.EqualTo(expected));

    [Test]
    public void Allowed_ScalesWithLength()
        => Assert.That(NameLengths.Select(DuplicateSuggestionService.Allowed), Is.EqualTo(AllowedEdits));

    [Test]
    public void PlaceholderGamertag_IsTheUserNamesGamertag()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(DuplicateSuggestionService.PlaceholderGamertag("historical:Old Tag:123", "x"), Is.EqualTo("Old Tag"));
            Assert.That(DuplicateSuggestionService.PlaceholderGamertag("someone", "Shown"), Is.EqualTo("Shown"));
        }
    }

    [TestCase("Xander", "XANDER")]
    [TestCase("Lander", "LANDER")]
    [TestCase("Illusion", "ILLUSION")]
    [TestCase("Xerox", "XEROX")]
    [TestCase("Fox", "FOX")]
    [TestCase("Max", "MAX")]
    [TestCase("Lloyd Hall", "LLOYDHALL")]
    [TestCase("Llama Drill", "LLAMADRILL")]
    [TestCase("x Chief", "XCHIEF")]
    [TestCase("xChiefx", "XCHIEFX")]
    [TestCase("x Chiefx", "XCHIEFX")]
    [TestCase("xX Quillon xX", "XXQUILLONXX")]
    [TestCase("xX Bob Xx", "XXBOBXX")]
    [TestCase("xX Bob Xx#1234", "XXBOBXX")]
    [TestCase("xXxX", "XXXX")]
    [TestCase("____", "")]
    public void StripFraming_KeepsRealNamesAndLookalikeEnds(string gamertag, string expected)
        => Assert.That(HistoricalKeys.StripFraming(gamertag), Is.EqualTo(expected));

    [TestCase("xXXanderXx", "XANDER")]
    [TestCase("llLanderll", "LANDER")]
    [TestCase("xX Master Chief Xx", "MASTERCHIEF")]
    [TestCase("x Master Chief x", "MASTERCHIEF")]
    [TestCase("ii flowers ii", "FLOWERS")]
    [TestCase("l Flowers l", "FLOWERS")]
    [TestCase("oo Sniper oo", "SNIPER")]
    [TestCase("0 Sniper 0", "SNIPER")]
    [TestCase("__xX Chief Xx__", "CHIEF")]
    [TestCase("~~ii Quillon ii~~", "QUILLON")]
    [TestCase("xX Quillon Xx#1234", "QUILLON")]
    public void StripFraming_DropsMirroredFraming(string gamertag, string expected)
        => Assert.That(HistoricalKeys.StripFraming(gamertag), Is.EqualTo(expected));

    [TestCase("Its Rainin Flowerz", new[] { "RAININ", "FLOWERZ" })]
    [TestCase("Rainier117", new[] { "RAINIER117", "RAINIER" })]
    [TestCase("117Rainier", new[] { "117RAINIER", "RAINIER" })]
    [TestCase("XMLParser", new[] { "XMLPARSER", "PARSER" })]
    [TestCase("MasterChief", new[] { "MASTERCHIEF" })]
    [TestCase("Ghost Rider", new string[0])]
    [TestCase("Dark Knight", new string[0])]
    public void ExtractTokens_SplitsWordsBothWaysRoundDigits_AndSkipsShortAndCommonWords(string gamertag, string[] expected)
        => Assert.That(DuplicateSuggestionService.ExtractTokens(gamertag), Is.EquivalentTo(expected));
}
