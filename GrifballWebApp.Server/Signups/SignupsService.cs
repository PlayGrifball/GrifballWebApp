using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Signups;

public class SignupsService
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;
    public SignupsService(GrifballContext grifballContext, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = grifballContext;
    }
    public Task<SignupDateInfoDto[]> GetSignupDateInfo(int personID, CancellationToken ct)
    {
        // Signups opening within 30 days (by date) and closed no more than 3 days ago. Day boundaries
        // worked out here rather than with DATEDIFF, which only SQL Server has.
        var today = _time.GetUtcNow().UtcDateTime.Date;
        var opensBefore = today.AddDays(31);
        var closesFrom = today.AddDays(-3);
        return _context.Seasons
            .Where(s => s.SignupsOpen < opensBefore && s.SignupsClose >= closesFrom)
            .Select(s => new SignupDateInfoDto()
            {
                SeasonID = s.SeasonID,
                IsSignedUp = s.SeasonSignups.Any(x => x.UserID == personID),
                SignupsClose = s.SignupsClose,
                SignupsOpen = s.SignupsOpen,
            })
            .ToArrayAsync(ct);
    }

    public Task<List<SignupResponseDto>> GetSignups(int seasonID, CancellationToken ct = default)
    {
        return _context.SeasonSignups
            .Where(signup => signup.SeasonID == seasonID)
            .Select(x => new SignupResponseDto()
            {
                SeasonID = seasonID,
                UserID = x.UserID,
                PersonName = UserNames.Pick(x.User.XboxUser!.Gamertag, x.User.DiscordUser!.DiscordUsername, x.User.DisplayName, x.User.UserName),
                Timestamp = x.Timestamp,
                TeamName = x.TeamName,
                WillCaptain = x.WillCaptain,
                RequiresAssistanceDrafting = x.RequiresAssistanceDrafting,
                Region = x.User.Region != null ? x.User.Region.RegionName : null,
            }).AsNoTracking().AsSplitQuery().ToListAsync(ct);
    }
    private static int MathMod(int a, int b)
    {
        return (Math.Abs(a * b) + a) % b;
    }

    public async Task<TimeslotDto[]> GetTimeslots(int seasonID, int offset, int userID, CancellationToken ct = default)
    {
        var signupID = await _context.SeasonSignups
            .Where(signup => signup.SeasonID == seasonID && signup.UserID == userID)
            .Select(x => x.SeasonSignupID)
            .FirstOrDefaultAsync(ct);

        var timeslots = await _context.SeasonAvailability
            .Where(x => x.SeasonID == seasonID)
            .Select(x => x.AvailabilityOption)
            .Select(x => new TimeslotDto()
            {
                OptionID = x.AvailabilityOptionID,
                DayOfWeek = x.DayOfWeek,
                Time = x.Time,
                IsChecked = x.SignupAvailability.Any(y => y.SeasonSignupID == signupID),
            }).ToArrayAsync(ct);

        var offsetTimespan = TimeSpan.FromMinutes(offset);

        foreach (var item in timeslots)
        {
            item.Time = item.Time.Add(offsetTimespan, out var wrappedDays);
            item.DayOfWeek = (DayOfWeek)MathMod((int)item.DayOfWeek + wrappedDays, 7);
        }

        timeslots = timeslots.OrderBy(x => x.DayOfWeek).ToArray();
        return timeslots;
    }

    public async Task<SignupResponseDto?> GetSignup(int seasonID, int offset, int userID, CancellationToken ct = default)
    {
        var timeslots = await GetTimeslots(seasonID, offset, userID, ct);

        return await _context.SeasonSignups
            .Where(signup => signup.SeasonID == seasonID && signup.UserID == userID)
            .Select(x => new SignupResponseDto()
            {
                SeasonID = seasonID,
                UserID = x.UserID,
                PersonName = UserNames.Pick(x.User.XboxUser!.Gamertag, x.User.DiscordUser!.DiscordUsername, x.User.DisplayName, x.User.UserName),
                Timestamp = x.Timestamp,
                TeamName = x.TeamName,
                WillCaptain = x.WillCaptain,
                RequiresAssistanceDrafting = x.RequiresAssistanceDrafting,
                Region = x.User.Region != null ? x.User.Region.RegionName : null,
                Timeslots = timeslots,
            }).AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Upserts a signup for the given player and season
    /// </summary>
    /// <param name="dto"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    /// <exception cref="SignupsClosedException">Throw when season signups have been closed</exception>
    /// <exception cref="SignupNeedsGamertagException">Thrown when the player has no gamertag set (the site and Discord alike)</exception>
    public async Task UpsertSignup(SignupRequestDto dto, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        Season season = await _context.Seasons.Where(x => x.SeasonID == dto.SeasonID).FirstOrDefaultAsync(ct) ?? throw new Exception("Season does not exist");

        if (!season.SignupsAreOpenAt(now))
            throw new SignupsClosedException();

        if (!await _context.Users.AnyAsync(u => u.Id == dto.UserID && u.XboxUserID != null, ct))
            throw new SignupNeedsGamertagException();

        SeasonSignup? seasonSignup = await _context.SeasonSignups
            .Include(x => x.SignupAvailability)
            .Where(signup => signup.SeasonID == dto.SeasonID && signup.UserID == dto.UserID)
            .FirstOrDefaultAsync(ct);

        if (seasonSignup is null)
        {
            seasonSignup = new SeasonSignup();
            await _context.SeasonSignups.AddAsync(seasonSignup, ct);
        }

        seasonSignup.UserID = dto.UserID;
        seasonSignup.SeasonID = dto.SeasonID;
        seasonSignup.Timestamp = now;
        // Stored trimmed: a franchise is its teams of one name across seasons (TeamHistoryService)
        seasonSignup.TeamName = dto.TeamName?.Trim();
        seasonSignup.WillCaptain = dto.WillCaptain;
        seasonSignup.RequiresAssistanceDrafting = dto.RequiresAssistanceDrafting;

        var toRemove = seasonSignup.SignupAvailability.Where(x => dto.Timeslots.Any(y => y.OptionID == x.AvailabilityOptionID && y.IsChecked) is false).ToArray();
        foreach (var item in toRemove)
        {
            seasonSignup.SignupAvailability.Remove(item);
        }

        var toAdd = dto.Timeslots.Where(x => x.IsChecked && seasonSignup.SignupAvailability.Any(y => y.AvailabilityOptionID == x.OptionID) is false)
            .Select(x => x.OptionID).ToArray();

        var validOptions = await _context.SeasonAvailability.Where(x => x.SeasonID == dto.SeasonID)
            .Select(x => x.AvailabilityOptionID)
            .ToArrayAsync(ct);
        var invalidOptions = toAdd.Where(x => validOptions.Contains(x) is false);
        if (invalidOptions.Any())
            throw new Exception($"Cannot add options for season {dto.SeasonID}. The following are not valid {string.Join(',', invalidOptions)}");

        foreach (var item in toAdd)
        {
            seasonSignup.SignupAvailability.Add(new SignupAvailability()
            {
                AvailabilityOptionID = item,
            });
        }

        await _context.SaveChangesAsync(ct);
    }
}
