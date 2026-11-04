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
        var user = new User { UserName = "historical:" + gamertag, NormalizedUserName = ("historical:" + gamertag).ToUpperInvariant(), DisplayName = gamertag, IsDummyUser = true };
        _context.Users.Add(user);
        return user;
    }

    private User Real(string gamertag, long xuid)
    {
        var user = new User { UserName = "u" + xuid, XboxUser = new XboxUser { XboxUserID = xuid, Gamertag = gamertag } };
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
        _context.Users.Add(new User { UserName = "historical:Twin Tag:4", IsDummyUser = true, XboxUser = new XboxUser { XboxUserID = 4, Gamertag = "Twin Tag" } });
        Real("Twin Tagg", 5);
        await _context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.Distance)), Is.EquivalentTo(new[]
        {
            (suffix.Id, shadow.Id, 0),
            (typo.Id, padre.Id, 1),
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
        var season = new Season { SeasonName = "S1", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(1) };
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

    [Test]
    public void HistoricalKeys_StripFraming_PreservesRealNamesAndSymmetricFraming()
    {
        Assert.Multiple(() =>
        {
            // Real names must NOT have their letters stripped
            Assert.That(HistoricalKeys.StripFraming("Xander"), Is.EqualTo("XANDER"));
            Assert.That(HistoricalKeys.StripFraming("Lander"), Is.EqualTo("LANDER"));
            Assert.That(HistoricalKeys.StripFraming("Illusion"), Is.EqualTo("ILLUSION"));
            Assert.That(HistoricalKeys.StripFraming("Xerox"), Is.EqualTo("XEROX"));
            Assert.That(HistoricalKeys.StripFraming("Fox"), Is.EqualTo("FOX"));
            Assert.That(HistoricalKeys.StripFraming("Max"), Is.EqualTo("MAX"));

            // Symmetric framing must be stripped
            Assert.That(HistoricalKeys.StripFraming("xX Master Chief Xx"), Is.EqualTo("MASTERCHIEF"));
            Assert.That(HistoricalKeys.StripFraming("x Master Chief x"), Is.EqualTo("MASTERCHIEF"));
            Assert.That(HistoricalKeys.StripFraming("ii flowers ii"), Is.EqualTo("FLOWERS"));
            Assert.That(HistoricalKeys.StripFraming("l Flowers l"), Is.EqualTo("FLOWERS"));
            Assert.That(HistoricalKeys.StripFraming("__Ghost__"), Is.EqualTo("GHOST"));
            Assert.That(HistoricalKeys.StripFraming("--Spartan--"), Is.EqualTo("SPARTAN"));
            Assert.That(HistoricalKeys.StripFraming("oo Sniper oo"), Is.EqualTo("SNIPER"));
            Assert.That(HistoricalKeys.StripFraming("0 Sniper 0"), Is.EqualTo("SNIPER"));
        });
    }

    [Test]
    public async Task Suggests_TokenBoundedMatches_AndFramingVariants_WithoutStopWordFalsePositives()
    {
        // 1. Token-bounded anchor match: "MenZ" <=> "Its Rainin MenZ"
        var menz = Placeholder("MenZ");
        await _context.SaveChangesAsync();
        var rainin = Placeholder("Its Rainin MenZ");
        await _context.SaveChangesAsync();

        // 2. Framing match: "ii flowers ii" <=> "l Flowers l"
        var flowers1 = Placeholder("ii flowers ii");
        await _context.SaveChangesAsync();
        var flowers2 = Placeholder("l Flowers l");
        await _context.SaveChangesAsync();

        // 3. Stop word protection: "King" vs "King Rick" must NOT match
        Placeholder("King");
        Placeholder("King Rick");
        await _context.SaveChangesAsync();

        // 4. Real names preservation: "Illusion" vs "Usion" must NOT match (old framing strip ate "Ill" -> "USION")
        Placeholder("Illusion");
        Placeholder("Usion");
        await _context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.Multiple(() =>
        {
            // Menz token match present
            Assert.That(suggestions.Any(s =>
                (s.SourceGamertag == "Its Rainin MenZ" && s.TargetGamertag == "MenZ") ||
                (s.SourceGamertag == "MenZ" && s.TargetGamertag == "Its Rainin MenZ")), Is.True);

            // Flowers framing match present
            Assert.That(suggestions.Any(s =>
                (s.SourceGamertag == "l Flowers l" && s.TargetGamertag == "ii flowers ii") ||
                (s.SourceGamertag == "ii flowers ii" && s.TargetGamertag == "l Flowers l")), Is.True);

            // King stop word NOT matched
            Assert.That(suggestions.Any(s =>
                s.SourceGamertag.Contains("King") && s.TargetGamertag.Contains("King")), Is.False);

            // Real names NOT matched via framing stripping
            Assert.That(suggestions.Any(s =>
                (s.SourceGamertag == "Illusion" && s.TargetGamertag == "Usion") ||
                (s.SourceGamertag == "Usion" && s.TargetGamertag == "Illusion")), Is.False);
        });
    }
}
