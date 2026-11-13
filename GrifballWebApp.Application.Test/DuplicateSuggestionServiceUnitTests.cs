using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace GrifballWebApp.Application.Test;

/// <summary>DuplicateSuggestionService's instance methods (List/Merge/Dismiss): its static helpers (LooseKey,
/// StripFraming, Distance, ExtractTokens, ...) are DuplicateSuggestionUnitTests'.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class DuplicateSuggestionServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
    private IUserMergeService _merges = null!;
    private DuplicateSuggestionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _merges = new UserMergeService(Context);
        _service = new DuplicateSuggestionService(Context, _merges, new FakeTimeProvider(Now));
    }

    private User Placeholder(string gamertag)
    {
        var user = User.Seed(userName: "historical:" + gamertag, normalizedUserName: ("historical:" + gamertag).ToUpperInvariant(), displayName: gamertag, isDummyUser: true);
        Context.Users.Add(user);
        return user;
    }

    private User Real(string gamertag, long xuid)
    {
        var user = User.Seed(userName: "u" + xuid, xboxUser: XboxUser.FromXbox(xuid, gamertag));
        Context.Users.Add(user);
        return user;
    }

    [Test]
    public async Task List_SuggestsASuffixedTag_AndATypo_ButNotShortNamesOrTwoAccounts()
    {
        var suffix = Placeholder("Shadow Striker");
        var shadow = Real("Shadow Striker#1234", 1);
        var typo = Placeholder("Grunt Padree");
        var padre = Real("Grunt Padre", 2);
        Placeholder("Abc");
        Real("Abd", 3);
        await Context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.Match, s.Distance)), Is.EquivalentTo(new[]
        {
            (suffix.Id, shadow.Id, DuplicateMatch.Exact, 0),
            (typo.Id, padre.Id, DuplicateMatch.Edits, 1),
        }));
    }

    [Test]
    public async Task List_OfTwoPlaceholders_TheNewerGoesIntoTheOlder()
    {
        var older = Placeholder("Mr Epic Beast");
        await Context.SaveChangesAsync();
        var newer = Placeholder("Mr_Epic_Beast");
        await Context.SaveChangesAsync();

        var suggestion = (await _service.List()).Single();

        Assert.That((suggestion.SourceUserID, suggestion.TargetUserID, suggestion.TargetIsPlaceholder), Is.EqualTo((newer.Id, older.Id, true)));
    }

    [Test]
    public async Task List_ConflictingSeasons_AreNotSuggested()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        var season = Season.Seed(seasonName: "S1").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(1));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var a = Team.Seed(seasonID: season.SeasonID, teamName: "A");
        var b = Team.Seed(seasonID: season.SeasonID, teamName: "B");
        Context.Teams.AddRange(a, b);
        await Context.SaveChangesAsync();
        Context.Set<TeamPlayer>().AddRange(TeamPlayer.Seed(teamID: a.TeamID, userID: placeholder.Id), TeamPlayer.Seed(teamID: b.TeamID, userID: real.Id));
        await Context.SaveChangesAsync();

        Assert.That(await _service.List(), Is.Empty);
    }

    [Test]
    public async Task List_AWordMatch_PairsTwoPlaceholders_TheNewerIntoTheOlder()
    {
        var olderLong = Placeholder("Its Rainin Flowerz");
        await Context.SaveChangesAsync();
        var newerShort = Placeholder("Flowerz");
        await Context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.TargetIsPlaceholder, s.Match, s.Distance)), Is.EquivalentTo(new[]
        {
            (newerShort.Id, olderLong.Id, true, DuplicateMatch.Token, 9),
        }));
    }

    [Test]
    public async Task List_AFramingMatch_KeepsTheNamesOwnLetters_AndMergesIntoTheRealAccount()
    {
        var xander = Placeholder("xXXanderXx");
        var realXander = Real("Xander", 1);
        await Context.SaveChangesAsync();

        var suggestions = await _service.List();

        Assert.That(suggestions.Select(s => (s.SourceUserID, s.TargetUserID, s.TargetIsPlaceholder, s.Match, s.Distance)), Is.EqualTo(new[]
        {
            (xander.Id, realXander.Id, false, DuplicateMatch.Framing, 4),
        }));
    }

    [Test]
    public async Task List_RealNearMisses_RankAheadOfFramingAndWordMatches_AndTheCapLimitsThem()
    {
        var token = Placeholder("Its Rainin Flowerz");
        await Context.SaveChangesAsync();
        var tokenSource = Placeholder("Flowerz");
        var framing = Placeholder("xX Quillon Xx");
        Real("Quillon", 1);
        var typo = Placeholder("Spartann117");
        Real("Spartan117x", 2);
        var exact = Placeholder("Shadow Striker");
        Real("Shadow Striker#1234", 3);
        await Context.SaveChangesAsync();

        var all = await _service.List();
        var two = await _service.List(2);

        using (Assert.EnterMultipleScope())
        {
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

    [Test]
    public async Task List_ADismissedPair_IsNotSuggestedAgain()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();

        await _service.Dismiss(real.Id, placeholder.Id);

        Assert.That(await _service.List(), Is.Empty);
        var dismissal = await Context.DuplicateDismissals.SingleAsync();
        Assert.That((dismissal.LowUserID, dismissal.HighUserID), Is.EqualTo((Math.Min(real.Id, placeholder.Id), Math.Max(real.Id, placeholder.Id))));
    }

    [Test]
    public async Task Dismiss_ASecondTimeForTheSamePair_IsANoOp()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();

        await _service.Dismiss(real.Id, placeholder.Id);
        await _service.Dismiss(placeholder.Id, real.Id);

        Assert.That(await Context.DuplicateDismissals.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Dismiss_AUserWithItself_Throws()
    {
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Dismiss(real.Id, real.Id));
    }

    [Test]
    public async Task Dismiss_KeepsTheSignedInSysadminAndTheTime()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.GetCurrentUserId().Returns(42);
        var service = new DuplicateSuggestionService(Context, _merges, new FakeTimeProvider(Now), currentUser);

        await service.Dismiss(real.Id, placeholder.Id);

        var dismissal = await Context.DuplicateDismissals.SingleAsync(DuplicateDismissal.Of(placeholder.Id, real.Id));
        Assert.That((dismissal.DismissedByUserID, dismissal.DismissedAt), Is.EqualTo(((int?)42, Now.UtcDateTime)));
    }

    [Test]
    public async Task Merge_OnlyFromAPlaceholder_Throws()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Merge(real.Id, placeholder.Id));
    }

    [Test]
    public async Task Merge_NoSuchSource_Throws()
    {
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();

        await Assert.ThrowsAsync<UserNotFoundException>(() => _service.Merge(999, real.Id));
    }

    [Test]
    public async Task Merge_IsAudited_AsADuplicateSuggestionTrigger()
    {
        var placeholder = Placeholder("Shadow Striker");
        var real = Real("Shadow Striker#1234", 1);
        await Context.SaveChangesAsync();

        await _service.Merge(placeholder.Id, real.Id);

        var merge = await Context.UserMerges.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((merge.Trigger, merge.SourceUserID, merge.TargetUserID), Is.EqualTo((MergeTrigger.DuplicateSuggestion, placeholder.Id, real.Id)));
            Assert.That(await Context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.False);
        }
    }
}
