using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.HaloInfinite;

/// <summary>One player's stats decoded from their Score and PersonalScore. A stat the layout doesn't have is null.</summary>
public sealed record DecodedScore(int? Goals, int? PassIns = null, int? PlateStops = null, int? SwordKills = null, int? CarrierKills = null,
    int? BallPunches = null, int? GoalAssists = null, TimeSpan? BallHeldTime = null)
{
    public static readonly DecodedScore None = new((int?)null);
}

/// <summary>A team of a match as the validator sees it: its score (Halo's, its goals in Grifball) and its players' raw values.</summary>
public sealed record TeamScores(int TeamScore, IReadOnlyList<PlayerScores> Players);

/// <summary>A player's raw Score and PersonalScore.</summary>
public readonly record struct PlayerScores(int Score, int PersonalScore);

/// <summary>
/// Decodes the stats Grifball's custom game modes pack into Halo Infinite's Score and PersonalScore (#217), as the
/// league's stats sheets do with MID(): read right-aligned instead, (value / place) % 10^digits, which also reads the
/// shorter numbers MID() gets wrong. Which layout a match uses can't be told from one number (120001056 is 1 pass-in and 2
/// goals under A, 1 goal and 20 carrier kills under B), so it's chosen per match by the team scores: in Grifball a team's
/// score is its goals, so a layout fits a match when every team's players' decoded goals add up to its score.
/// </summary>
public static class CustomScoreDecoder
{
    /// <summary>The most a Score can be and still be goals: a Plain match's.</summary>
    public const int PlainMaxScore = 20;

    /// <summary>The layouts a match is checked against, in the order they're preferred when more than one fits.</summary>
    public static readonly IReadOnlyList<CustomScoreLayout> Layouts = [CustomScoreLayout.Plain, CustomScoreLayout.A, CustomScoreLayout.B, CustomScoreLayout.C];

    /// <summary>
    /// A player's stats under <paramref name="layout"/>; <see cref="DecodedScore.None"/> for values that can't be that
    /// layout's (negative, or a Plain Score above 20) and for <see cref="CustomScoreLayout.Ambiguous"/> and
    /// <see cref="CustomScoreLayout.Undecoded"/>.
    /// </summary>
    public static DecodedScore Decode(CustomScoreLayout layout, int score, int personalScore)
    {
        switch (layout)
        {
            case CustomScoreLayout.Plain:
                return score is >= 0 and <= PlainMaxScore ? new DecodedScore(score) : DecodedScore.None;
            case CustomScoreLayout.A when personalScore >= 0:
                return new DecodedScore(
                    Goals: Field(personalScore, 10_000_000, 10),
                    PassIns: Field(personalScore, 100_000_000, 10),
                    PlateStops: Field(personalScore, 100_000, 100),
                    SwordKills: Field(personalScore, 1_000, 100),
                    // In units of 2 seconds
                    BallHeldTime: TimeSpan.FromSeconds(Field(personalScore, 1, 1_000) * 2));
            case CustomScoreLayout.B when personalScore >= 0:
                return new DecodedScore(
                    Goals: personalScore / 100_000_000,
                    CarrierKills: Field(personalScore, 1_000_000, 100),
                    BallPunches: Field(personalScore, 10_000, 100),
                    BallHeldTime: TimeSpan.FromSeconds(Field(personalScore, 1, 10_000) * 2));
            case CustomScoreLayout.C when personalScore >= 0 && score >= 0:
                {
                    // The last two digits are a small adjustment either way, which borrows from the fields above it when
                    // it's negative (132199996 is 132200000 less 4): read the fields from the nearest hundred
                    var personal = NearestHundred(personalScore);
                    var mode = NearestHundred(score);
                    return new DecodedScore(
                        Goals: Field(personal, 100, 100),
                        BallPunches: Field(personal, 10_000, 100),
                        // In half-seconds
                        BallHeldTime: TimeSpan.FromSeconds(personal / 1_000_000 / 2.0),
                        CarrierKills: mode / 1_000_000,
                        SwordKills: Field(mode, 10_000, 100),
                        GoalAssists: Field(mode, 100, 100));
                }
            default:
                return DecodedScore.None;
        }
    }

    private static int Field(int value, int place, int size) => value / place % size;

    private static int NearestHundred(int value) => (int)((value + 50L) / 100 * 100);

    /// <summary>A player's goals under <paramref name="layout"/>, null when they can't be read that way.</summary>
    public static int? Goals(CustomScoreLayout layout, PlayerScores player) => Decode(layout, player.Score, player.PersonalScore).Goals;

    /// <summary>
    /// The validator: <paramref name="layout"/> fits the match when every team's players' goals under it add up to the
    /// team's score. Plain fits only a match whose Scores are all 20 or less.
    /// </summary>
    public static bool Fits(CustomScoreLayout layout, IReadOnlyCollection<TeamScores> teams)
    {
        if (teams.Count == 0)
            return false;
        foreach (var team in teams)
        {
            var total = 0;
            foreach (var player in team.Players)
            {
                var goals = Goals(layout, player);
                if (goals is null)
                    return false;
                total += goals.Value;
            }
            if (total != team.TeamScore)
                return false;
        }
        return true;
    }

    /// <summary>Every layout that fits the match, in <see cref="Layouts"/> order.</summary>
    public static IReadOnlyList<CustomScoreLayout> Fitting(IReadOnlyCollection<TeamScores> teams) => Layouts.Where(l => Fits(l, teams)).ToList();

    /// <summary>
    /// The layout the team scores alone give the match, and whether one fitted. Plain when it fits (a Score of 20 or
    /// less that adds up is goals, as the stats always read it); otherwise the one layout that fits; Ambiguous when
    /// several do (mostly 0-0 games, where every layout gives nobody a goal). When none fits: Plain still for a match
    /// whose Scores are all 20 or less (the rule the stats had before, which the layouts can't overrule), Undecoded
    /// otherwise: nothing guessed, no goals counted.
    /// </summary>
    public static (CustomScoreLayout Layout, bool Fitted) ByTeamScores(IReadOnlyCollection<TeamScores> teams)
    {
        var fitting = Fitting(teams);
        if (fitting.Contains(CustomScoreLayout.Plain))
            return (CustomScoreLayout.Plain, true);
        if (fitting.Count == 1)
            return (fitting[0], true);
        if (fitting.Count > 1)
            return (CustomScoreLayout.Ambiguous, true);
        var plainScores = teams.SelectMany(t => t.Players).All(p => p.Score is >= 0 and <= PlainMaxScore);
        return (plainScores ? CustomScoreLayout.Plain : CustomScoreLayout.Undecoded, false);
    }

    /// <summary>
    /// A game variant version's layout: the one that fits most of its matches, when it fits more than half of those the
    /// team scores decide (fitting one layout, or none); null when none does, or the version has no such match.
    /// Ambiguous matches don't count either way.
    /// </summary>
    public static CustomScoreLayout? VariantLayout(IEnumerable<IReadOnlyCollection<TeamScores>> matches)
    {
        var votes = new Dictionary<CustomScoreLayout, int>();
        var decided = 0;
        foreach (var match in matches)
        {
            var (layout, fitted) = ByTeamScores(match);
            if (layout == CustomScoreLayout.Ambiguous)
                continue;
            decided++;
            if (fitted)
                votes[layout] = votes.GetValueOrDefault(layout) + 1;
        }
        if (votes.Count == 0)
            return null;
        var (best, count) = votes.OrderByDescending(v => v.Value).ThenBy(v => v.Key).Select(v => (v.Key, v.Value)).First();
        return count * 2 > decided ? best : null;
    }

    /// <summary>
    /// The match's layout: its variant version's when that has one (so its 0-0 games and the odd one off by a player who
    /// left are read the same way as the rest), otherwise by its team scores (<see cref="ByTeamScores"/>). A Plain variant
    /// doesn't make a Score above 20 goals: that match is Undecoded.
    /// </summary>
    public static CustomScoreLayout Choose(IReadOnlyCollection<TeamScores> teams, CustomScoreLayout? variantLayout)
    {
        if (variantLayout is CustomScoreLayout.Plain)
            return teams.SelectMany(t => t.Players).All(p => p.Score is >= 0 and <= PlainMaxScore) ? CustomScoreLayout.Plain : CustomScoreLayout.Undecoded;
        if (variantLayout is { } layout and not CustomScoreLayout.Ambiguous and not CustomScoreLayout.Undecoded)
            return layout;
        return ByTeamScores(teams).Layout;
    }

    /// <summary>
    /// Each player's stats under the match's <paramref name="layout"/>, in the order of the teams' players. An Ambiguous
    /// match keeps only goals, where every layout that fits gives the player the same.
    /// </summary>
    public static IReadOnlyList<DecodedScore> DecodeMatch(CustomScoreLayout layout, IReadOnlyCollection<TeamScores> teams)
    {
        var players = teams.SelectMany(t => t.Players).ToList();
        if (layout != CustomScoreLayout.Ambiguous)
            return players.Select(p => Decode(layout, p.Score, p.PersonalScore)).ToList();

        var fitting = Fitting(teams);
        return players.Select(p =>
        {
            var goals = fitting.Select(l => Goals(l, p)).Distinct().ToList();
            return goals.Count == 1 ? new DecodedScore(goals[0]) : DecodedScore.None;
        }).ToList();
    }
}
