using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Test;

/// <summary>
/// Shared seeding helpers for the SeasonMatchService tests.
/// </summary>
internal static class SeasonMatchTestData
{
    public static readonly long[] HomeXbox = [101, 102, 103, 104];
    public static readonly long[] AwayXbox = [201, 202, 203, 204];
    public static readonly long[] OtherXbox = [301, 302, 303, 304, 305, 306, 307, 308];

    public record Seeded(Season Season, Team Home, Team Away, SeasonMatch SeasonMatch);

    /// <summary>
    /// Creates a season, two teams with four players each (with xbox accounts), plus a pool of unrelated xbox users,
    /// and one season match between the teams.
    /// </summary>
    public static async Task<Seeded> SeedAsync(GrifballContext context, int bestOf = 3, bool playersHaveXbox = true)
    {
        var season = new Season { SeasonName = "Cov Season", SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        var home = new Team { SeasonID = season.SeasonID, TeamName = "Home Team" };
        var away = new Team { SeasonID = season.SeasonID, TeamName = "Away Team" };
        context.Teams.AddRange(home, away);
        await context.SaveChangesAsync();

        foreach (var id in HomeXbox)
            context.TeamPlayers.Add(new TeamPlayer { TeamID = home.TeamID, User = NewUser(id, playersHaveXbox) });
        foreach (var id in AwayXbox)
            context.TeamPlayers.Add(new TeamPlayer { TeamID = away.TeamID, User = NewUser(id, playersHaveXbox) });
        foreach (var id in OtherXbox)
            context.XboxUsers.Add(new XboxUser { XboxUserID = id, Gamertag = $"GT{id}" });
        if (playersHaveXbox is false)
        {
            foreach (var id in HomeXbox.Concat(AwayXbox))
                context.XboxUsers.Add(new XboxUser { XboxUserID = id, Gamertag = $"GT{id}" });
        }
        await context.SaveChangesAsync();

        var seasonMatch = new SeasonMatch
        {
            SeasonID = season.SeasonID,
            HomeTeamID = home.TeamID,
            AwayTeamID = away.TeamID,
            BestOf = bestOf,
        };
        context.SeasonMatches.Add(seasonMatch);
        await context.SaveChangesAsync();

        return new Seeded(season, home, away, seasonMatch);
    }

    private static User NewUser(long xboxId, bool withXbox)
    {
        var user = new User { UserName = $"user{xboxId}", DisplayName = $"User {xboxId}" };
        if (withXbox)
            user.XboxUser = new XboxUser { XboxUserID = xboxId, Gamertag = $"GT{xboxId}" };
        return user;
    }

    /// <summary>
    /// Adds an infinite match with two teams (TeamID 0 and 1).
    /// </summary>
    public static async Task<Match> AddMatchAsync(GrifballContext context, DateTime start,
        long[] team0, Outcomes team0Outcome, long[] team1, Outcomes team1Outcome, int team0Score = 5, int team1Score = 3)
    {
        var matchID = Guid.NewGuid();
        var match = new Match
        {
            MatchID = matchID,
            StartTime = start,
            EndTime = start.AddMinutes(10),
            Duration = TimeSpan.FromMinutes(10),
            MatchTeams = new List<MatchTeam>
            {
                new()
                {
                    MatchID = matchID, TeamID = 0, Score = team0Score, Outcome = team0Outcome,
                    MatchParticipants = team0.Select(x => new MatchParticipant { MatchID = matchID, TeamID = 0, XboxUserID = x, Kills = (int)(x % 10), Deaths = 1, Score = (int)(x % 10) }).ToList(),
                },
                new()
                {
                    MatchID = matchID, TeamID = 1, Score = team1Score, Outcome = team1Outcome,
                    MatchParticipants = team1.Select(x => new MatchParticipant { MatchID = matchID, TeamID = 1, XboxUserID = x, Kills = (int)(x % 10), Deaths = 2, Score = 0 }).ToList(),
                },
            },
        };
        context.Matches.Add(match);
        await context.SaveChangesAsync();
        return match;
    }

}
