using System.Linq.Expressions;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Extensions;

namespace GrifballWebApp.Test;

/// <summary>A derived DTO's projection extending its base's (<see cref="Projections.Extend"/>).</summary>
[TestFixture]
public class ProjectionsTests
{
    private record Source(int A, int B);

    private class Base
    {
        public int A { get; set; }
        public int B { get; set; }
    }

    private class Derived : Base
    {
        public int C { get; set; }
    }

    private static readonly Expression<Func<Source, Base>> BaseFields = s => new Base { A = s.A, B = s.B };

    [Test]
    public void Extend_Should_SetTheBasesMembersAndTheDerivedOnes_TheDerivedWinning()
    {
        var derived = Projections.Extend(BaseFields, (Source s) => new Derived { B = s.B * 10, C = s.A + s.B }).Compile()(new Source(1, 2));

        Assert.That((derived.A, derived.B, derived.C), Is.EqualTo((1, 20, 3)));
    }

    [Test]
    public void Extend_Should_SetOnlyTheBasesMembers_When_TheDerivedSetsNone()
    {
        var derived = Projections.Extend(BaseFields, (Source s) => new Derived()).Compile()(new Source(1, 2));

        Assert.That((derived.A, derived.B, derived.C), Is.EqualTo((1, 2, 0)));
    }

    [Test]
    public void Extend_Should_Throw_When_EitherIsNotAMemberInitializer()
    {
        Assert.Throws<ArgumentException>(() => Projections.Extend(BaseFields, (Source s) => (Derived)null!));
        Assert.Throws<ArgumentException>(() => Projections.Extend((Source s) => (Base)new Derived(), (Source s) => new Derived { C = 1 }));
    }

    private static readonly Expression<Func<Source, bool>> IsBig = s => s.A > 10;

    private static Expression<Func<Source, bool>> Exceeds(int limit) => s => s.A + s.B > limit;

    [Test]
    public void Extend_InlinesTheRulesEitherApplies()
    {
        var extended = Projections.Extend((Source s) => new Base { A = s.Apply(IsBig) ? 1 : 0, B = s.B }, (Source s) => new Derived { C = s.Apply(Exceeds(5)) ? 1 : 0 });

        Assert.Multiple(() =>
        {
            Assert.That(extended.ToString(), Does.Not.Contain(nameof(ModelRules.Apply)));
            var derived = extended.Compile()(new Source(11, 0));
            Assert.That((derived.A, derived.B, derived.C), Is.EqualTo((1, 0, 1)));
        });
    }
}
