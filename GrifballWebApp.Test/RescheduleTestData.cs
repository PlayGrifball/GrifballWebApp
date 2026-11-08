using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Reschedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCord.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using System.Security.Claims;

namespace GrifballWebApp.Test;

internal static class RescheduleTestData
{
    public record Seeded(Season Season, Team Home, Team Away, User HomeCaptain, User AwayCaptain, User Requester, SeasonMatch Match);

    public static async Task<User> AddUser(GrifballContext context, string name, long? xboxId = null, long? discordId = null, string? displayName = null)
    {
        var user = User.Seed(userName: name, displayName: displayName);
        if (xboxId is not null)
            user.TieToXbox(XboxUser.FromXbox(xboxId.Value, $"GT_{name}"));
        if (discordId is not null)
            user.LinkDiscord(new GrifballWebApp.Database.Models.DiscordUser { DiscordUserID = discordId.Value, DiscordUsername = $"dc_{name}" });
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    public static async Task<Team> AddTeamWithCaptain(GrifballContext context, Season season, string name, User captain)
    {
        var team = new Team { SeasonID = season.SeasonID, TeamName = name };
        context.Teams.Add(team);
        await context.SaveChangesAsync();
        var tp = new TeamPlayer { TeamID = team.TeamID, UserID = captain.Id };
        context.TeamPlayers.Add(tp);
        await context.SaveChangesAsync();
        team.CaptainID = tp.TeamPlayerID;
        await context.SaveChangesAsync();
        return team;
    }

    public static async Task<Seeded> SeedAsync(GrifballContext context, DateTime? scheduled = null, bool captainsHaveDiscord = true)
    {
        var season = Season.Seed(seasonName: "Reschedule Season");
        context.Seasons.Add(season);
        await context.SaveChangesAsync();
        var homeCap = await AddUser(context, "homecap", 1001, captainsHaveDiscord ? 5001 : null);
        var awayCap = await AddUser(context, "awaycap", 1002, captainsHaveDiscord ? 5002 : null);
        var requester = await AddUser(context, "requester", 1003, 5003);
        var home = await AddTeamWithCaptain(context, season, "Home", homeCap);
        var away = await AddTeamWithCaptain(context, season, "Away", awayCap);
        // The requester plays for the home team: only players on either team (or a commissioner) may ask.
        context.TeamPlayers.Add(new TeamPlayer { TeamID = home.TeamID, UserID = requester.Id });
        await context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3, scheduledTime: scheduled);
        context.SeasonMatches.Add(match);
        await context.SaveChangesAsync();
        return new Seeded(season, home, away, homeCap, awayCap, requester, match);
    }

    /// <summary>Gives the user the Commissioner role.</summary>
    public static async Task MakeCommissioner(GrifballContext context, User user)
    {
        var role = await context.Roles.SingleAsync(x => x.Name == "Commissioner");
        context.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await context.SaveChangesAsync();
    }

    public static async Task<MatchReschedule> AddReschedule(GrifballContext context, int seasonMatchID, int requesterId,
        RescheduleStatus status = RescheduleStatus.Pending, DateTime? newTime = null, DateTime? requestedAt = null, ulong? threadId = null, DateTime? original = null)
    {
        var r = MatchReschedule.Request(seasonMatchID, original, newTime, "Cannot make it", requesterId, requestedAt ?? DateTime.UtcNow);
        if (threadId is { } thread)
            r.AttachThread(thread);
        context.MatchReschedules.Add(r);
        if (status != RescheduleStatus.Pending)
            Decided(context, r, status, requesterId);
        await context.SaveChangesAsync();
        return r;
    }

    /// <summary>
    /// Marks a seeded request approved or rejected by <paramref name="byUserID"/>, as stored, without what a decision does
    /// to its match (<see cref="MatchReschedule.Decide"/> is tested on its own).
    /// </summary>
    public static void Decided(GrifballContext context, MatchReschedule reschedule, RescheduleStatus status, int byUserID)
    {
        var entry = context.Entry(reschedule);
        entry.Property(x => x.Status).CurrentValue = status;
        entry.Property(x => x.ApprovedByUserID).CurrentValue = byUserID;
        entry.Property(x => x.ProcessedAt).CurrentValue = DateTime.UtcNow;
    }
}
