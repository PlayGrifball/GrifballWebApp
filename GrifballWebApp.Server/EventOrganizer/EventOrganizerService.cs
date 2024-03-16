using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.EventOrganizer;

public class EventOrganizerService
{
    private readonly GrifballContext _context;
    public EventOrganizerService(GrifballContext context)
    {
        _context = context;
    }

    public Task<List<SeasonDto>> GetSeasons(CancellationToken ct = default)
    {
        return _context.Seasons
            .Select(x => new SeasonDto()
            {
                SeasonID = x.SeasonID,
                SeasonName = x.SeasonName,
                SignupsOpen = x.SignupsOpen,
                SignupsClose = x.SignupsClose,
                DraftStart = x.DraftStart,
                SeasonStart = x.SeasonStart,
                SeasonEnd = x.SeasonEnd,
                SignupsCount = x.SeasonSignups.Count,
            }).AsNoTracking().AsSplitQuery().ToListAsync(ct);
    }

    public Task<SeasonDto?> GetSeason(int seasonID, CancellationToken ct = default)
    {
        return _context.Seasons
            .Select(x => new SeasonDto()
            {
                SeasonID = x.SeasonID,
                SeasonName = x.SeasonName,
                SignupsOpen = x.SignupsOpen,
                SignupsClose = x.SignupsClose,
                DraftStart = x.DraftStart,
                SeasonStart = x.SeasonStart,
                SeasonEnd = x.SeasonEnd,
                SignupsCount = x.SeasonSignups.Count,
            }).Where(x => x.SeasonID == seasonID).AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(ct);
    }

    public async Task<int> UpsertSeason(UpsertSeasonDto dto, CancellationToken ct = default)
    {
        Season? season = null;

        if (dto.SeasonID is not null)
        {
            season = await _context.Seasons.Where(x => x.SeasonID == dto.SeasonID).FirstOrDefaultAsync(ct) ?? throw new Exception("Season does not exist");
        }
        else
        {
            season = new Season();
            await _context.Seasons.AddAsync(season, ct);
        }

        season.SeasonName = dto.SeasonName;
        season.SignupsOpen = dto.SignupsOpen;
        season.SignupsClose = dto.SignupsClose;
        season.DraftStart = dto.DraftStart;
        season.SeasonStart = dto.SeasonStart;
        season.SeasonEnd = dto.SeasonEnd;

        await _context.SaveChangesAsync(ct);
        return season.SeasonID;
    }
}
