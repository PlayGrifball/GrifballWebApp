using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test.CovD;

internal static class SeedHelpers_d
{
    internal static async Task<Season> Season(GrifballContext ctx, string name = "CovD Season")
    {
        var season = new Season { SeasonName = name, SeasonStart = DateTime.UtcNow, SeasonEnd = DateTime.UtcNow.AddDays(30) };
        ctx.Seasons.Add(season);
        await ctx.SaveChangesAsync();
        return season;
    }

    /// <summary>Creates a team whose captain is a user with the given gamertag.</summary>
    internal static async Task<Team> TeamWithCaptain(GrifballContext ctx, Season season, string gamertag, long xuid)
    {
        var user = new User { UserName = "user_" + gamertag, XboxUser = new XboxUser { XboxUserID = xuid, Gamertag = gamertag } };
        var team = new Team { SeasonID = season.SeasonID, TeamName = "Team " + gamertag };
        ctx.Users.Add(user);
        ctx.Teams.Add(team);
        await ctx.SaveChangesAsync();
        var tp = new TeamPlayer { TeamID = team.TeamID, UserID = user.Id };
        ctx.TeamPlayers.Add(tp);
        await ctx.SaveChangesAsync();
        team.CaptainID = tp.TeamPlayerID;
        await ctx.SaveChangesAsync();
        return team;
    }
}
