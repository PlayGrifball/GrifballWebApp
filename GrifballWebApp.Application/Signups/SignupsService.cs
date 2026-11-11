using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Signups;

public class SignupsService
{
    private readonly IApplicationDbContext _context;
    private readonly TimeProvider _time;
    public SignupsService(IApplicationDbContext grifballContext, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = grifballContext;
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
        // The player's timeslots, read through their signup
        var chosen = (await _context.SeasonSignups
            .Where(SeasonSignup.Of(seasonID, userID))
            .SelectMany(x => x.SignupAvailability.Select(a => a.AvailabilityOptionID))
            .ToListAsync(ct)).ToHashSet();

        var timeslots = await _context.Seasons.Timeslots()
            .Where(x => x.SeasonID == seasonID)
            .Select(x => x.AvailabilityOption)
            .Select(x => new TimeslotDto()
            {
                OptionID = x.AvailabilityOptionID,
                DayOfWeek = x.DayOfWeek,
                Time = x.Time,
            }).ToArrayAsync(ct);

        var offsetTimespan = TimeSpan.FromMinutes(offset);

        foreach (var item in timeslots)
        {
            item.IsChecked = chosen.Contains(item.OptionID);
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
            .Where(SeasonSignup.Of(seasonID, userID))
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
    /// Signs the player up for the season, or changes their signup (<see cref="SeasonSignup.SignUp"/>, <see cref="SeasonSignup.Change"/>)
    /// </summary>
    /// <exception cref="Exception">Thrown when the season doesn't exist, or a timeslot chosen isn't the season's</exception>
    /// <exception cref="SignupsClosedException">Throw when season signups have been closed</exception>
    /// <exception cref="SignupNeedsGamertagException">Thrown when the player has no gamertag set (the site and Discord alike)</exception>
    public async Task UpsertSignup(SignupRequestDto dto, CancellationToken ct = default)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        Season season = await _context.Seasons.Include(x => x.SeasonAvailability).Where(x => x.SeasonID == dto.SeasonID).FirstOrDefaultAsync(ct) ?? throw new Exception("Season does not exist");

        SeasonSignup? seasonSignup = await _context.SeasonSignups
            .Include(x => x.SignupAvailability)
            .Where(SeasonSignup.Of(dto.SeasonID, dto.UserID))
            .FirstOrDefaultAsync(ct);

        var choices = new SignupChoices(dto.TeamName, dto.WillCaptain, dto.RequiresAssistanceDrafting,
            dto.Timeslots.Where(x => x.IsChecked).Select(x => x.OptionID).ToArray());
        var outcome = seasonSignup is null
            ? SeasonSignup.SignUp(season, dto.UserID, now, choices)
            : seasonSignup.Change(season, now, choices);

        // Refused for a captain's team name too long first, then while signups are closed, then without a gamertag (the
        // player's, not the signup's rule), then for a timeslot
        if (outcome.Result is SignupResult.TeamNameTooLong)
            throw new SignupTeamNameTooLongException();
        if (outcome.Result is SignupResult.SignupsClosed)
            throw new SignupsClosedException();
        if (!await _context.Users.AnyAsync(u => u.Id == dto.UserID && u.XboxUserID != null, ct))
            throw new SignupNeedsGamertagException();

        switch (outcome.Result)
        {
            case SignupResult.TimeslotsNotOffered:
                throw new Exception($"Cannot add options for season {dto.SeasonID}. The following are not valid {string.Join(',', outcome.RefusedTimeslots!)}");
            case SignupResult.SignedUp:
                _context.SeasonSignups.Add(outcome.Signup!);
                break;
        }

        await _context.SaveChangesAsync(ct);
    }
}
