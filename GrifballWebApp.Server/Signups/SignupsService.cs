using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.EventOrganizer;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Signups;

public class SignupsService
{
    private readonly GrifballContext _context;
    public SignupsService(GrifballContext grifballContext)
    {
        _context = grifballContext;
    }
    public Task<SignupDateInfoDto[]> GetSignupDateInfo(int personID, CancellationToken ct)
    {
        var n = DateTime.UtcNow;
        return _context.Seasons
            .Where(s => EF.Functions.DateDiffDay(s.SignupsOpen, n) >= -30 && EF.Functions.DateDiffDay(n, s.SignupsClose) >= -3)
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
                PersonID = x.UserID,
                PersonName = x.User.DisplayName,
                Timestamp = x.Timestamp,
                TeamName = x.TeamName,
                WillCaptain = x.WillCaptain,
                RequiresAssistanceDrafting = x.RequiresAssistanceDrafting,
            }).AsNoTracking().AsSplitQuery().ToListAsync(ct);
    }

    public Task<SignupResponseDto?> GetSignup(int seasonID, int personID, CancellationToken ct = default)
    {
        return _context.SeasonSignups
            .Where(signup => signup.SeasonID == seasonID && signup.UserID == personID)
            .Select(x => new SignupResponseDto()
            {
                SeasonID = seasonID,
                PersonID = x.UserID,
                PersonName = x.User.DisplayName,
                Timestamp = x.Timestamp,
                TeamName = x.TeamName,
                WillCaptain = x.WillCaptain,
                RequiresAssistanceDrafting = x.RequiresAssistanceDrafting,
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
    public async Task UpsertSignup(SignupRequestDto dto, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        Season season = await _context.Seasons.Where(x => x.SeasonID == dto.SeasonID).FirstOrDefaultAsync(ct) ?? throw new Exception("Season does not exist");

        if (!(season.SignupsOpen <= now && now <= season.SignupsClose))
            throw new SignupsClosedException();


        SeasonSignup? seasonSignup = await _context.SeasonSignups
            .Where(signup => signup.SeasonID == dto.SeasonID && signup.UserID == dto.PersonID)
            .FirstOrDefaultAsync(ct);

        if (seasonSignup is null)
        {
            seasonSignup = new SeasonSignup();
            await _context.SeasonSignups.AddAsync(seasonSignup, ct);
        }

        seasonSignup.UserID = dto.PersonID;
        seasonSignup.SeasonID = dto.SeasonID;
        seasonSignup.Timestamp = now;
        seasonSignup.TeamName = dto.TeamName;
        seasonSignup.WillCaptain = dto.WillCaptain;
        seasonSignup.RequiresAssistanceDrafting = dto.RequiresAssistanceDrafting;

        await _context.SaveChangesAsync(ct);
    }
}
