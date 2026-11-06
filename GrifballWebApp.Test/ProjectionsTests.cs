using System.Linq.Expressions;
using GrifballWebApp.Server.Extensions;

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
}
