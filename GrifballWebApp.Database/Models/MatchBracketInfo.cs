#nullable disable

using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A season match's slot in its season's bracket: which bracket and round, its number, and where each side's team comes
/// from, a seed or the match before it (exactly one, as the check constraints say). A part of its
/// <see cref="Models.SeasonMatch"/> aggregate: made by the factories below and given to the match
/// (<see cref="SeasonMatch.InBracket"/>, <see cref="SeasonMatch.Imported"/>); nothing outside sets its fields.
/// </summary>
[AggregatePart(typeof(SeasonMatch))]
public class MatchBracketInfo : AuditableEntity
{
    /// <summary>For EF, and the tests' <see cref="Seed"/>.</summary>
    internal MatchBracketInfo() { }

    private MatchBracketInfo(Bracket bracket, int roundNumber, int matchNumber)
    {
        Bracket = bracket;
        RoundNumber = roundNumber;
        MatchNumber = matchNumber;
    }

    /// <summary>A first-round slot: each side's team is a seed.</summary>
    public static MatchBracketInfo Seeded(Bracket bracket, int roundNumber, int matchNumber, int homeSeed, int awaySeed)
        => From(bracket, roundNumber, matchNumber, homeSeed, null, awaySeed, null);

    /// <summary>A later slot: each side's team comes from a match before it (its winner, or its loser into the losers' bracket).</summary>
    public static MatchBracketInfo Following(Bracket bracket, int roundNumber, int matchNumber, MatchBracketInfo homeFrom, MatchBracketInfo awayFrom)
        => From(bracket, roundNumber, matchNumber, null, homeFrom ?? throw new ArgumentNullException(nameof(homeFrom)), null, awayFrom ?? throw new ArgumentNullException(nameof(awayFrom)));

    /// <summary>
    /// A slot whose sides each take a seed or a match before it (exactly one each): what the historical import builds, a
    /// team's first playoff series by a seed and the next ones from its previous series.
    /// </summary>
    public static MatchBracketInfo From(Bracket bracket, int roundNumber, int matchNumber, int? homeSeed, MatchBracketInfo homeFrom, int? awaySeed, MatchBracketInfo awayFrom)
    {
        if ((homeSeed is null) == (homeFrom is null) || (awaySeed is null) == (awayFrom is null))
            throw new ArgumentException("Each side of a bracket slot takes a seed or a match before it, not both or neither");
        var slot = new MatchBracketInfo(bracket, roundNumber, matchNumber) { HomeTeamSeedNumber = homeSeed, AwayTeamSeedNumber = awaySeed };
        slot.FeedHomeFrom(homeFrom);
        slot.FeedAwayFrom(awayFrom);
        return slot;
    }

    /// <summary>
    /// The double-elimination grand final: the winners' bracket final's winner at home. Its away side is the losers'
    /// bracket's winner, whose final is built after it (<see cref="AwaitLosersFinal"/>): until then the slot can't be saved.
    /// </summary>
    public static MatchBracketInfo GrandFinal(int roundNumber, int matchNumber, MatchBracketInfo winnersFinal)
    {
        var slot = new MatchBracketInfo(Bracket.GrandFinal, roundNumber, matchNumber);
        slot.FeedHomeFrom(winnersFinal ?? throw new ArgumentNullException(nameof(winnersFinal)));
        return slot;
    }

    /// <summary>The grand final's sudden death (when the losers' bracket winner wins the grand final): both sides from it.</summary>
    public static MatchBracketInfo SuddenDeath(int roundNumber, int matchNumber, MatchBracketInfo grandFinal)
    {
        if (grandFinal?.Bracket is not Bracket.GrandFinal)
            throw new ArgumentException("A sudden death follows the grand final", nameof(grandFinal));
        return Following(Bracket.GrandFinalSuddenDeath, roundNumber, matchNumber, grandFinal, grandFinal);
    }

    /// <summary>The grand final's away side: the losers' bracket final's winner. Only a grand final still waiting for it.</summary>
    public void AwaitLosersFinal(MatchBracketInfo losersFinal)
    {
        if (Bracket is not Bracket.GrandFinal || AwayTeamSeedNumber is not null || AwayTeamPreviousMatchBracketInfo is not null)
            throw new InvalidOperationException("Only a grand final still waiting for the losers' bracket takes its final");
        FeedAwayFrom(losersFinal ?? throw new ArgumentNullException(nameof(losersFinal)));
    }

    private void FeedHomeFrom(MatchBracketInfo previous)
    {
        if (previous is null)
            return;
        HomeTeamPreviousMatchBracketInfo = previous;
        previous._inverseHomeTeamPreviousMatchBracketInfo.Add(this);
    }

    private void FeedAwayFrom(MatchBracketInfo previous)
    {
        if (previous is null)
            return;
        AwayTeamPreviousMatchBracketInfo = previous;
        previous._inverseAwayTeamNextMatchBracketInfo.Add(this);
    }

    /// <summary>
    /// Any slot, as tests seed one (a slot with a side by its ID, one attached to a saved match by its ID, one that breaks
    /// the rules to see the database refuse it); the app goes through the factories above.
    /// </summary>
    internal static MatchBracketInfo Seed(int roundNumber = 0, int matchNumber = 0, Bracket bracket = Bracket.Winner,
        int? homeTeamSeedNumber = null, int? awayTeamSeedNumber = null,
        int? homeTeamPreviousMatchBracketInfoID = null, int? awayTeamPreviousMatchBracketInfoID = null,
        MatchBracketInfo homeTeamPreviousMatchBracketInfo = null, MatchBracketInfo awayTeamPreviousMatchBracketInfo = null,
        int seasonMatchID = 0, SeasonMatch seasonMatch = null, int matchBracketInfoID = 0)
    {
        var slot = new MatchBracketInfo(bracket, roundNumber, matchNumber)
        {
            MatchBracketInfoID = matchBracketInfoID,
            HomeTeamSeedNumber = homeTeamSeedNumber,
            AwayTeamSeedNumber = awayTeamSeedNumber,
            HomeTeamPreviousMatchBracketInfoID = homeTeamPreviousMatchBracketInfoID,
            AwayTeamPreviousMatchBracketInfoID = awayTeamPreviousMatchBracketInfoID,
            SeasonMatchID = seasonMatchID,
            SeasonMatch = seasonMatch,
        };
        slot.FeedHomeFrom(homeTeamPreviousMatchBracketInfo);
        slot.FeedAwayFrom(awayTeamPreviousMatchBracketInfo);
        return slot;
    }

    public int MatchBracketInfoID { get; internal set; }
    // Its season match's slot: set only through the match (SeasonMatch.InBracket, SeasonMatch.Imported); internal for tests
    public int SeasonMatchID { get; internal set; }
    public SeasonMatch SeasonMatch { get; internal set; }
    public int RoundNumber { get; private set; }
    public int MatchNumber { get; private set; }
    public int? HomeTeamSeedNumber { get; private set; }
    public int? HomeTeamPreviousMatchBracketInfoID { get; private set; }
    public MatchBracketInfo HomeTeamPreviousMatchBracketInfo { get; private set; }
    /// <summary>The slots this one's team goes on to on their home side (read-only: set by the slots' factories, and EF).</summary>
    public IReadOnlyCollection<MatchBracketInfo> InverseHomeTeamPreviousMatchBracketInfo => _inverseHomeTeamPreviousMatchBracketInfo;
    private readonly HashSet<MatchBracketInfo> _inverseHomeTeamPreviousMatchBracketInfo = [];
    public int? AwayTeamSeedNumber { get; private set; }
    public int? AwayTeamPreviousMatchBracketInfoID { get; private set; }
    public MatchBracketInfo AwayTeamPreviousMatchBracketInfo { get; private set; }
    /// <summary>The slots this one's team goes on to on their away side (read-only, as the home side's).</summary>
    public IReadOnlyCollection<MatchBracketInfo> InverseAwayTeamNextMatchBracketInfo => _inverseAwayTeamNextMatchBracketInfo;
    private readonly HashSet<MatchBracketInfo> _inverseAwayTeamNextMatchBracketInfo = [];
    public Bracket Bracket { get; private set; }

    /// <summary>A grand final or its sudden death: never forfeited, its games decide the bracket (<see cref="IsGrandFinal"/>
    /// for a loaded slot).</summary>
    public static readonly Expression<Func<MatchBracketInfo, bool>> InGrandFinal = b => b.Bracket == Bracket.GrandFinal || b.Bracket == Bracket.GrandFinalSuddenDeath;

    /// <summary>Whether it is a grand final or its sudden death (<see cref="InGrandFinal"/> for queries).</summary>
    public bool IsGrandFinal => Bracket is Bracket.GrandFinal or Bracket.GrandFinalSuddenDeath;
}

public enum Bracket
{
    Winner,
    Loser,
    GrandFinal,
    GrandFinalSuddenDeath
}
