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

    public async Task<int> UpsertSeason(SeasonDto dto, CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        Season? season = null;

        if (dto.SeasonID is not 0)
        {
            season = await _context.Seasons
                .Include(x => x.SeasonSignups)
                    .ThenInclude(x => x.SignupAvailability)
                .Include(x => x.Teams)
                    .ThenInclude(x => x.TeamAvailability)
                .Include(x => x.Teams)
                    .ThenInclude(x => x.TeamPlayers)
                .Include(x => x.SeasonAvailability)
                .Where(x => x.SeasonID == dto.SeasonID).FirstOrDefaultAsync(ct)
                ?? throw new Exception("Season does not exist");
        }
        else
        {
            season = new Season();
            _context.Seasons.Add(season);
        }

        season.SeasonName = dto.SeasonName;
        season.SignupsOpen = dto.SignupsOpen;
        season.SignupsClose = dto.SignupsClose;
        season.DraftStart = dto.DraftStart;
        season.SeasonStart = dto.SeasonStart;
        season.SeasonEnd = dto.SeasonEnd;

        if (!string.IsNullOrEmpty(dto.CopyFrom))
        {
            var copyFrom = await _context.Seasons
                .Include(x => x.SeasonSignups)
                    .ThenInclude(x => x.SignupAvailability)
                .Include(x => x.Teams)
                    .ThenInclude(x => x.TeamAvailability)
                .Include(x => x.Teams)
                    .ThenInclude(x => x.TeamPlayers)
                .Include(x => x.SeasonAvailability)
                .Where(x => x.SeasonName == dto.CopyFrom).FirstOrDefaultAsync(ct)
                ?? throw new Exception($"Cannot copy from season {dto.CopyFrom}, it was not found");

            if (dto.CopyAvailability)
            {
                season.SeasonAvailability.Clear();

                var copied = copyFrom.SeasonAvailability.Select(x => new SeasonAvailability()
                {
                    AvailabilityOptionID = x.AvailabilityOptionID,
                });
                foreach (var item in copied )
                    season.SeasonAvailability.Add(item);
            }

            if (dto.CopySignups)
            {
                season.SeasonSignups.Clear();

                var copied = copyFrom.SeasonSignups.Select(x => x.Copy());
                foreach (var item in copied)
                    season.SeasonSignups.Add(item);
            }

            if (dto.CopyTeams)
            {
                season.Teams.Clear();

                var captains = new List<Tuple<Team, TeamPlayer>>();

                var copied = copyFrom.Teams.Select(x =>
                {
                    var copiedTeam = x.Copy(out var captain);
                    if (captain is not null)
                        captains.Add(new(copiedTeam, captain));
                    return copiedTeam;
                });
                foreach (var item in copied)
                    season.Teams.Add(item);

                await _context.SaveChangesAsync(ct);
                foreach(var captain in captains)
                {
                    captain.Item1.Captain = captain.Item2;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return season.SeasonID;
    }
}
