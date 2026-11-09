using System.Linq.Expressions;
using GrifballWebApp.Database;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Test;

/// <summary>A model's rule used inside a query (<see cref="ModelRules.Apply"/>, <see cref="ModelRules.Inline"/>) and on a
/// loaded object (<see cref="ModelRules.Evaluate"/>).</summary>
[TestFixture]
public class ModelRulesTests
{
    private record Source(int A, int B, IReadOnlyList<int> Items);

    private class Row
    {
        public int A { get; set; }
        public int B { get; set; }
        public List<int> Items { get; set; } = [];
    }

    private static readonly Expression<Func<Source, bool>> IsBig = s => s.A > 10;

    private static readonly Expression<Func<Source, IEnumerable<int>>> EvenItems = s => s.Items.Where(i => i % 2 == 0);

    private static Expression<Func<Source, bool>> Exceeds(int limit) => s => s.A + s.B > limit;

    /// <summary>A rule that uses another: inlined too.</summary>
    private static readonly Expression<Func<Source, bool>> BigWithEvens = s => s.Apply(IsBig) && s.Apply(EvenItems).Any();

    [Test]
    public void Apply_OutsideAnInlinedQuery_Throws()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => new Source(11, 0, []).Apply(IsBig));

        Assert.That(thrown!.Message, Does.Contain(nameof(ModelRules.Inline)));
    }

    [Test]
    public void Inline_PutsEachRuleInPlaceOfItsApply_AFieldsAMethodsAndARuleInARule()
    {
        var limit = 3;
        var inlined = ModelRules.Inline((Source s) => new Row
        {
            A = s.Apply(IsBig) ? 1 : 0,
            B = new Source(s.B, 0, s.Items).Apply(Exceeds(limit)) ? 1 : 0,
            Items = s.Apply(BigWithEvens) ? s.Apply(EvenItems).ToList() : new List<int>(),
        });
        var compiled = inlined.Compile();
        var big = compiled(new Source(11, 4, [1, 2, 3, 4]));
        var small = compiled(new Source(1, 3, [2]));

        Assert.Multiple(() =>
        {
            Assert.That(inlined.ToString(), Does.Not.Contain(nameof(ModelRules.Apply)), "Only the rules' own expressions are left for the database");
            Assert.That(inlined.ToString(), Does.Contain("s.A > 10"));
            Assert.That((big.A, big.B), Is.EqualTo((1, 1)));
            Assert.That(big.Items, Is.EqualTo(new[] { 2, 4 }), "A rule of any result");
            Assert.That((small.A, small.B, small.Items.Count), Is.EqualTo((0, 0, 0)));
        });
    }

    [Test]
    public void Evaluate_RunsTheRuleOnALoadedObject_CompilingItOnce()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModelRules.Evaluate(new Source(11, 0, []), IsBig), Is.True);
            Assert.That(ModelRules.Evaluate(new Source(1, 0, []), IsBig), Is.False, "The cached rule, on another object");
            Assert.That(ModelRules.Evaluate(new Source(1, 0, [1, 2]), EvenItems), Is.EqualTo(new[] { 2 }));
        });
    }
}
