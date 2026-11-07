using System.Reflection;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>
/// A bracket slot is a part of its season match's aggregate (#298): made only by its factories, each side's team from a
/// seed or a match before it (exactly one, as the database's checks say), linked both ways as it is made, and with no
/// public way in to change it.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MatchBracketInfoModelTests
{
    [Test]
    public void Seeded_IsAFirstRoundSlotOfTwoSeeds()
    {
        var slot = MatchBracketInfo.Seeded(Bracket.Winner, 1, 3, 1, 8);

        Assert.That((slot.Bracket, slot.RoundNumber, slot.MatchNumber, slot.HomeTeamSeedNumber, slot.AwayTeamSeedNumber, slot.HomeTeamPreviousMatchBracketInfo, slot.AwayTeamPreviousMatchBracketInfo),
            Is.EqualTo((Bracket.Winner, 1, 3, (int?)1, (int?)8, (MatchBracketInfo?)null, (MatchBracketInfo?)null)));
    }

    [Test]
    public void Following_TakesEachSideFromAMatchBefore_AndTheyKnowWhereTheirTeamsGo()
    {
        var home = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 4);
        var away = MatchBracketInfo.Seeded(Bracket.Winner, 1, 2, 2, 3);

        var next = MatchBracketInfo.Following(Bracket.Winner, 2, 3, home, away);
        var losers = MatchBracketInfo.Following(Bracket.Loser, 1, 1, home, away);

        Assert.Multiple(() =>
        {
            Assert.That((next.HomeTeamPreviousMatchBracketInfo, next.AwayTeamPreviousMatchBracketInfo, next.HomeTeamSeedNumber, next.AwayTeamSeedNumber),
                Is.EqualTo((home, away, (int?)null, (int?)null)));
            Assert.That(home.InverseHomeTeamPreviousMatchBracketInfo, Is.EqualTo(new[] { next, losers }));
            Assert.That(away.InverseAwayTeamNextMatchBracketInfo, Is.EqualTo(new[] { next, losers }));
            Assert.That(home.InverseAwayTeamNextMatchBracketInfo, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.Following(Bracket.Winner, 2, 3, home, null!));
        });
    }

    [Test]
    public void From_TakesASeedOrAMatchBeforeOnEachSide_NeverBothOrNeither()
    {
        var previous = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);

        var mixed = MatchBracketInfo.From(Bracket.Winner, 2, 2, null, previous, 3, null);

        Assert.Multiple(() =>
        {
            Assert.That((mixed.HomeTeamPreviousMatchBracketInfo, mixed.HomeTeamSeedNumber, mixed.AwayTeamSeedNumber), Is.EqualTo((previous, (int?)null, (int?)3)));
            Assert.Throws<ArgumentException>(() => MatchBracketInfo.From(Bracket.Winner, 2, 2, 1, previous, 3, null), "Both");
            Assert.Throws<ArgumentException>(() => MatchBracketInfo.From(Bracket.Winner, 2, 2, 1, null, null, null), "Neither");
        });
    }

    [Test]
    public void TheGrandFinal_WaitsForTheLosersFinal_Once_AndItsSuddenDeathFollowsIt()
    {
        var winnersFinal = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);
        var losersFinal = MatchBracketInfo.Seeded(Bracket.Loser, 1, 1, 3, 4);
        var grandFinal = MatchBracketInfo.GrandFinal(2, 2, winnersFinal);
        var suddenDeath = MatchBracketInfo.SuddenDeath(3, 3, grandFinal);

        Assert.That(grandFinal.AwayTeamPreviousMatchBracketInfo, Is.Null, "Waiting");
        grandFinal.AwaitLosersFinal(losersFinal);

        Assert.Multiple(() =>
        {
            Assert.That((grandFinal.Bracket, grandFinal.HomeTeamPreviousMatchBracketInfo, grandFinal.AwayTeamPreviousMatchBracketInfo), Is.EqualTo((Bracket.GrandFinal, winnersFinal, losersFinal)));
            Assert.That(losersFinal.InverseAwayTeamNextMatchBracketInfo, Is.EqualTo(new[] { grandFinal }));
            Assert.That((suddenDeath.Bracket, suddenDeath.HomeTeamPreviousMatchBracketInfo, suddenDeath.AwayTeamPreviousMatchBracketInfo), Is.EqualTo((Bracket.GrandFinalSuddenDeath, grandFinal, grandFinal)));
            Assert.Throws<InvalidOperationException>(() => grandFinal.AwaitLosersFinal(losersFinal), "Once");
            Assert.Throws<InvalidOperationException>(() => winnersFinal.AwaitLosersFinal(losersFinal), "Only a grand final");
            Assert.Throws<ArgumentException>(() => MatchBracketInfo.SuddenDeath(3, 3, winnersFinal), "Only after a grand final");
        });
    }

    [Test]
    public void TheFactories_RefuseAMissingMatchBefore()
    {
        var slot = MatchBracketInfo.Seeded(Bracket.Winner, 1, 1, 1, 2);
        var grandFinal = MatchBracketInfo.GrandFinal(2, 2, slot);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.Following(Bracket.Winner, 2, 3, null!, slot));
            Assert.Throws<ArgumentNullException>(() => MatchBracketInfo.GrandFinal(2, 2, null!));
            Assert.Throws<ArgumentException>(() => MatchBracketInfo.SuddenDeath(3, 3, null!));
            Assert.Throws<ArgumentNullException>(() => grandFinal.AwaitLosersFinal(null!));
        });
    }

    [Test]
    public void AGrandFinal_WhoseAwaySideIsASeed_IsNotWaiting()
    {
        var seeded = MatchBracketInfo.Seed(bracket: Bracket.GrandFinal, awayTeamSeedNumber: 2);

        Assert.Throws<InvalidOperationException>(() => seeded.AwaitLosersFinal(MatchBracketInfo.Seeded(Bracket.Loser, 1, 1, 3, 4)));
    }

    [Test]
    public void Forfeit_RefusesAnUnknownForfeiter()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SeasonMatch.Seed(1, 10, 20).Forfeit((Forfeiter)42));
    }

    [TestCase(Bracket.Winner, false)]
    [TestCase(Bracket.Loser, false)]
    [TestCase(Bracket.GrandFinal, true)]
    [TestCase(Bracket.GrandFinalSuddenDeath, true)]
    public void IsGrandFinal_AgreesWithItsExpression(Bracket bracket, bool grandFinal)
    {
        var slot = MatchBracketInfo.Seed(bracket: bracket);

        Assert.Multiple(() =>
        {
            Assert.That(slot.IsGrandFinal, Is.EqualTo(grandFinal));
            Assert.That(MatchBracketInfo.InGrandFinal.Compile()(slot), Is.EqualTo(grandFinal));
        });
    }

    [Test]
    public void ASeasonMatch_OpensAndClosesItsRescheduleRequest()
    {
        var match = SeasonMatch.Seed(1, 10, 20);

        match.OpenRescheduleRequest(7);
        var open = match.ActiveRescheduleRequestId;
        match.CloseRescheduleRequest();

        Assert.That((open, match.ActiveRescheduleRequestId, match.ActiveRescheduleRequest), Is.EqualTo(((int?)7, (int?)null, (MatchReschedule?)null)));
    }

    [TestCase(typeof(SeasonMatch))]
    [TestCase(typeof(MatchBracketInfo))]
    public void TheAggregate_HasNoPublicSetter(Type type)
    {
        // Its own: the audit columns of every AuditableEntity are the audit interceptor's
        var publicSetters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.DeclaringType == type && p.SetMethod is { IsPublic: true })
            .Select(p => p.Name);

        Assert.Multiple(() =>
        {
            Assert.That(publicSetters, Is.Empty);
            Assert.That(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance), Is.Empty, "Made by its factories");
        });
    }

    [Test]
    public void TheContext_HasNoPublicSetOfBracketSlots()
    {
        var publicSets = typeof(GrifballContext).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.PropertyType == typeof(DbSet<MatchBracketInfo>));

        Assert.Multiple(() =>
        {
            Assert.That(publicSets, Is.Empty);
            Assert.That(typeof(GrifballContext).GetProperty(nameof(GrifballContext.MatchBracketInfo), BindingFlags.NonPublic | BindingFlags.Instance)!.GetMethod!.IsAssembly, Is.True);
        });
    }
}
