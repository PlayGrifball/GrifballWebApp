using GrifballWebApp.Application.Jobs;

namespace GrifballWebApp.Application.Scheduler;

/// <summary>
/// Working out a season's schedule recommendation, a background job one season at a time, as the scheduler page starts it: its
/// state, and queueing a run. Infrastructure's ScheduleRecommendationJob does the work.
/// </summary>
public interface IScheduleRecommendationRuns
{
    Task<OnDemandJobStatusDto> Status(int seasonID, CancellationToken ct = default);

    Task<OnDemandJobStatusDto> Queue(int seasonID, string? requestedBy, int? requestedByUserID, CancellationToken ct = default);
}
