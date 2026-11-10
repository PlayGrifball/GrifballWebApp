using System.Linq.Expressions;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Stats;

namespace GrifballWebApp.Integration.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class GrifballGoalsTests
{
    // A participant of a match with the given mode layout (null: not decoded yet), its goals decoded as given
    private static MatchParticipant In(CustomScoreLayout? layout, int score, int? goals = null, int objectives = 0, int kills = 0, int deaths = 0, int personalScore = 0)
    {
        var match = Match.Seed(Guid.NewGuid());
        match.AddTeam(0, 0, Outcomes.Won);
        var player = match.AddParticipant(0, 1, new MatchParticipantStats { Score = score, PersonalScore = personalScore, ObjectivesCompleted = objectives, Kills = kills, Deaths = deaths });
        if (layout is { } decided)
            match.Decode(decided, new Dictionary<MatchParticipant, DecodedScore> { [player] = new DecodedScore(goals) });
        return player;
    }

    [Test]
    public void Infinite_NotDecodedYet_IsAScoreOfUpTo20_NotObjectivesCompleted()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GrifballGoals.Infinite(In(null, 3, objectives: 5)), Is.EqualTo(3));
            Assert.That(GrifballGoals.Infinite(In(null, 20)), Is.EqualTo(20));
        });
    }

    [Test]
    public void Infinite_NotDecodedYet_HasNoGoals_FromAPackedCustomModeScore()
    {
        // #217: a score above 20 is a custom mode's packed numbers, not goals
        Assert.That(GrifballGoals.Infinite(In(null, 504_010_048)), Is.Null);
    }

    [Test]
    public void Infinite_Decoded_IsTheDecodedGoals()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GrifballGoals.Infinite(In(CustomScoreLayout.Plain, 4, goals: 4)), Is.EqualTo(4));
            Assert.That(GrifballGoals.Infinite(In(CustomScoreLayout.B, 504_010_048, goals: 5)), Is.EqualTo(5));
            Assert.That(GrifballGoals.Infinite(In(CustomScoreLayout.Undecoded, 504_010_048)), Is.Null);
        });
    }

    [Test]
    public void Inline_ReplacesEveryCall_NestedLambdasIncluded_WithTheDefinition()
    {
        var inlined = GrifballGoals.Inline((List<MatchParticipant> players) =>
            players.Sum(p => GrifballGoals.Infinite(p)) + GrifballGoals.Infinite(players[0]));

        var players = new List<MatchParticipant> { In(null, 2, objectives: 9), In(CustomScoreLayout.B, 504_010_048, goals: 4) };
        Assert.Multiple(() =>
        {
            Assert.That(new GoalsCallFinder().Found(inlined), Is.False, "A query provider would not know the call");
            Assert.That(inlined.Compile()(players), Is.EqualTo(2 + 4 + 2));
        });
    }

    [Test]
    public void Inline_AlsoInlinesTheModelsRules_AsTheStatsReadHalosPoints()
    {
        // The stats' Score: Halo's points (Match.WithHalosPoints), unless the mode packs its counts into them (#217)
        var inlined = GrifballGoals.Inline((MatchParticipant p) =>
            p.MatchTeam.Match.Apply(Match.WithHalosPoints) ? (int?)p.PersonalScore : null);

        var score = inlined.Compile();
        Assert.Multiple(() =>
        {
            Assert.That(new ModelRulesCallFinder().Found(inlined), Is.False, "A query provider would not know the call");
            Assert.That(score(In(null, 3, personalScore: 450)), Is.EqualTo(450), "Not decoded yet");
            Assert.That(score(In(CustomScoreLayout.Plain, 3, personalScore: 450)), Is.EqualTo(450));
            Assert.That(score(In(CustomScoreLayout.B, 504_010_048, personalScore: 2801)), Is.Null, "Packed counts, not points");
            Assert.That(In(CustomScoreLayout.B, 504_010_048).MatchTeam.Match.HasHalosPoints, Is.False);
        });
    }

    [Test]
    public void Inline_LeavesOtherCallsAlone()
    {
        var inlined = GrifballGoals.Inline((MatchParticipant p) => Math.Max(p.Kills, p.Deaths));

        Assert.That(inlined.Compile()(In(null, 0, kills: 7, deaths: 3)), Is.EqualTo(7));
    }

    private sealed class ModelRulesCallFinder : ExpressionVisitor
    {
        private bool _found;

        public bool Found(Expression expression)
        {
            Visit(expression);
            return _found;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            _found |= node.Method.DeclaringType == typeof(ModelRules);
            return base.VisitMethodCall(node);
        }
    }

    private sealed class GoalsCallFinder : ExpressionVisitor
    {
        private bool _found;

        public bool Found(Expression expression)
        {
            Visit(expression);
            return _found;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            _found |= node.Method.DeclaringType == typeof(GrifballGoals);
            return base.VisitMethodCall(node);
        }
    }
}
