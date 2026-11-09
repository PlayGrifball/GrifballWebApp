using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Match = GrifballWebApp.Domain.Models.Match;

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
        var season = Season.Seed(seasonName: "Cov Season").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();

        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home Team");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away Team");
        context.Teams.AddRange(home, away);
        await context.SaveChangesAsync();

        foreach (var id in HomeXbox)
            home.SeedPlayer(user: NewUser(id, playersHaveXbox));
        foreach (var id in AwayXbox)
            away.SeedPlayer(user: NewUser(id, playersHaveXbox));
        foreach (var id in OtherXbox)
            context.XboxUsers.Add(XboxUser.FromXbox(id, $"GT{id}"));
        if (playersHaveXbox is false)
        {
            foreach (var id in HomeXbox.Concat(AwayXbox))
                context.XboxUsers.Add(XboxUser.FromXbox(id, $"GT{id}"));
        }
        await context.SaveChangesAsync();

        var seasonMatch = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: bestOf);
        context.SeasonMatches.Add(seasonMatch);
        await context.SaveChangesAsync();

        return new Seeded(season, home, away, seasonMatch);
    }

    private static User NewUser(long xboxId, bool withXbox)
    {
        var user = User.Seed(userName: $"user{xboxId}", displayName: $"User {xboxId}");
        if (withXbox)
            user.TieToXbox(XboxUser.FromXbox(xboxId, $"GT{xboxId}"));
        return user;
    }

    /// <summary>
    /// Adds an infinite match with two teams (TeamID 0 and 1).
    /// </summary>
    public static async Task<Match> AddMatchAsync(GrifballContext context, DateTime start,
        long[] team0, Outcomes team0Outcome, long[] team1, Outcomes team1Outcome, int team0Score = 5, int team1Score = 3)
    {
        var matchID = Guid.NewGuid();
        var match = Match.Seed(matchID, startTime: start, endTime: start.AddMinutes(10), duration: TimeSpan.FromMinutes(10));
        match.AddTeam(0, team0Score, team0Outcome);
        match.AddTeam(1, team1Score, team1Outcome);
        foreach (var x in team0)
            match.AddParticipant(0, x, new MatchParticipantStats { Kills = (int)(x % 10), Deaths = 1, Score = (int)(x % 10) });
        foreach (var x in team1)
            match.AddParticipant(1, x, new MatchParticipantStats { Kills = (int)(x % 10), Deaths = 2, Score = 0 });
        context.Matches.Add(match);
        await context.SaveChangesAsync();
        return match;
    }

}
