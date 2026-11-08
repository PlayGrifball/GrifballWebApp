using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

/// <summary>Seeding helpers for seasons, users, signups and teams.</summary>
internal static class TeamsTestData
{
    public const int CommissionerRoleId = 2;

    public static async Task<Season> AddSeason(this GrifballContext context, string name = "Season", bool captainsLocked = false)
    {
        var season = Season.Seed(seasonName: name, captainsLocked: captainsLocked).WithDates(seasonStart: new DateTime(2025, 1, 1), seasonEnd: new DateTime(2025, 3, 1));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }

    private static long _nextExternalId = 100_000;

    public static async Task<User> AddUser(this GrifballContext context, string userName, string? displayName = null,
        string? gamertag = null, string? discordName = null, bool commissioner = false)
    {
        var user = User.Seed(userName: userName, displayName: displayName);
        if (gamertag is not null)
            user.TieToXbox(XboxUser.FromXbox(Interlocked.Increment(ref _nextExternalId), gamertag));
        if (discordName is not null)
            user.LinkDiscord(DiscordUser.FromDiscord(Interlocked.Increment(ref _nextExternalId), discordName));
        context.Users.Add(user);
        await context.SaveChangesAsync();
        if (commissioner)
        {
            context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = CommissionerRoleId });
            await context.SaveChangesAsync();
        }
        return user;
    }

    public static async Task<SeasonSignup> AddSignup(this GrifballContext context, Season season, User user, string? teamName = null)
    {
        var signup = SeasonSignup.Seed(season.SeasonID, user.Id, timestamp: new DateTime(2024, 12, 1), teamName: teamName);
        context.SeasonSignups.Add(signup);
        await context.SaveChangesAsync();
        return signup;
    }

    /// <summary>Creates a team captained by <paramref name="captain"/> plus non-captain players in the given draft-round order.</summary>
    public static async Task<Team> AddTeam(this GrifballContext context, Season season, string teamName, User captain, int captainOrder, params User[] players)
    {
        var team = Team.Seed(seasonID: season.SeasonID, teamName: teamName);
        team.SeedPlayer(captain.Id, draftCaptainOrder: captainOrder);
        var round = 1;
        foreach (var p in players)
            team.SeedPlayer(p.Id, draftRound: round++);
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        team.Captained(captain.Id);
        await context.SaveChangesAsync();
        return team;
    }

    /// <summary>Creates a season that starts now and runs for 30 days.</summary>
    public static async Task<Season> AddCurrentSeason(this GrifballContext context, string name = "Test Season")
    {
        var season = Season.Seed(seasonName: name).WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(30));
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }

    /// <summary>Creates a team whose captain is a new user with the given gamertag and Xbox user id.</summary>
    public static async Task<Team> AddTeamWithCaptain(this GrifballContext context, Season season, string gamertag, long xuid)
    {
        var user = User.Seed(userName: "user_" + gamertag, xboxUser: XboxUser.FromXbox(xuid, gamertag));
        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Team " + gamertag);
        context.Users.Add(user);
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        await context.AddPlayers(team, user.Id);
        team.Captained(user.Id);
        await context.SaveChangesAsync();
        return team;
    }

    /// <summary>
    /// The team captained by <paramref name="userID"/>, one of its saved roster, through <see cref="Team.SetCaptain"/>; a
    /// user not on its roster fails the test.
    /// </summary>
    public static Team Captained(this Team team, int userID)
    {
        if (team.SetCaptain(userID) is not TeamCaptainResult.Appointed)
            throw new InvalidOperationException($"Test team {team.TeamName}: {userID} is not on its roster");
        return team;
    }

    /// <summary>The users put on the team's roster (<see cref="Team.AddPlayer"/>) and saved; one already on it fails the test.</summary>
    public static async Task<Team> AddPlayers(this GrifballContext context, Team team, params int[] userIDs)
    {
        foreach (var userID in userIDs)
        {
            if (team.AddPlayer(userID) is not TeamRosterResult.Done)
                throw new InvalidOperationException($"Test team {team.TeamName}: {userID} is on its roster already");
        }
        await context.SaveChangesAsync();
        return team;
    }
}
