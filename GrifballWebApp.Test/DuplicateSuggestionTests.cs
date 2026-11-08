using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Test;

/// <summary>"Possible duplicate" suggestions on the claims page: close gamertags whose seasons don't conflict, never merged on their own.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DuplicateSuggestionTests
{
    private GrifballContext _context;
    private DuplicateSuggestionService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _service = new DuplicateSuggestionService(_context, new UserMergeService(_context), new FakeTimeProvider());
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private User Placeholder(string gamertag)
    {
        var user = User.Seed(userName: "historical:" + gamertag, normalizedUserName: ("historical:" + gamertag).ToUpperInvariant(), displayName: gamertag, isDummyUser: true);
        _context.Users.Add(user);
        return user;
    }

    private User Real(string gamertag, long xuid)
    {
        var user = User.Seed(userName: "u" + xuid, xboxUser: XboxUser.FromXbox(xuid, gamertag));
        _context.Users.Add(user);
        return user;
    }

    [TestCase("Name#1234", "Name")]
    [TestCase("Name #12", "Name")]
    [TestCase("Name", "Name")]
    [TestCase("#123", "#123")]
    public void WithoutSuffix_DropsAModernSuffix(string gamertag, string expected)
        => Assert.That(HistoricalKeys.WithoutSuffix(gamertag), Is.EqualTo(expected));

    [Test]
    public void LooseKey_IgnoresCaseSpacesPunctuationAndSuffix()
        => Assert.That(new[] { "Mr_Epic Beast", "mr epic beast#0420", "MrEpicBeast" }.Select(HistoricalKeys.LooseKey).Distinct(), Is.EqualTo(new[] { "MREPICBEAST" }));

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
        => Assert.That(new[] { 3, 4, 5, 7, 8, 15 }.Select(DuplicateSuggestionService.Allowed), Is.EqualTo(new[] { 0, 0, 1, 1, 2, 2 }));

    [Test]
    public void PlaceholderGamertag_IsTheUserNamesGamertag()
    {
        Assert.That(DuplicateSuggestionService.PlaceholderGamertag("historical:Old Tag:123", "x"), Is.EqualTo("Old Tag"));
        Assert.That(DuplicateSuggestionService.PlaceholderGamertag("someone", "Shown"), Is.EqualTo("Shown"));
    }

    [Test]
    public async Task PlaceholderAsync_IsSuffixAware()
    {
        var plain = Placeholder("Shadow");
        var suffixed = Placeholder("Pie#0420");
        await _context.SaveChangesAsync();
        var matching = new GamertagMatching(_context);

        Assert.That((await matching.PlaceholderAsync("Shadow#1234", untiedOnly: true, CancellationToken.None))?.Id, Is.EqualTo(plain.Id));
        Assert.That((await matching.PlaceholderAsync("Pie", untiedOnly: true, CancellationToken.None))?.Id, Is.EqualTo(suffixed.Id));
        Assert.That(await matching.PlaceholderAsync("Nobody#1", untiedOnly: true, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Suggests_ASuffixedTag_AndATypo_ButNotShortNamesOrTwoAccounts()
    {
        var suffix = Placeholder("Shadow Striker");
        var shadow = Real("Shadow Striker#1234", 1);
        var typo = Placeholder("Grunt Padree");
        var padre = Real("Grunt Padre", 2);
        Placeholder("Abc");
        Real("Abd", 3);
        // Two Xbox accounts are two players
        _context.Users.Add(User.Seed(userName: "historical:Twin Tag:4", isDummyUser: true, xboxUser: XboxUser.FromXbox(4, "Twin Tag")));
        Real("Twin Tagg", 5);
        await _context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.Match, s.Distance)), Is.EquivalentTo(new[]
        {
            (suffix.Id, shadow.Id, DuplicateMatch.Exact, 0),
            (typo.Id, padre.Id, DuplicateMatch.Edits, 1),
        }));
    }

    [Test]
    public async Task OfTwoPlaceholders_TheNewerGoesIntoTheOlder()
    {
        var older = Placeholder("Mr Epic Beast");
        await _context.SaveChangesAsync();
        var newer = Placeholder("Mr_Epic_Beast");
        await _context.SaveChangesAsync();

        var suggestion = (await _service.List()).Single();

        Assert.That((suggestion.SourceUserID, suggestion.TargetUserID, suggestion.TargetIsPlaceholder), Is.EqualTo((newer.Id, older.Id, true)));
    }

    [Test]
    public async Task ConflictingSeasons_AreNotSuggested()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        var season = Season.Seed(seasonName: "S1").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(1));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var a = new Team { SeasonID = season.SeasonID, TeamName = "A" };
        var b = new Team { SeasonID = season.SeasonID, TeamName = "B" };
        _context.Teams.AddRange(a, b);
        await _context.SaveChangesAsync();
        _context.TeamPlayers.AddRange(new TeamPlayer { TeamID = a.TeamID, UserID = placeholder.Id }, new TeamPlayer { TeamID = b.TeamID, UserID = real.Id });
        await _context.SaveChangesAsync();

        Assert.That(await _service.List(), Is.Empty);
    }

    [Test]
    public async Task ADismissedPair_IsNotSuggestedAgain()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await _context.SaveChangesAsync();

        await _service.Dismiss(real.Id, placeholder.Id);
        await _service.Dismiss(placeholder.Id, real.Id);

        Assert.That(await _service.List(), Is.Empty);
        var dismissal = await _context.DuplicateDismissals.SingleAsync();
        Assert.That((dismissal.LowUserID, dismissal.HighUserID), Is.EqualTo((Math.Min(real.Id, placeholder.Id), Math.Max(real.Id, placeholder.Id))));
        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Dismiss(real.Id, real.Id));
    }

    [Test]
    public async Task Merge_IsAudited_AndOnlyFromAPlaceholder()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Merge(real.Id, placeholder.Id));
        await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(999, real.Id));
        await _service.Merge(placeholder.Id, real.Id);

        var merge = await _context.UserMerges.SingleAsync();
        Assert.That((merge.Trigger, merge.SourceUserID, merge.TargetUserID), Is.EqualTo((MergeTrigger.DuplicateSuggestion, placeholder.Id, real.Id)));
    }

    [Test]
    public async Task Controller_ListsMergesAndDismisses()
    {
        var one = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        var two = Placeholder("Grunt Padree");
        var padre = Real("Grunt Padre", 2);
        await _context.SaveChangesAsync();
        var controller = new GamertagClaimsController(new GamertagClaimService(_context, new UserMergeService(_context)),
            new XuidLookupService(_context, NSubstitute.Substitute.For<Surprenant.Grunt.Core.IHaloInfiniteClient>(), new GamertagMatching(_context), Options.Create(new XuidLookupOptions()), TimeProvider.System),
            new XuidLookupSignal(), _service);

        var listed = await controller.DuplicateSuggestions(50, CancellationToken.None);
        var merged = await controller.MergeDuplicate(new DuplicatePairRequest { SourceUserID = one.Id, TargetUserID = real.Id }, CancellationToken.None);
        var dismissed = await controller.DismissDuplicate(new DuplicatePairRequest { SourceUserID = two.Id, TargetUserID = padre.Id }, CancellationToken.None);

        Assert.That(listed, Has.Count.EqualTo(2));
        Assert.That(merged, Is.TypeOf<NoContentResult>());
        Assert.That(dismissed, Is.TypeOf<NoContentResult>());
        Assert.That(await controller.DuplicateSuggestions(50, CancellationToken.None), Is.Empty);
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

    [Test]
    public async Task ACommonOrShortWord_NeverPairsAPlaceholderWithAnotherGamertag()
    {
        Placeholder("Ghost");
        Real("Ghost Rider", 1);
        Real("Pale Ghost", 2);
        Placeholder("Ghost Of Sparta");
        Placeholder("Dark");
        Placeholder("Dark Knight");
        Real("Dark Angel", 3);
        Placeholder("Shadow");
        Placeholder("Pale Shadow");
        Placeholder("Sniper");
        Placeholder("Sniper Wolf");
        Placeholder("Spartan");
        Placeholder("117Spartan");
        Placeholder("Spartan117");
        // An uncommon word, but the other gamertag is a real account
        Placeholder("Quillon");
        Real("Quillon Rex", 4);
        // A word several gamertags share says nothing about which one it was
        Placeholder("Zephyr");
        Placeholder("Zephyr Hunter");
        Placeholder("Pale Zephyr");
        // Placeholders tied to an Xbox account are real accounts too, on either side
        _context.Users.Add(User.Seed(userName: "historical:Quasarix:5", isDummyUser: true, xboxUser: XboxUser.FromXbox(5, "Quasarix")));
        Placeholder("Its Quasarix Time");
        Placeholder("Nebulon");
        _context.Users.Add(User.Seed(userName: "historical:Nebulon Kid:6", isDummyUser: true, xboxUser: XboxUser.FromXbox(6, "Nebulon Kid")));
        await _context.SaveChangesAsync();

        Assert.That((await _service.List()).Select(s => (s.SourceGamertag, s.TargetGamertag)), Is.Empty);
    }

    [Test]
    public async Task AWordMatch_PairsTwoPlaceholders_TheNewerIntoTheOlder()
    {
        var olderLong = Placeholder("Its Rainin Flowerz");
        await _context.SaveChangesAsync();
        var newerShort = Placeholder("Flowerz");
        var olderShort = Placeholder("Marblehead");
        await _context.SaveChangesAsync();
        var newerLong = Placeholder("Big Marblehead");
        await _context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.TargetIsPlaceholder, s.Match, s.Distance)), Is.EquivalentTo(new[]
        {
            (newerShort.Id, olderLong.Id, true, DuplicateMatch.Token, 9),
            (newerLong.Id, olderShort.Id, true, DuplicateMatch.Token, 3),
        }));
    }

    [Test]
    public async Task AFramingMatch_KeepsTheNamesOwnLetters_AndMergesIntoTheRealAccount()
    {
        var xander = Placeholder("xXXanderXx");
        var lander = Placeholder("llLanderll");
        var realXander = Real("Xander", 1);
        var realLander = Real("Lander", 2);
        var quillon = Placeholder("__xX Quillon Xx__");
        var realQuillon = Real("Quillon", 3);
        await _context.SaveChangesAsync();
        var olderFlowers = Placeholder("ii flowers ii");
        await _context.SaveChangesAsync();
        var newerFlowers = Placeholder("l Flowers l");
        await _context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.TargetIsPlaceholder, s.Match, s.Distance)), Is.EquivalentTo(new[]
        {
            (xander.Id, realXander.Id, false, DuplicateMatch.Framing, 4),
            (lander.Id, realLander.Id, false, DuplicateMatch.Framing, 4),
            (quillon.Id, realQuillon.Id, false, DuplicateMatch.Framing, 4),
            (newerFlowers.Id, olderFlowers.Id, true, DuplicateMatch.Framing, 4),
        }));
    }

    [Test]
    public async Task RealNearMisses_RankAheadOfFramingAndWordMatches()
    {
        var token = Placeholder("Its Rainin Flowerz");
        await _context.SaveChangesAsync();
        var tokenSource = Placeholder("Flowerz");
        var framing = Placeholder("xX Quillon Xx");
        Real("Quillon", 1);
        var typo = Placeholder("Spartann117");
        Real("Spartan117x", 2);
        var exact = Placeholder("Shadow Striker");
        Real("Shadow Striker#1234", 3);
        await _context.SaveChangesAsync();

        var all = await _service.List();
        var two = await _service.List(2);

        Assert.That(all.Select(s => (s.SourceUserID, s.Match, s.Distance)), Is.EqualTo(new[]
        {
            (exact.Id, DuplicateMatch.Exact, 0),
            (typo.Id, DuplicateMatch.Edits, 2),
            (framing.Id, DuplicateMatch.Framing, 4),
            (tokenSource.Id, DuplicateMatch.Token, 9),
        }));
        Assert.That(all.Last().TargetUserID, Is.EqualTo(token.Id));
        Assert.That(two.Select(s => s.SourceUserID), Is.EqualTo(new[] { exact.Id, typo.Id }));
    }
}
