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
                PublicAt = x.PublicAt,
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
                PublicAt = x.PublicAt,
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
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
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
            if (season.IsHistorical)
                throw new SeasonRefusedException("That season was imported from the historical archive and can't be edited here.");
        }
        else
        {
            season = new Season();
            _context.Seasons.Add(season);
        }

        // Names are unique per league: say which season has it
        var taken = await _context.Seasons.Where(x => x.LeagueID == season.LeagueID && x.SeasonName == dto.SeasonName && x.SeasonID != dto.SeasonID)
            .Select(x => new { x.IsHistorical }).FirstOrDefaultAsync(ct);
        if (taken is not null)
            throw new SeasonRefusedException(taken.IsHistorical
                ? $"A historical season of this league is already named {dto.SeasonName}. Choose another name."
                : $"There is already a season named {dto.SeasonName}. Choose another name.", StatusCodes.Status409Conflict);

        season.SeasonName = dto.SeasonName;
        season.PublicAt = dto.PublicAt;
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

/// <summary>A season change the organizer can't make; the message is for the user.</summary>
public class SeasonRefusedException(string message, int status = StatusCodes.Status400BadRequest) : Exception(message)
{
    public int Status { get; } = status;
}
