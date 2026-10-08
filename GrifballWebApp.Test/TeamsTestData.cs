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
        var season = new Season
        {
            SeasonName = name,
            SeasonStart = new DateTime(2025, 1, 1),
            SeasonEnd = new DateTime(2025, 3, 1),
            CaptainsLocked = captainsLocked,
        };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }

    private static long _nextExternalId = 100_000;

    public static async Task<User> AddUser(this GrifballContext context, string userName, string? displayName = null,
        string? gamertag = null, string? discordName = null, bool commissioner = false)
    {
        var user = new User { UserName = userName, DisplayName = displayName };
        if (gamertag is not null)
            user.XboxUser = new XboxUser { XboxUserID = Interlocked.Increment(ref _nextExternalId), Gamertag = gamertag };
        if (discordName is not null)
            user.DiscordUser = new DiscordUser { DiscordUserID = Interlocked.Increment(ref _nextExternalId), DiscordUsername = discordName };
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
        var signup = new SeasonSignup { SeasonID = season.SeasonID, UserID = user.Id, Timestamp = new DateTime(2024, 12, 1), TeamName = teamName };
        context.SeasonSignups.Add(signup);
        await context.SaveChangesAsync();
        return signup;
    }

    /// <summary>Creates a team captained by <paramref name="captain"/> plus non-captain players in the given draft-round order.</summary>
    public static async Task<Team> AddTeam(this GrifballContext context, Season season, string teamName, User captain, int captainOrder, params User[] players)
    {
        var team = new Team { SeasonID = season.SeasonID, TeamName = teamName };
        var captainTp = new TeamPlayer { UserID = captain.Id, DraftCaptainOrder = captainOrder };
        team.TeamPlayers.Add(captainTp);
        var round = 1;
        foreach (var p in players)
            team.TeamPlayers.Add(new TeamPlayer { UserID = p.Id, DraftRound = round++ });
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        team.Captain = captainTp;
        await context.SaveChangesAsync();
        return team;
    }

    /// <summary>Creates a season that starts now and runs for 30 days.</summary>
    public static async Task<Season> AddCurrentSeason(this GrifballContext context, string name = "Test Season")
    {
        var season = new Season { SeasonName = name, SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        return season;
    }

    /// <summary>Creates a team whose captain is a new user with the given gamertag and Xbox user id.</summary>
    public static async Task<Team> AddTeamWithCaptain(this GrifballContext context, Season season, string gamertag, long xuid)
    {
        var user = new User { UserName = "user_" + gamertag, XboxUser = new XboxUser { XboxUserID = xuid, Gamertag = gamertag } };
        var team = new Team { SeasonID = season.SeasonID, TeamName = "Team " + gamertag };
        context.Users.Add(user);
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        var tp = new TeamPlayer { TeamID = team.TeamID, UserID = user.Id };
        context.TeamPlayers.Add(tp);
        await context.SaveChangesAsync();
        team.CaptainID = tp.TeamPlayerID;
        await context.SaveChangesAsync();
        return team;
    }
}
