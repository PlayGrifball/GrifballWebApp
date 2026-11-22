using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.HaloInfinite;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// CustomScoreLayouts.DecodeAsync and VariantLayoutsAsync, on a real (temp-file SQLite) GrifballContext: a Plain
/// match (every Score 20 or less, which the stats always read as goals) needs no game variant to decode, and a game
/// variant version's layout is read from its own matches. CustomScoreLayouts.Apply (the pure decoder itself) is
/// already exercised through DataPullService's saves; this is only the EF side around it.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class CustomScoreLayoutsDecodeUnitTests : SqliteContextFixture
{
    private CustomScoreLayouts _layouts = null!;

    [SetUp]
    public void Setup()
    {
        _layouts = new CustomScoreLayouts(Context);
    }

    private long _nextXuid;

    private async Task<Match> SeedMatch(Guid matchId, int scoreA, int scoreB, Guid? gameVariantVersion = null)
    {
        var match = Match.Seed(matchId, ugcGameVariantVersionID: gameVariantVersion, ugcGameVariantAssetID: gameVariantVersion is null ? null : Guid.NewGuid());
        match.AddTeam(0, scoreA, Outcomes.Won);
        match.AddTeam(1, scoreB, Outcomes.Lost);
        var xuidA = ++_nextXuid;
        var xuidB = ++_nextXuid;
        match.AddParticipant(0, xuidA, new MatchParticipantStats { Score = scoreA }, [], XboxUser.FromXbox(xuidA, "A"));
        match.AddParticipant(1, xuidB, new MatchParticipantStats { Score = scoreB }, [], XboxUser.FromXbox(xuidB, "B"));
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return match;
    }

    [Test]
    public async Task DecodeAsync_NoUndecodedMatches_ReturnsZero()
        => Assert.That(await _layouts.DecodeAsync([]), Is.Zero);

    [Test]
    public async Task DecodeAsync_APlainMatch_DecodesGoalsFromScores_WithNoGameVariantNeeded()
    {
        var matchId = Guid.NewGuid();
        await SeedMatch(matchId, 5, 3);

        var done = 0;
        var changed = await _layouts.DecodeAsync([], (d, _) => done = d);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(done, Is.EqualTo(1));
        }
        var saved = await Context.Matches.AsNoTracking().Include(m => m.MatchTeams).ThenInclude(t => t.MatchParticipants)
            .SingleAsync(m => m.MatchID == matchId);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(saved.ScoreLayout, Is.EqualTo(CustomScoreLayout.Plain));
            Assert.That(saved.MatchTeams.SelectMany(t => t.MatchParticipants).Select(p => p.Goals), Is.EquivalentTo(new int?[] { 5, 3 }));
        }
    }

    [Test]
    public async Task DecodeAsync_ConstructedWithAnExplicitStatFlagService_UsesItRatherThanMakingItsOwn()
    {
        // #548: MatchVariantBackfillJob passes its own StatFlagService through rather than let one be made per decode
        var layouts = new CustomScoreLayouts(Context, new StatFlagService(Context));

        var changed = await layouts.DecodeAsync([]);

        Assert.That(changed, Is.Zero);
    }

    [Test]
    public async Task DecodeAsync_AMatchThatStaysUndecoded_FlagsTheUndecodedCustomScore()
    {
        // #548: re-decoding must re-run the stat flag check, the same as the first save does
        // SL26-era numbers whose goals add up under no layout (CustomScoreDecoderTests.NoLayoutFits_ItsUndecoded_NoGoalsCounted)
        var matchId = Guid.NewGuid();
        var match = Match.Seed(matchId);
        match.AddTeam(0, 5, Outcomes.Won);
        match.AddTeam(1, 2, Outcomes.Lost);
        var x1 = ++_nextXuid;
        var x2 = ++_nextXuid;
        var x3 = ++_nextXuid;
        var x4 = ++_nextXuid;
        match.AddParticipant(0, x1, new MatchParticipantStats { Score = 3000000, PersonalScore = 15010100 }, [], XboxUser.FromXbox(x1, "A"));
        match.AddParticipant(0, x2, new MatchParticipantStats { Score = 3000000, PersonalScore = 19030200 }, [], XboxUser.FromXbox(x2, "B"));
        match.AddParticipant(1, x3, new MatchParticipantStats { Score = 2000000, PersonalScore = 18020000 }, [], XboxUser.FromXbox(x3, "C"));
        match.AddParticipant(1, x4, new MatchParticipantStats { Score = 5000000, PersonalScore = 53110300 }, [], XboxUser.FromXbox(x4, "D"));
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var changed = await _layouts.DecodeAsync([]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.EqualTo(1));
            var flag = await Context.Set<StatFlag>().AsNoTracking().SingleAsync(f => f.MatchID == matchId);
            Assert.That(flag.Rule, Is.EqualTo(StatFlagRule.UndecodedCustomScore));
        }
    }

    [Test]
    public async Task DecodeAsync_ADecodedMatch_IsLeftAlone_UnlessItsVariantChanged()
    {
        var matchId = Guid.NewGuid();
        await SeedMatch(matchId, 5, 3);
        await _layouts.DecodeAsync([]);

        var changed = await _layouts.DecodeAsync([]);

        Assert.That(changed, Is.Zero, "Already decoded, and its variant (none) didn't change");
    }

    [Test]
    public async Task VariantLayoutsAsync_TwoMatchesOfAVersion_PicksPlainWhenBothFitIt()
    {
        var version = Guid.NewGuid();
        await SeedMatch(Guid.NewGuid(), 5, 3, version);
        await SeedMatch(Guid.NewGuid(), 2, 1, version);

        var layouts = await _layouts.VariantLayoutsAsync([version]);

        Assert.That(layouts[version], Is.EqualTo(CustomScoreLayout.Plain));
    }

    [Test]
    public async Task VariantLayoutsAsync_AVersionWithNoMatches_IsNull()
    {
        var layouts = await _layouts.VariantLayoutsAsync([Guid.NewGuid()]);

        Assert.That(layouts.Values.Single(), Is.Null);
    }

    [Test]
    public async Task ApplyAsync_UsesTheVersionsLayout_WhenTheMatchHasOne()
    {
        var version = Guid.NewGuid();
        await SeedMatch(Guid.NewGuid(), 5, 3, version);
        var second = await SeedMatch(Guid.NewGuid(), 2, 1, version);

        await _layouts.ApplyAsync(second);

        Assert.That(second.ScoreLayout, Is.EqualTo(CustomScoreLayout.Plain));
    }
}
