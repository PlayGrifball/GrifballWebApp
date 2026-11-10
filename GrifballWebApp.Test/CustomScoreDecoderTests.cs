using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Test;

/// <summary>
/// The custom modes' Score and PersonalScore decoded as the league's stats sheets decode them (#217): each case is a row of
/// a workbook's Copy tab (PersonalScore in GB, Score in GG, the sheet's decoded values in GP–GV), and the team-score
/// validator that picks a match's layout.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class CustomScoreDecoderTests
{
    private static TimeSpan Seconds(double s) => TimeSpan.FromSeconds(s);

    // SML25 Legacy, Copy rows 2, 127 and 202: layout A
    [TestCase(30001124, 3, 0, 0, 1, 248, TestName = "A: SML25 Legacy row 2, 3 goals, 1 sword kill, 248 s held")]
    [TestCase(10203081, 1, 0, 2, 3, 162, TestName = "A: SML25 Legacy row 127, 2 plate stops, 3 sword kills")]
    [TestCase(120001056, 2, 1, 0, 1, 112, TestName = "A: SML25 Legacy row 202, a pass-in")]
    public void LayoutA_ReadsTheSheetsValues(int personalScore, int goals, int passIns, int plateStops, int swordKills, int heldSeconds)
    {
        // A's Score is its PersonalScore
        var d = CustomScoreDecoder.Decode(CustomScoreLayout.A, personalScore, personalScore);

        Assert.That(d, Is.EqualTo(new DecodedScore(goals, PassIns: passIns, PlateStops: plateStops, SwordKills: swordKills, BallHeldTime: Seconds(heldSeconds))));
    }

    // SML25 Legacy, Copy rows 218, 219 and 221: layout B (Score differs in the last digits; the sheet reads PersonalScore)
    [TestCase(201000054, 201000048, 2, 1, 0, 108, TestName = "B: SML25 Legacy row 218, 2 goals, 1 carrier kill")]
    [TestCase(208040236, 208040240, 2, 8, 4, 472, TestName = "B: SML25 Legacy row 219, 8 carrier kills, 4 punches, 472 s held")]
    [TestCase(7030041, 7030041, 0, 7, 3, 82, TestName = "B: SML25 Legacy row 221, a shorter number, no goals")]
    public void LayoutB_ReadsTheSheetsValues(int personalScore, int score, int goals, int carrierKills, int punches, int heldSeconds)
    {
        var d = CustomScoreDecoder.Decode(CustomScoreLayout.B, score, personalScore);

        Assert.That(d, Is.EqualTo(new DecodedScore(goals, CarrierKills: carrierKills, BallPunches: punches, BallHeldTime: Seconds(heldSeconds))));
    }

    // SL26 Major, Copy rows 2, 122 and 142: layout C
    [TestCase(158060304, 100, 3, 6, 79, 0, 0, 1, TestName = "C: SL26 Major row 2, 3 goals, a goal assist, +4 in the last digits")]
    [TestCase(37050100, 1030100, 1, 5, 18.5, 1, 3, 1, TestName = "C: SL26 Major row 122, held time in half-seconds")]
    [TestCase(9000000, 7120000, 0, 0, 4.5, 7, 12, 0, TestName = "C: SL26 Major row 142, 7 carrier kills, 12 sword kills")]
    public void LayoutC_ReadsTheSheetsValues(int personalScore, int score, int goals, int punches, double heldSeconds, int carrierKills, int swordKills, int goalAssists)
    {
        var d = CustomScoreDecoder.Decode(CustomScoreLayout.C, score, personalScore);

        Assert.That(d, Is.EqualTo(new DecodedScore(goals, SwordKills: swordKills, CarrierKills: carrierKills, BallPunches: punches,
            GoalAssists: goalAssists, BallHeldTime: Seconds(heldSeconds))));
    }

    [Test]
    public void LayoutC_ANegativeAdjustment_BorrowsFromTheFieldsAbove_SoTheyreReadFromTheNearestHundred()
    {
        // SL26 Major's game of 2026-04-03 01:06: the sheet reads 105060092 as no goals (MID), but it's 105060100 less 8,
        // and the team's goals only add up to its score with the goal read back
        var borrowed = CustomScoreDecoder.Decode(CustomScoreLayout.C, 2090000, 105060092);
        var issue = CustomScoreDecoder.Decode(CustomScoreLayout.C, 0, 132199996);

        Assert.Multiple(() =>
        {
            Assert.That((borrowed.Goals, borrowed.BallPunches, borrowed.BallHeldTime), Is.EqualTo(((int?)1, (int?)6, (TimeSpan?)Seconds(52.5))));
            Assert.That((issue.Goals, issue.BallPunches, issue.BallHeldTime), Is.EqualTo(((int?)0, (int?)20, (TimeSpan?)Seconds(66))), "132199996 is 132200000 less 4");
        });
    }

    [Test]
    public void Plain_IsTheScore_UpTo20()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CustomScoreDecoder.Decode(CustomScoreLayout.Plain, 3, 2801), Is.EqualTo(new DecodedScore(3)));
            Assert.That(CustomScoreDecoder.Decode(CustomScoreLayout.Plain, 21, 21), Is.EqualTo(DecodedScore.None));
        });
    }

    [TestCase(CustomScoreLayout.A)]
    [TestCase(CustomScoreLayout.B)]
    [TestCase(CustomScoreLayout.C)]
    [TestCase(CustomScoreLayout.Ambiguous)]
    [TestCase(CustomScoreLayout.Undecoded)]
    public void NegativeValues_AndNoLayout_DecodeToNothing(CustomScoreLayout layout)
    {
        Assert.That(CustomScoreDecoder.Decode(layout, -990000, -1999800), Is.EqualTo(DecodedScore.None));
    }

    private static TeamScores Team(int score, params (int Score, int PersonalScore)[] players) =>
        new(score, players.Select(p => new PlayerScores(p.Score, p.PersonalScore)).ToList());

    private static TeamScores A(int score, params int[] values) => Team(score, values.Select(v => (v, v)).ToArray());

    /// <summary>The SL26 game above: C only fits with the last digits read as an adjustment.</summary>
    private static readonly TeamScores[] SL26Game =
    [
        Team(5, (2090000, 105060092), (2010000, 1000000), (1030000, 11040100), (4020000, 101040302)),
        Team(2, (2010000, 106040096), (4000000, 18000100), (0, 1000000), (2030000, 4000000)),
    ];

    [Test]
    public void TheValidator_PicksTheLayoutWhoseGoalsAddUpToEveryTeamsScore()
    {
        // SML25 Legacy rows 2 and 127 on one side (4 goals), the other side 0 goals
        var a = new[] { A(4, 30001124, 10203081), A(0, 2081, 1004) };
        // SML25 Legacy rows 218, 219 (4 goals) against 221 (0)
        var b = new[] { Team(4, (201000048, 201000054), (208040240, 208040236)), Team(0, (7030041, 7030041)) };

        Assert.Multiple(() =>
        {
            Assert.That(CustomScoreDecoder.ByTeamScores(a), Is.EqualTo((CustomScoreLayout.A, true)));
            Assert.That(CustomScoreDecoder.ByTeamScores(b), Is.EqualTo((CustomScoreLayout.B, true)));
            Assert.That(CustomScoreDecoder.ByTeamScores(SL26Game), Is.EqualTo((CustomScoreLayout.C, true)));
            Assert.That(CustomScoreDecoder.Fits(CustomScoreLayout.A, b), Is.False);
        });
    }

    [Test]
    public void Plain_WinsWhenItFits_ForScoresThatAreGoals()
    {
        // 2023-11 to 2024-06: Score is the goals, PersonalScore Halo's points (100 a kill and more)
        var plain = new[] { Team(3, (1, 2801), (2, 3201)), Team(1, (1, 900), (0, 400)) };

        Assert.That(CustomScoreDecoder.ByTeamScores(plain), Is.EqualTo((CustomScoreLayout.Plain, true)));
    }

    [Test]
    public void A00Game_FitsEveryLayout_SoItsAmbiguous_AndKeepsTheGoalsTheyAgreeOn()
    {
        var nilNil = new[] { A(0, 2081, 1004), A(0, 41, 0) };

        var (layout, fitted) = CustomScoreDecoder.ByTeamScores(nilNil);
        var decoded = CustomScoreDecoder.DecodeMatch(layout, nilNil);

        Assert.Multiple(() =>
        {
            Assert.That((layout, fitted), Is.EqualTo((CustomScoreLayout.Ambiguous, true)));
            Assert.That(decoded.Select(d => d.Goals), Is.All.EqualTo(0));
            Assert.That(decoded.Select(d => d.BallHeldTime), Is.All.Null, "Only goals: which layout's held time is unknown");
        });
    }

    [Test]
    public void AnAmbiguousGame_WhoseLayoutsDisagreeOnAPlayer_LeavesThatPlayersGoalsOut()
    {
        // 120001056 is 2 goals under A, 1 under B,
        // and 100000000 (no goals under A, 1 under B): the team's 2 is made up differently under each
        var game = new[] { Team(2, (120001056, 120001056), (0, 100000000)), Team(0) };

        Assert.Multiple(() =>
        {
            Assert.That(CustomScoreDecoder.Fitting(game), Is.EqualTo(new[] { CustomScoreLayout.A, CustomScoreLayout.B }));
            Assert.That(CustomScoreDecoder.DecodeMatch(CustomScoreLayout.Ambiguous, game).Select(d => d.Goals), Is.EqualTo(new int?[] { null, null }));
        });
    }

    [Test]
    public void NoLayoutFits_ItsUndecoded_NoGoalsCounted()
    {
        // SL26-era numbers whose goals add up under no layout (2026-01)
        var game = new[] { Team(5, (3000000, 15010100), (3000000, 19030200)), Team(2, (2000000, 18020000), (5000000, 53110300)) };

        var (layout, fitted) = CustomScoreDecoder.ByTeamScores(game);

        Assert.Multiple(() =>
        {
            Assert.That((layout, fitted), Is.EqualTo((CustomScoreLayout.Undecoded, false)));
            Assert.That(CustomScoreDecoder.DecodeMatch(layout, game), Is.All.EqualTo(DecodedScore.None));
        });
    }

    [Test]
    public void NoLayoutFits_ButEveryScoreIs20OrLess_ItStaysPlain_AsTheStatsAlwaysReadIt()
    {
        // A player who left: their goals aren't in the stats, so the team's don't add up
        var game = new[] { Team(3, (1, 1000), (1, 500)), Team(1, (1, 400)) };

        Assert.That(CustomScoreDecoder.ByTeamScores(game), Is.EqualTo((CustomScoreLayout.Plain, false)));
    }

    [Test]
    public void AMatchWithNoTeams_FitsNothing()
    {
        Assert.That(CustomScoreDecoder.Fitting([]), Is.Empty);
    }

    [Test]
    public void AVariantsLayout_IsTheOneThatFitsMostOfItsMatches_IgnoringAmbiguousOnes()
    {
        var aGame = new[] { A(4, 30001124, 10203081), A(0, 2081, 1004) };
        var nilNil = new[] { A(0, 2081, 1004), A(0, 41, 0) };
        var noFit = new[] { A(3, 30001124, 10203081), A(0, 2081, 1004) };

        Assert.Multiple(() =>
        {
            Assert.That(CustomScoreDecoder.VariantLayout([aGame, aGame, noFit, nilNil, nilNil, nilNil]), Is.EqualTo(CustomScoreLayout.A));
            Assert.That(CustomScoreDecoder.VariantLayout([aGame, noFit]), Is.Null, "Half isn't most");
            Assert.That(CustomScoreDecoder.VariantLayout([nilNil, noFit]), Is.Null, "Nothing fits");
            Assert.That(CustomScoreDecoder.VariantLayout([]), Is.Null);
        });
    }

    [Test]
    public void AKnownVariant_DecidesItsMatches_00GamesIncluded()
    {
        var nilNil = new[] { A(0, 2081, 1004), A(0, 41, 0) };
        var plainBig = new[] { Team(0, (25, 25)) };

        Assert.Multiple(() =>
        {
            Assert.That(CustomScoreDecoder.Choose(nilNil, CustomScoreLayout.A), Is.EqualTo(CustomScoreLayout.A));
            Assert.That(CustomScoreDecoder.DecodeMatch(CustomScoreLayout.A, nilNil)[0].BallHeldTime, Is.EqualTo(Seconds(162)));
            Assert.That(CustomScoreDecoder.Choose(nilNil, null), Is.EqualTo(CustomScoreLayout.Ambiguous));
            Assert.That(CustomScoreDecoder.Choose(SL26Game, CustomScoreLayout.Undecoded), Is.EqualTo(CustomScoreLayout.C), "Not a layout: the team scores decide");
            Assert.That(CustomScoreDecoder.Choose(plainBig, CustomScoreLayout.Plain), Is.EqualTo(CustomScoreLayout.Undecoded), "A Plain variant never makes 25 goals");
            Assert.That(CustomScoreDecoder.Choose([Team(1, (1, 300))], CustomScoreLayout.Plain), Is.EqualTo(CustomScoreLayout.Plain));
        });
    }
}
