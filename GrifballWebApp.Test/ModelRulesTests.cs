using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>A model's rule used inside a bigger query is inlined as the rule itself, so it translates and keeps one definition.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ModelRulesTests
{
    private static readonly RecordedGameFacts Facts = new(1);

    [Test]
    public void Meets_IsTheRule_OnALoadedEntity()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModelRules.Meets(RecordedGame.EnterByHand(Facts), RecordedGame.Manual), Is.True);
            Assert.That(ModelRules.Meets(RecordedGame.Import("archive/1", Facts, null), RecordedGame.Manual), Is.False);
            Assert.That(ModelRules.Meets(RecordedGame.Import("archive/1", Facts, null), RecordedGame.Imported), Is.True);
        });
    }

    [Test]
    public void Inline_ReplacesEveryCall_NestedLambdasIncluded_WithTheRule()
    {
        var inlined = ModelRules.Inline((List<RecordedGame> games) =>
            games.Count(g => ModelRules.Meets(g, RecordedGame.Manual)) * 10 + (ModelRules.Meets(games[0], RecordedGame.Imported) ? 1 : 0));

        var games = new List<RecordedGame> { RecordedGame.Import("archive/1", Facts, null), RecordedGame.EnterByHand(Facts), RecordedGame.EnterByHand(Facts) };
        Assert.Multiple(() =>
        {
            Assert.That(new MeetsCallFinder().Found(inlined), Is.False, "A query provider would not know the call");
            Assert.That(inlined.Compile()(games), Is.EqualTo(21));
        });
    }

    [Test]
    public void Inline_LeavesOtherCallsAlone()
    {
        var inlined = ModelRules.Inline((RecordedGame g) => Math.Max(g.GameVersionID, 3));

        Assert.That(inlined.Compile()(RecordedGame.EnterByHand(new RecordedGameFacts(7))), Is.EqualTo(7));
    }

    private sealed class MeetsCallFinder : ExpressionVisitor
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
}
