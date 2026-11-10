using GrifballWebApp.Application.Availability;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Test;

/// <summary>Two availability options are the same timeslot when their day and time are; a missing one is only the same as another missing one.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class AvailabilityComparerTests
{
    private static readonly IEqualityComparer<AvailabilityOption> Comparer = new AvailabilityComparer();

    [Test]
    public void Equals_ComparesTheDayAndTheTime_AndNulls()
    {
        var mondaySix = AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Comparer.Equals(null, null), Is.True);
            Assert.That(Comparer.Equals(null, mondaySix), Is.False);
            Assert.That(Comparer.Equals(mondaySix, null), Is.False);
            Assert.That(Comparer.Equals(mondaySix, AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(18, 0))), Is.True);
            Assert.That(Comparer.Equals(mondaySix, AvailabilityOption.At(DayOfWeek.Tuesday, new TimeOnly(18, 0))), Is.False);
            Assert.That(Comparer.Equals(mondaySix, AvailabilityOption.At(DayOfWeek.Monday, new TimeOnly(19, 0))), Is.False);
        }
    }
}
