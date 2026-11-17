using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// A season to schedule in: two weeks (1 to 14 June 2026), playing Mondays and Tuesdays at 20:00 Eastern. Reds' four players
/// can all play Mondays, two of them Tuesdays; Blues' four can play both. Reds vs Blues (<see cref="Match"/>) is unscheduled;
/// Blues vs Greens (<see cref="Other"/>) is on the first Tuesday. Each team's captain is its first player.
/// </summary>
internal sealed record SchedulingTestData(int SeasonID, int MondaysID, int TuesdaysID, Team Reds, Team Blues, Team Greens, SeasonMatch Match, SeasonMatch Other)
{
    /// <summary>"Now" in the tests: Monday 1 June 2026, noon UTC (EDT, UTC-4).</summary>
    public static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    /// <summary>Monday 1 June 20:00 Eastern.</summary>
    public static readonly DateTime Monday = new(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
    /// <summary>Tuesday 2 June 20:00 Eastern.</summary>
    public static readonly DateTime Tuesday = new(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);

    public static async Task<SchedulingTestData> SeedAsync(GrifballContext context)
    {
        var season = Season.Seed(seasonName: "S1");
        season.Schedule(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 5, 15, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 5, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        context.Seasons.Add(season);
        AvailabilityOption mondays = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Monday, time: new TimeOnly(20, 0)),
            tuesdays = AvailabilityOption.Seed(dayOfWeek: DayOfWeek.Tuesday, time: new TimeOnly(20, 0));
        context.Availability.AddRange(mondays, tuesdays);
        var users = Enumerable.Range(1, 9).Select(i => User.Register($"p{i}")).ToList();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        var seasonID = season.SeasonID;
        context.AddRange(SeasonAvailability.Seed(seasonID, mondays.AvailabilityOptionID), SeasonAvailability.Seed(seasonID, tuesdays.AvailabilityOptionID));

        var reds = Roster(context, seasonID, "Reds", users[0..4]);
        var blues = Roster(context, seasonID, "Blues", users[4..8]);
        var greens = Roster(context, seasonID, "Greens", users[8..9]);
        int[] both = [mondays.AvailabilityOptionID, tuesdays.AvailabilityOptionID], monday = [mondays.AvailabilityOptionID];
        context.SeasonSignups.AddRange(users.Select((u, i) => SeasonSignup.Seed(seasonID, u.Id, timeslots: i switch { < 2 => both, < 4 => monday, _ => both })));
        await context.SaveChangesAsync();
        // A captain only once the roster is saved (each points at the other)
        foreach (var (team, captain) in new[] { (reds, users[0]), (blues, users[4]), (greens, users[8]) })
            team.SetCaptain(captain.Id);
        await context.SaveChangesAsync();
        var match = SeasonMatch.Between(seasonID, reds.TeamID, blues.TeamID, bestOf: 3);
        var other = SeasonMatch.Between(seasonID, blues.TeamID, greens.TeamID, bestOf: 3);
        other.ScheduleAt(Tuesday);
        context.SeasonMatches.AddRange(match, other);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return new SchedulingTestData(seasonID, mondays.AvailabilityOptionID, tuesdays.AvailabilityOptionID, reds, blues, greens, match, other);
    }

    /// <summary>The user ID of <paramref name="team"/>'s captain (its first player).</summary>
    public static int CaptainOf(Team team) => team.TeamPlayers.First().UserID;

    private static Team Roster(GrifballContext context, int seasonID, string name, IEnumerable<User> players)
    {
        var list = players.ToList();
        var team = Team.Drafted(seasonID, name, list[0].Id, 1);
        foreach (var player in list.Skip(1))
            team.Draft(player.Id);
        context.Teams.Add(team);
        return team;
    }
}
