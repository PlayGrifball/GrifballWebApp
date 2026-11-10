using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using GrifballWebApp.Application.Database;

namespace GrifballWebApp.Application.EventOrganizer;

public class EventOrganizerService
{
    private readonly IApplicationDbContext _context;
    public EventOrganizerService(IApplicationDbContext context)
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
                GameVersionID = x.GameVersionID,
                LeagueID = x.LeagueID,
                Division = x.Division,
                Format = x.Format,
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
                GameVersionID = x.GameVersionID,
                LeagueID = x.LeagueID,
                Division = x.Division,
                Format = x.Format,
            }).Where(x => x.SeasonID == seasonID).AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(ct);
    }

    public async Task<int> UpsertSeason(SeasonDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        Season? season = null;

        if (dto.SeasonID is not 0)
        {
            season = await WithEverythingCopied(dto.SeasonID, ct)
                ?? throw new Exception("Season does not exist");
        }
        else
        {
            if (dto.GameVersionID is null)
                throw new SeasonRefusedException("Choose the game the season is played on.");
            season = Season.Organized(dto.SeasonName, dto.LeagueID, dto.GameVersionID, dto.Division, dto.Format);
            _context.Seasons.Add(season);
        }
        // First, so an imported season is refused before anything else is said about the edit (Season.Schedule refuses it)
        var scheduled = season.Schedule(dto.SignupsOpen, dto.SignupsClose, dto.DraftStart, dto.SeasonStart, dto.SeasonEnd);
        if (ScheduleRefusal(scheduled) is { } refusal)
            throw new SeasonRefusedException(refusal);
        await RefuseUnknownOrTaken(dto, ct);

        // Schedule refused an imported season first, so the edit is one the season takes
        season.Edit(dto.SeasonName, dto.LeagueID, dto.GameVersionID ?? season.GameVersionID, dto.Division, dto.Format);
        season.AnnounceAt(dto.PublicAt);

        if (dto.CopyFromSeasonID is int copyFromID)
            await CopyFrom(season, dto, copyFromID, ct);

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return season.SeasonID;
    }

    /// <summary>A season with everything a copy takes from it: its signups, teams and availability.</summary>
    private Task<Season?> WithEverythingCopied(int seasonID, CancellationToken ct)
        => _context.Seasons
            .Include(x => x.SeasonSignups)
                .ThenInclude(x => x.SignupAvailability)
            .Include(x => x.Teams)
                .ThenInclude(x => x.TeamAvailability)
            .Include(x => x.Teams)
                .ThenInclude(x => x.TeamPlayers)
            .Include(x => x.SeasonAvailability)
            // A query per collection: one join of them all multiplies the rows
            .AsSplitQuery()
            .Where(x => x.SeasonID == seasonID).FirstOrDefaultAsync(ct);

    /// <summary>Refuses a game, league or format that doesn't exist, and a name another season of the league has.</summary>
    private async Task RefuseUnknownOrTaken(SeasonDto dto, CancellationToken ct)
    {
        if (dto.GameVersionID is not null && !await _context.GameVersions.AnyAsync(g => g.GameVesionID == dto.GameVersionID, ct))
            throw new SeasonRefusedException("That game doesn't exist.");
        if (!await _context.Leagues.AnyAsync(l => l.LeagueID == dto.LeagueID, ct))
            throw new SeasonRefusedException("That league doesn't exist.");
        if (!Enum.IsDefined(dto.Format))
            throw new SeasonRefusedException("That season format doesn't exist.");

        // Names are unique per league: say which season has it
        // Whether the season with the name is an imported one (Season.ImportedFromArchive); null: no season has it
        var takenByImported = await _context.Seasons.Where(x => x.LeagueID == dto.LeagueID && x.SeasonName == dto.SeasonName && x.SeasonID != dto.SeasonID)
            .Select(Season.ImportedFromArchive).Select(imported => (bool?)imported).FirstOrDefaultAsync(ct);
        if (takenByImported is bool historical)
            throw new SeasonRefusedException(historical
                ? $"A historical season of this league is already named {dto.SeasonName}. Choose another name."
                : $"There is already a season named {dto.SeasonName}. Choose another name.", StatusCodes.Status409Conflict);
    }

    /// <summary>Copies what the editor ticked (availability, signups, teams with their captains) from another season.</summary>
    private async Task CopyFrom(Season season, SeasonDto dto, int copyFromID, CancellationToken ct)
    {
        var copyFrom = await WithEverythingCopied(copyFromID, ct)
            ?? throw new SeasonRefusedException($"Cannot copy from season {copyFromID}, it was not found");

        if (dto.CopyAvailability)
            season.CopyAvailabilityFrom(copyFrom);

        if (dto.CopySignups)
            season.CopySignupsFrom(copyFrom);

        if (dto.CopyTeams)
        {
            var captains = season.CopyTeamsFrom(copyFrom);
            await _context.SaveChangesAsync(ct);
            foreach (var (team, captainUserID) in captains)
                team.SetCaptain(captainUserID);
        }
    }

    /// <summary>What the editor says when the season's dates are refused (<see cref="Season.Schedule"/>); null: scheduled.</summary>
    private static string? ScheduleRefusal(SeasonScheduleResult result) => result switch
    {
        SeasonScheduleResult.SignupsCloseBeforeTheyOpen => "Signups can't close before they open.",
        SeasonScheduleResult.DraftAfterSeasonStart => "The draft can't start after the season does.",
        SeasonScheduleResult.EndsBeforeItStarts => "The season can't end before it starts.",
        // An imported season is rewritten from its spreadsheets on every import, so an edit made here would be reverted by the
        // next one. Allowing edits would mean recording each admin change and reapplying it after every import; until then the
        // spreadsheet is the place to correct an imported season.
        SeasonScheduleResult.Imported => SeasonRefusedException.ImportedSeason,
        _ => null,
    };

    /// <summary>The games a season can be played on, for the season editor.</summary>
    public Task<List<GameVersionOptionDto>> GetGameVersions(CancellationToken ct = default)
        => _context.GameVersions.AsNoTracking().OrderBy(g => g.GameVesionID)
            .Select(g => new GameVersionOptionDto { GameVersionID = g.GameVesionID, Name = g.GameVersionName }).ToListAsync(ct);

    /// <summary>The leagues a season can be in, for the season editor.</summary>
    public Task<List<LeagueOptionDto>> GetLeagues(CancellationToken ct = default)
        => _context.Leagues.AsNoTracking().OrderBy(l => l.LeagueID)
            .Select(l => new LeagueOptionDto { LeagueID = l.LeagueID, Name = l.LeagueName }).ToListAsync(ct);
}

/// <summary>A season change the organizer can't make; the message is for the user.</summary>
public class SeasonRefusedException(string message, int status = StatusCodes.Status400BadRequest) : Exception(message)
{
    /// <summary>What an organizer's edit of a season imported from the historical archive is told (the editor's, the timeslots').</summary>
    public const string ImportedSeason = "That season was imported from the historical archive and can't be edited here.";

    /// <summary>What a season's timeslots are told when one has a day no week has (JSON can carry any number).</summary>
    public const string UnknownDay = "A timeslot's day isn't a day of the week.";

    public int Status { get; } = status;
}

public class GameVersionOptionDto
{
    public int GameVersionID { get; set; }
    public required string Name { get; set; }
}

public class LeagueOptionDto
{
    public int LeagueID { get; set; }
    public required string Name { get; set; }
}
