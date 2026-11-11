using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Application.Test;

/// <summary>The same timeslots every day, for the scheduler's tests.</summary>
internal static class AvailabilityTestData
{
    internal static AvailabilityOption[] EveryDay(params int[] hours) => EveryDayFor([], hours);

    /// <summary>Every day at <paramref name="hours"/>, each timeslot one <paramref name="teamIDs"/> can all play in.</summary>
    internal static AvailabilityOption[] EveryDayFor(int[] teamIDs, params int[] hours) =>
        Enum.GetValues<DayOfWeek>()
            .SelectMany(d => hours.Select(h => AvailabilityOption.Seed(dayOfWeek: d, time: new TimeOnly(h, 0),
                teamAvailability: teamIDs.Select(t => TeamAvailability.Seed(teamID: t)))))
            .ToArray();
}
