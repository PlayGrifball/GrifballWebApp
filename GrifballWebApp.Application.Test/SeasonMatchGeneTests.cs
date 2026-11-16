using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Scheduler;

namespace GrifballWebApp.Application.Test;

[TestFixture]
public class SeasonMatchGeneTests
{
    private static readonly DateTime Monday20 = new(2024, 1, 1, 20, 0, 0); // 2024-01-01 is a Monday

    private static SeasonMatchGene Gene(int id, int home, int away, DateTime at) =>
        new() { SeasonMatchID = id, HomeTeamID = home, AwayTeamID = away, ScheduledTime = at };

    [Test]
    public void EndsAt_IsOneTickBeforeThirtyMinutes()
    {
        var g = Gene(1, 1, 2, Monday20);
        Assert.That(g.EndsAt, Is.EqualTo(Monday20.AddMinutes(30).AddTicks(-1)));
    }

    [TestCase(1, 2, 0, true, Description = "same teams, same time")]
    [TestCase(2, 3, 15, true, Description = "shares team 2 (as home), overlapping time")]
    [TestCase(3, 1, 29, true, Description = "shares team 1 (as away), overlapping time")]
    [TestCase(1, 5, 0, true, Description = "shares home team")]
    [TestCase(5, 2, 0, true, Description = "shares away team")]
    [TestCase(1, 2, 30, false, Description = "back to back is not an overlap")]
    [TestCase(1, 2, -30, false, Description = "back to back before is not an overlap")]
    [TestCase(3, 4, 0, false, Description = "different teams at the same time")]
    public void HasOverlap(int home, int away, int minutesOffset, bool expected)
    {
        var a = Gene(1, 1, 2, Monday20);
        var b = Gene(2, home, away, Monday20.AddMinutes(minutesOffset));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.HasOverlap(b), Is.EqualTo(expected));
            Assert.That(b.HasOverlap(a), Is.EqualTo(expected), "overlap is symmetric");
        }
    }

    [Test]
    public void GetOverlap_ExcludesSelfAndNonOverlapping()
    {
        var a = Gene(1, 1, 2, Monday20);
        var b = Gene(2, 2, 3, Monday20.AddMinutes(10));
        var c = Gene(3, 4, 5, Monday20);
        var list = new List<SeasonMatchGene> { a, b, c };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.GetOverlap(list), Is.EqualTo(new[] { b }));
            Assert.That(a.AnyOverlap(list), Is.True);
            Assert.That(c.AnyOverlap(list), Is.False);
            Assert.That(a.AnyOverlap([a]), Is.False);
        }
    }
}
