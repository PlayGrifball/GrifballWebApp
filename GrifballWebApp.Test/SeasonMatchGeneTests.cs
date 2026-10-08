using GeneticSharp;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Scheduler;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
public class SeasonMatchGeneTests
{
    private static readonly DateTime Monday20 = new(2024, 1, 1, 20, 0, 0); // 2024-01-01 is a Monday

    private static SeasonMatchGene Gene(int id, int home, int away, DateTime at) =>
        new() { SeasonMatchID = id, HomeTeamID = home, AwayTeamID = away, ScheduledTime = at };

    private static AvailabilityOption Option(DayOfWeek day, int hour, params int[] teamIDs)
    {
        var option = new AvailabilityOption { DayOfWeek = day, Time = new TimeOnly(hour, 0) };
        foreach (var t in teamIDs)
            option.TeamAvailability.Add(new TeamAvailability { TeamID = t });
        return option;
    }

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

        Assert.That(a.HasOverlap(b), Is.EqualTo(expected));
        Assert.That(b.HasOverlap(a), Is.EqualTo(expected), "overlap is symmetric");
    }

    [Test]
    public void GetOverlap_ExcludesSelfAndNonOverlapping()
    {
        var a = Gene(1, 1, 2, Monday20);
        var b = Gene(2, 2, 3, Monday20.AddMinutes(10));
        var c = Gene(3, 4, 5, Monday20);
        var list = new List<SeasonMatchGene> { a, b, c };

        Assert.Multiple(() =>
        {
            Assert.That(a.GetOverlap(list), Is.EqualTo(new[] { b }));
            Assert.That(a.AnyOverlap(list), Is.True);
            Assert.That(c.AnyOverlap(list), Is.False);
            Assert.That(a.AnyOverlap([a]), Is.False);
        });
    }

    [Test]
    public void TeamAvailable_UsesOptionMatchingDayAndTime()
    {
        var options = new[]
        {
            Option(DayOfWeek.Monday, 20, 1),
            Option(DayOfWeek.Monday, 21, 2),
            Option(DayOfWeek.Tuesday, 20, 2),
        };
        var g = Gene(1, 1, 2, Monday20);

        Assert.Multiple(() =>
        {
            Assert.That(g.HomeTeamAvailable(options), Is.True);
            Assert.That(g.AwayTeamAvailable(options), Is.False);
            var later = Gene(1, 1, 2, Monday20.AddHours(1));
            Assert.That(later.HomeTeamAvailable(options), Is.False);
            Assert.That(later.AwayTeamAvailable(options), Is.True);
        });
    }

    [Test]
    public void TeamAvailable_NoOptionForSlot_Throws()
    {
        var g = Gene(1, 1, 2, Monday20.AddMinutes(30));
        var options = new[] { Option(DayOfWeek.Monday, 20, 1, 2) };

        Assert.That(Assert.Throws<Exception>(() => g.HomeTeamAvailable(options))!.Message, Is.EqualTo("Missing option"));
        Assert.That(Assert.Throws<Exception>(() => g.AwayTeamAvailable(options))!.Message, Is.EqualTo("Missing option"));
    }
}
