using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Domain.Test;

/// <summary>A bracket slot's factories: each side a seed or the match before it (exactly one), the grand final waiting
/// for the losers' bracket, and its sudden death. No database, host, file or network.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchBracketInfoUnitTests
{
    private const string SeedOrMatchBefore = "Each side of a bracket slot takes a seed or a match before it, not both or neither";

    [Test]
    public void Seeded_IsAFirstRoundSlotOfTwoSeeds()
    {
        var slot = MatchBracketInfo.Seeded(Bracket.Winner, roundNumber: 1, matchNumber: 2, homeSeed: 1, awaySeed: 8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((slot.Bracket, slot.RoundNumber, slot.MatchNumber), Is.EqualTo((Bracket.Winner, 1, 2)));
            Assert.That((slot.HomeTeamSeedNumber, slot.AwayTeamSeedNumber), Is.EqualTo(((int?)1, (int?)8)));
            Assert.That((slot.HomeTeamPreviousMatchBracketInfo, slot.AwayTeamPreviousMatchBracketInfo),
                Is.EqualTo(((MatchBracketInfo?)null, (MatchBracketInfo?)null)), "Neither side comes from a match");
            Assert.That(slot.IsGrandFinal, Is.False);
        }
    }

    [Test]
    public void Following_TakesEachSideFromTheMatchBefore_AndTheyLeadToIt()
    {
        var top = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 4);
        var bottom = MatchBracketInfo.Seeded(Bracket.Winner, 1, 2, 2, 3);

        var final = MatchBracketInfo.Following(Bracket.Winner, 2, 1, top, bottom);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((final.HomeTeamPreviousMatchBracketInfo, final.AwayTeamPreviousMatchBracketInfo), Is.EqualTo((top, bottom)));
            Assert.That((final.HomeTeamSeedNumber, final.AwayTeamSeedNumber), Is.EqualTo(((int?)null, (int?)null)), "No seed where a match feeds it");
            Assert.That(top.InverseHomeTeamPreviousMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { final }), "The top match's team goes on at home");
            Assert.That(bottom.InverseAwayTeamNextMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { final }), "The bottom match's team goes on away");
            Assert.That((top.InverseAwayTeamNextMatchBracketInfo, bottom.InverseHomeTeamPreviousMatchBracketInfo), Is.EqualTo((Array.Empty<MatchBracketInfo>(), Array.Empty<MatchBracketInfo>())));
        }
    }

    [Test]
    public void Following_RefusesAMissingMatchBefore()
    {
        var before = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);

        var noHome = Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.Following(Bracket.Winner, 2, 1, null!, before))!;
        var noAway = Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.Following(Bracket.Winner, 2, 1, before, null!))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noHome.ParamName, Is.EqualTo("homeFrom"));
            Assert.That(noAway.ParamName, Is.EqualTo("awayFrom"));
            Assert.That(before.InverseHomeTeamPreviousMatchBracketInfo, Is.Empty, "A refused slot isn't linked to the match before it");
        }
    }

    [Test]
    public void From_MixesASeedAndAMatchBefore_OnePerSide()
    {
        var before = MatchBracketInfo.Seeded(Bracket.Loser, 1, 1, 5, 6);

        var slot = MatchBracketInfo.From(Bracket.Loser, 2, 1, homeSeed: 4, homeFrom: null!, awaySeed: null, awayFrom: before);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((slot.HomeTeamSeedNumber, slot.HomeTeamPreviousMatchBracketInfo), Is.EqualTo(((int?)4, (MatchBracketInfo?)null)));
            Assert.That((slot.AwayTeamSeedNumber, slot.AwayTeamPreviousMatchBracketInfo), Is.EqualTo(((int?)null, before)));
            Assert.That(before.InverseAwayTeamNextMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { slot }));
        }
    }

    [TestCase(true, true, false, false, TestName = "From_RefusesAHomeSideWithBoth")]
    [TestCase(false, false, true, false, TestName = "From_RefusesAHomeSideWithNeither")]
    [TestCase(true, false, true, true, TestName = "From_RefusesAnAwaySideWithBoth")]
    [TestCase(true, false, false, false, TestName = "From_RefusesAnAwaySideWithNeither")]
    public void From_RefusesASideWithBothOrNeither(bool homeSeeded, bool homeFed, bool awaySeeded, bool awayFed)
    {
        var before = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);

        var refused = Assert.Throws<ArgumentException>(() => MatchBracketInfo.From(Bracket.Winner, 2, 1,
            homeSeeded ? 1 : null, homeFed ? before : null!, awaySeeded ? 2 : null, awayFed ? before : null!))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Message, Is.EqualTo(SeedOrMatchBefore));
            Assert.That((before.InverseHomeTeamPreviousMatchBracketInfo, before.InverseAwayTeamNextMatchBracketInfo),
                Is.EqualTo((Array.Empty<MatchBracketInfo>(), Array.Empty<MatchBracketInfo>())), "Nothing is linked to a refused slot");
        }
    }

    [Test]
    public void GrandFinal_WaitsForTheLosersFinal_ThenTakesItOnce()
    {
        var winnersFinal = MatchBracketInfo.Seeded(Bracket.Winner, 3, 1, 1, 2);
        var losersFinal = MatchBracketInfo.Seeded(Bracket.Loser, 4, 1, 3, 4);
        var otherLosersFinal = MatchBracketInfo.Seeded(Bracket.Loser, 4, 2, 5, 6);

        var grandFinal = MatchBracketInfo.GrandFinal(5, 1, winnersFinal);
        var waiting = (grandFinal.AwayTeamSeedNumber, grandFinal.AwayTeamPreviousMatchBracketInfo);
        grandFinal.AwaitLosersFinal(losersFinal);
        var again = Assert.Throws<InvalidOperationException>(() => grandFinal.AwaitLosersFinal(otherLosersFinal))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((grandFinal.Bracket, grandFinal.RoundNumber, grandFinal.MatchNumber, grandFinal.IsGrandFinal), Is.EqualTo((Bracket.GrandFinal, 5, 1, true)));
            Assert.That(waiting, Is.EqualTo(((int?)null, (MatchBracketInfo?)null)), "Until the losers' bracket is built, its away side is open");
            Assert.That((grandFinal.HomeTeamPreviousMatchBracketInfo, grandFinal.AwayTeamPreviousMatchBracketInfo), Is.EqualTo((winnersFinal, losersFinal)),
                "The winners' bracket's winner at home, the losers' bracket's away");
            Assert.That(winnersFinal.InverseHomeTeamPreviousMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { grandFinal }));
            Assert.That(losersFinal.InverseAwayTeamNextMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { grandFinal }));
            Assert.That(again.Message, Is.EqualTo("Only a grand final still waiting for the losers' bracket takes its final"));
            Assert.That(otherLosersFinal.InverseAwayTeamNextMatchBracketInfo, Is.Empty, "The refused final isn't linked");
        }
    }

    [Test]
    public void GrandFinal_RefusesAMissingWinnersFinal_AndOnlyAGrandFinalAwaitsTheLosers()
    {
        var winnersSlot = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);
        var losersFinal = MatchBracketInfo.Seeded(Bracket.Loser, 2, 1, 3, 4);

        var noWinnersFinal = Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.GrandFinal(3, 1, null!))!;
        var notAGrandFinal = Assert.Throws<InvalidOperationException>(() => winnersSlot.AwaitLosersFinal(losersFinal))!;
        var grandFinal = MatchBracketInfo.GrandFinal(3, 1, winnersSlot);
        var noLosersFinal = Assert.Throws<ArgumentNullException>(() => grandFinal.AwaitLosersFinal(null!))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noWinnersFinal.ParamName, Is.EqualTo("winnersFinal"));
            Assert.That(notAGrandFinal.Message, Is.EqualTo("Only a grand final still waiting for the losers' bracket takes its final"));
            Assert.That(noLosersFinal.ParamName, Is.EqualTo("losersFinal"));
            Assert.That(grandFinal.AwayTeamPreviousMatchBracketInfo, Is.Null, "Still waiting");
        }
    }

    [Test]
    public void SuddenDeath_TakesBothSidesFromTheGrandFinal()
    {
        var grandFinal = MatchBracketInfo.GrandFinal(5, 1, MatchBracketInfo.Seeded(Bracket.Winner, 4, 1, 1, 2));
        grandFinal.AwaitLosersFinal(MatchBracketInfo.Seeded(Bracket.Loser, 4, 2, 3, 4));

        var suddenDeath = MatchBracketInfo.SuddenDeath(6, 1, grandFinal);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((suddenDeath.Bracket, suddenDeath.IsGrandFinal), Is.EqualTo((Bracket.GrandFinalSuddenDeath, true)), "Part of the grand final");
            Assert.That((suddenDeath.HomeTeamPreviousMatchBracketInfo, suddenDeath.AwayTeamPreviousMatchBracketInfo), Is.EqualTo((grandFinal, grandFinal)));
            Assert.That(grandFinal.InverseHomeTeamPreviousMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { suddenDeath }));
            Assert.That(grandFinal.InverseAwayTeamNextMatchBracketInfo, Is.EqualTo(new List<MatchBracketInfo> { suddenDeath }));
        }
    }

    [Test]
    public void SuddenDeath_FollowsOnlyAGrandFinal()
    {
        var winnersFinal = MatchBracketInfo.Seeded(Bracket.Winner, 4, 1, 1, 2);

        var afterAWinnersMatch = Assert.Throws<ArgumentException>(() => MatchBracketInfo.SuddenDeath(5, 1, winnersFinal))!;
        var afterNothing = Assert.Throws<ArgumentException>(() => MatchBracketInfo.SuddenDeath(5, 1, null!))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((afterAWinnersMatch.ParamName, afterAWinnersMatch.Message),
                Is.EqualTo(("grandFinal", "A sudden death follows the grand final (Parameter 'grandFinal')")));
            Assert.That(afterNothing.ParamName, Is.EqualTo("grandFinal"));
            Assert.That(winnersFinal.InverseHomeTeamPreviousMatchBracketInfo, Is.Empty);
        }
    }

    [Test]
    public void InGrandFinal_MatchesIsGrandFinal_ForEveryBracket()
    {
        var inGrandFinal = MatchBracketInfo.InGrandFinal.Compile();
        var slots = Enum.GetValues<Bracket>().Select(bracket => MatchBracketInfo.Seed(bracket: bracket)).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(slots.Where(inGrandFinal).Select(s => s.Bracket), Is.EqualTo(new List<Bracket> { Bracket.GrandFinal, Bracket.GrandFinalSuddenDeath }));
            Assert.That(slots.Select(inGrandFinal), Is.EqualTo(slots.Select(s => s.IsGrandFinal)), "The query's rule and the loaded slot's are one rule");
        }
    }
}
