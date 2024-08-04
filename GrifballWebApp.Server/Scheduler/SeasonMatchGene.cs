

using GeneticSharp;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Scheduler;

public struct SeasonMatchGene
{
    public required int SeasonMatchID { get; set; }
    public required int HomeTeamID { get; set; }
    public required int AwayTeamID { get; set; }
    public required DateTime ScheduledTime { get; set; }
    public DateTime EndsAt => ScheduledTime.AddMinutes(30).AddTicks(-1);
    //public TimeSpan Duration => new TimeSpan(EndsAt.Ticks - ScheduledTime.Ticks);
    private bool SameTeamPlaying(SeasonMatchGene other)
    {
        return HomeTeamID == other.HomeTeamID ||
            HomeTeamID == other.AwayTeamID ||
            AwayTeamID == other.HomeTeamID ||
            AwayTeamID == other.AwayTeamID;
    }
    public bool HasOverlap(SeasonMatchGene other) => ScheduledTime < other.EndsAt && EndsAt > other.ScheduledTime && SameTeamPlaying(other);

    public IEnumerable<SeasonMatchGene> GetOverlap(List<SeasonMatchGene> list)
    {
        var self = this;
        return list.Except([this])
            .Where(self.HasOverlap);
    }
    public bool AnyOverlap(List<SeasonMatchGene> list) => GetOverlap(list).Any();

    public bool HomeTeamAvailable(AvailabilityGridOption[] options)
    {
        var self = this;
        var option = options.FirstOrDefault(x => x.DayOfWeek == self.ScheduledTime.DayOfWeek && x.Time == TimeOnly.FromDateTime(self.ScheduledTime));

        if (option is null)
            throw new Exception("Missing option");

        return option.TeamAvailability.Any(x => x.TeamID == self.HomeTeamID);
    }

    public bool AwayTeamAvailable(AvailabilityGridOption[] options)
    {
        var self = this;
        var option = options.FirstOrDefault(x => x.DayOfWeek == self.ScheduledTime.DayOfWeek && x.Time == TimeOnly.FromDateTime(self.ScheduledTime));

        if (option is null)
            throw new Exception("Missing option");

        return option.TeamAvailability.Any(x => x.TeamID == self.AwayTeamID);
    }
}
