using System.Reflection;
using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A bracket slot is a part of its season match's aggregate (#298): made only by its factories, each side's team from a
/// seed or a match before it (exactly one, as the database's checks say), linked both ways as it is made, and with no
/// public way in to change it. No database, host, file or network (the context's side is Infrastructure.Test's).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MatchBracketInfoUnitTests
{
    private const string SeedOrMatchBefore = "Each side of a bracket slot takes a seed or a match before it, not both or neither";
    private const string OnlyAWaitingGrandFinal = "Only a grand final still waiting for the losers' bracket takes its final";

    /// <summary>Whether anything was linked to <paramref name="slot"/> as a match before it, on either side.</summary>
    private static (IReadOnlyCollection<MatchBracketInfo> Home, IReadOnlyCollection<MatchBracketInfo> Away) LedTo(MatchBracketInfo slot) =>
        (slot.InverseHomeTeamPreviousMatchBracketInfo, slot.InverseAwayTeamNextMatchBracketInfo);

    private static readonly (MatchBracketInfo[] Home, MatchBracketInfo[] Away) LedNowhere = ([], []);

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
        // Their losers go on too, into the losers' bracket: a match leads to as many slots as take from it
        var losers = MatchBracketInfo.Following(Bracket.Loser, 1, 1, top, bottom);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((final.HomeTeamPreviousMatchBracketInfo, final.AwayTeamPreviousMatchBracketInfo), Is.EqualTo((top, bottom)));
            Assert.That((final.HomeTeamSeedNumber, final.AwayTeamSeedNumber), Is.EqualTo(((int?)null, (int?)null)), "No seed where a match feeds it");
            Assert.That(top.InverseHomeTeamPreviousMatchBracketInfo, Is.EquivalentTo(new List<MatchBracketInfo> { final, losers }),
                "The top match's teams go on at home");
            Assert.That(bottom.InverseAwayTeamNextMatchBracketInfo, Is.EquivalentTo(new List<MatchBracketInfo> { final, losers }),
                "The bottom match's teams go on away");
            Assert.That((top.InverseAwayTeamNextMatchBracketInfo, bottom.InverseHomeTeamPreviousMatchBracketInfo), Is.EqualTo(LedNowhere));
            Assert.That(LedTo(final), Is.EqualTo(LedNowhere), "Nothing follows it yet");
        }
    }

    [Test]
    public void Following_RefusesAMissingMatchBefore_AndLinksNothing()
    {
        var before = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);

        var noHome = Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.Following(Bracket.Winner, 2, 1, null!, before))!;
        var noAway = Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.Following(Bracket.Winner, 2, 1, before, null!))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noHome.ParamName, Is.EqualTo("homeFrom"));
            Assert.That(noAway.ParamName, Is.EqualTo("awayFrom"));
            Assert.That(LedTo(before), Is.EqualTo(LedNowhere), "A refused slot isn't linked to the match before it, on either side");
        }
    }

    [Test]
    public void From_MixesASeedAndAMatchBefore_OnePerSide()
    {
        var before = MatchBracketInfo.Seeded(Bracket.Loser, 1, 1, 5, 6);

        var slot = MatchBracketInfo.From(Bracket.Loser, 2, 1, homeSeed: 4, homeFrom: null!, awaySeed: null, awayFrom: before);
        var mirrored = MatchBracketInfo.From(Bracket.Loser, 2, 2, homeSeed: null, homeFrom: before, awaySeed: 3, awayFrom: null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((slot.HomeTeamSeedNumber, slot.HomeTeamPreviousMatchBracketInfo), Is.EqualTo(((int?)4, (MatchBracketInfo?)null)));
            Assert.That((slot.AwayTeamSeedNumber, slot.AwayTeamPreviousMatchBracketInfo), Is.EqualTo(((int?)null, before)));
            Assert.That((mirrored.HomeTeamPreviousMatchBracketInfo, mirrored.HomeTeamSeedNumber, mirrored.AwayTeamSeedNumber),
                Is.EqualTo((before, (int?)null, (int?)3)));
            Assert.That(LedTo(before), Is.EqualTo((new[] { mirrored }, new[] { slot })));
        }
    }

    // Each case breaks one side only, the other side valid: only that side's check can refuse it
    [TestCase(true, true, true, false, TestName = "From_RefusesAHomeSideWithBoth")]
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
            Assert.That(LedTo(before), Is.EqualTo(LedNowhere), "Nothing is linked to a refused slot");
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
            Assert.That(LedTo(winnersFinal), Is.EqualTo((new[] { grandFinal }, Array.Empty<MatchBracketInfo>())));
            Assert.That(LedTo(losersFinal), Is.EqualTo((Array.Empty<MatchBracketInfo>(), new[] { grandFinal })));
            Assert.That(again.Message, Is.EqualTo(OnlyAWaitingGrandFinal));
            Assert.That(LedTo(otherLosersFinal), Is.EqualTo(LedNowhere), "The refused final isn't linked");
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
            Assert.That(notAGrandFinal.Message, Is.EqualTo(OnlyAWaitingGrandFinal));
            Assert.That(noLosersFinal.ParamName, Is.EqualTo("losersFinal"));
            Assert.That(grandFinal.AwayTeamPreviousMatchBracketInfo, Is.Null, "Still waiting");
            Assert.That(LedTo(losersFinal), Is.EqualTo(LedNowhere));
        }
    }

    [Test]
    public void AGrandFinal_WhoseAwaySideIsASeed_IsNotWaiting()
    {
        var seeded = MatchBracketInfo.Seed(bracket: Bracket.GrandFinal, awayTeamSeedNumber: 2);
        var losersFinal = MatchBracketInfo.Seeded(Bracket.Loser, 1, 1, 3, 4);

        var refused = Assert.Throws<InvalidOperationException>(() => seeded.AwaitLosersFinal(losersFinal))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(refused.Message, Is.EqualTo(OnlyAWaitingGrandFinal));
            Assert.That((seeded.AwayTeamSeedNumber, seeded.AwayTeamPreviousMatchBracketInfo), Is.EqualTo(((int?)2, (MatchBracketInfo?)null)), "Its seed stays");
            Assert.That(LedTo(losersFinal), Is.EqualTo(LedNowhere));
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
            Assert.That(LedTo(grandFinal), Is.EqualTo((new[] { suddenDeath }, new[] { suddenDeath })));
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
            Assert.That(LedTo(winnersFinal), Is.EqualTo(LedNowhere));
        }
    }

    [TestCase(Bracket.Winner, false)]
    [TestCase(Bracket.Loser, false)]
    [TestCase(Bracket.GrandFinal, true)]
    [TestCase(Bracket.GrandFinalSuddenDeath, true)]
    public void IsGrandFinal_AgreesWithItsExpression(Bracket bracket, bool grandFinal)
    {
        var slot = MatchBracketInfo.Seed(bracket: bracket);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(slot.IsGrandFinal, Is.EqualTo(grandFinal));
            Assert.That(MatchBracketInfo.InGrandFinal.Compile()(slot), Is.EqualTo(grandFinal), "The query's rule and the loaded slot's are one rule");
        }
    }

    [TestCase(typeof(SeasonMatch))]
    [TestCase(typeof(MatchBracketInfo))]
    public void TheAggregate_HasNoPublicSetter(Type type)
    {
        // Its own and the audit columns it inherits (AuditableEntity)
        var publicSetters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(publicSetters, Is.Empty);
            Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty, "Made by its factories");
        }
    }

    [Test]
    public void Forfeit_RefusesAnUnknownForfeiter()
    {
        var match = SeasonMatch.Seed(1, 10, 20);

        var refused = Assert.Throws<ArgumentOutOfRangeException>(() => match.Forfeit((Forfeiter)42))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((refused.ParamName, refused.ActualValue), Is.EqualTo(("forfeiter", (object)(Forfeiter)42)));
            Assert.That(match.IsDecided, Is.False, "An unknown forfeiter decides nothing");
        }
    }
}
