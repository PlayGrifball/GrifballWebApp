using System.Net;
using System.Reflection;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server;
using GrifballWebApp.Server.Signups;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetCord;
using NetCord.JsonModels;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using User = GrifballWebApp.Domain.Players.User;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;
using SignupButtonInteractions = GrifballWebApp.Server.Signups.ButtonInteractions;

namespace GrifballWebApp.Integration.Test;

internal static class SignupTestData
{
    internal static async Task<Season> OpenSeason(GrifballContext ctx, bool open = true, string name = "Signup Season")
    {
        var now = DateTime.UtcNow;
        var season = Season.Seed(seasonName: name).WithDates(seasonStart: now.AddDays(10), seasonEnd: now.AddDays(40), signupsOpen: open ? now.AddDays(-5) : now.AddDays(-10), signupsClose: open ? now.AddDays(5) : now.AddDays(-1));
        ctx.Seasons.Add(season);
        await ctx.SaveChangesAsync();
        return season;
    }

    /// <summary>Adds availability options (with odd minutes so they never clash with seeded options) and attaches them to the season.</summary>
    internal static async Task<AvailabilityOption[]> Options(GrifballContext ctx, Season season, params (DayOfWeek day, TimeOnly time)[] slots)
    {
        var options = slots.Select(s => AvailabilityOption.At(s.day, s.time)).ToArray();
        ctx.Availability.AddRange(options);
        await ctx.SaveChangesAsync();
        ctx.Set<SeasonAvailability>().AddRange(options.Select(o => SeasonAvailability.Seed(seasonID: season.SeasonID, availabilityOptionID: o.AvailabilityOptionID)));
        await ctx.SaveChangesAsync();
        return options;
    }
}
