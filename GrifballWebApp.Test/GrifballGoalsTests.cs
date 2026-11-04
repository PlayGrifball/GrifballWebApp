using System.Linq.Expressions;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Stats;

namespace GrifballWebApp.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class GrifballGoalsTests
{
    [Test]
    public void Infinite_IsTheParticipantsScore_NotObjectivesCompleted()
    {
        var participant = new MatchParticipant { Score = 3, ObjectivesCompleted = 5, PersonalScore = 300 };

        Assert.That(GrifballGoals.Infinite(participant), Is.EqualTo(3));
    }

    [Test]
    public void Infinite_KeepsAPackedCustomModeScore_AsRecorded()
    {
        // #217: these modes' encoding isn't known, so their goals stay the recorded value
        var participant = new MatchParticipant { Score = 504_010_048 };

        Assert.That(GrifballGoals.Infinite(participant), Is.EqualTo(504_010_048));
    }

    [Test]
    public void Inline_ReplacesEveryCall_NestedLambdasIncluded_WithTheDefinition()
    {
        var inlined = GrifballGoals.Inline((List<MatchParticipant> players) =>
            players.Sum(p => GrifballGoals.Infinite(p)) + GrifballGoals.Infinite(players[0]));

        var players = new List<MatchParticipant> { new() { Score = 2, ObjectivesCompleted = 9 }, new() { Score = 4 } };
        Assert.Multiple(() =>
        {
            Assert.That(new GoalsCallFinder().Found(inlined), Is.False, "A query provider would not know the call");
            Assert.That(inlined.Compile()(players), Is.EqualTo(2 + 4 + 2));
        });
    }

    [Test]
    public void Inline_LeavesOtherCallsAlone()
    {
        var inlined = GrifballGoals.Inline((MatchParticipant p) => Math.Max(p.Kills, p.Deaths));

        Assert.That(inlined.Compile()(new MatchParticipant { Kills = 7, Deaths = 3 }), Is.EqualTo(7));
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
