using GrifballWebApp.Database;
using GrifballWebApp.Domain.Models;
using GrifballWebApp.Server.Seasons;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Stats;

/// <summary>A player's stats for one phase of a season (regular season or playoffs) on one team, with the season's facts.</summary>
public class SeasonStatLine : StatLine
{
    public required SeasonInfoDto Season { get; set; }
    public int SeasonID => Season.SeasonID;
    public override string SeasonKey => $"season/{SeasonID}";

    /// <summary>The stored row, for stored totals.</summary>
    public int? PlayerSeasonStatID { get; set; }
    public int? TeamID { get; set; }
    public string? TeamName { get; set; }
    public string? TradeNote { get; set; }
    public string? RoleNote { get; set; }
    public int StintIndex { get; set; } = 1;
    public bool IsPlayoffs { get; set; }
    /// <summary>A stored row of a sheet that has the regular season and the playoffs together (the season's only row for
    /// the player): it covers both phases.</summary>
    public bool IsCombined { get; set; }
}

/// <summary>
/// Season player stats, the one place the season page, team page, profiles and leaderboards read them from. Where a
/// player's phase of a season has games in the app (<see cref="PlayerGameStatsQuery"/>: Waypoint matches of reported or
/// imported series and recorded MCC games), the stats are those games' totals; otherwise they are the season totals stored
/// for it (<see cref="PlayerSeasonStat"/>, a historical sheet's). Never both for one player's phase of a season.
/// </summary>
public class SeasonStatsService
{
    private readonly GrifballContext _context;
    private readonly TimeProvider _time;

    public SeasonStatsService(GrifballContext context, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _context = context;
    }

    /// <summary>The stat lines of a season, of a player, or of every season (both null).</summary>
    public async Task<List<SeasonStatLine>> GetAsync(int? seasonID = null, int? userID = null, CancellationToken ct = default)
    {
        var fromMatches = await FromMatchesAsync(seasonID, userID, ct);
        var covered = fromMatches.Select(l => (l.SeasonID, l.UserID, l.IsPlayoffs)).ToHashSet();

        var stored = _context.PlayerSeasonStats.AsNoTracking();
        if (seasonID is not null)
            stored = stored.Where(s => s.SeasonID == seasonID);
        if (userID is not null)
            stored = stored.Where(s => s.UserID == userID);
        var storedRows = (await stored.Select(s => new
        {
            Row = s,
            TeamName = s.Team != null ? s.Team.TeamName : null,
            GameVersionName = s.GameVersion.GameVersionName,
            GameVersionSlug = s.GameVersion.Slug,
        }).ToListAsync(ct))
            // The games, where there are any, are the player's stats for that phase of the season; a combined row covers both
            .Where(r => !covered.Contains((r.Row.SeasonID, r.Row.UserID, r.Row.IsPlayoffs)) && !(r.Row.IsCombined && covered.Contains((r.Row.SeasonID, r.Row.UserID, !r.Row.IsPlayoffs))))
            .ToList();

        var seasons = await SeasonInfoDto.LoadAsync(_context, fromMatches.Select(l => l.SeasonID).Concat(storedRows.Select(r => r.Row.SeasonID)), _time.GetUtcNow().UtcDateTime, ct);
        foreach (var line in fromMatches)
            line.Season = seasons[line.Season.SeasonID];
        var storedLines = storedRows.Select(r =>
        {
            var s = r.Row;
            var line = new SeasonStatLine
            {
                Season = seasons[s.SeasonID],
                Gamertag = s.Gamertag,
                PlayerSeasonStatID = s.PlayerSeasonStatID,
                TeamID = s.TeamID,
                TeamName = r.TeamName,
                TradeNote = s.TradeNote,
                RoleNote = s.RoleNote,
                StintIndex = s.StintIndex,
                IsPlayoffs = s.IsPlayoffs,
                IsCombined = s.IsCombined,
            };
            line.SetStored(s, r.GameVersionName, r.GameVersionSlug);
            return line;
        });

        return fromMatches.Concat(storedLines)
            .OrderByDescending(l => l.Season.Start)
            .ThenBy(l => l.SeasonID)
            .ThenBy(l => l.IsPlayoffs)
            .ThenByDescending(l => l.Goals)
            .ThenByDescending(l => l.Kills)
            .ThenBy(l => l.Gamertag)
            .ThenBy(l => l.StintIndex)
            .ToList();
    }

    /// <summary>
    /// The totals of the games that count for seasons (<see cref="PlayerGameStatsQuery"/>: Waypoint and recorded games in
    /// season matches' series, and recorded games of a season outside any), by player, season,
    /// phase (a bracket match is a playoff one) and team, totalled in the database. A stat a game doesn't record is left
    /// out of the total; one no game records is null. The lines' Season is a stub with its ID until <see cref="GetAsync"/>
    /// loads the seasons.
    /// </summary>
    private async Task<List<SeasonStatLine>> FromMatchesAsync(int? seasonID, int? userID, CancellationToken ct)
    {
        var query = PlayerGameStatsQuery.Query(_context);
        if (seasonID is not null)
            query = query.Where(g => g.SeasonID == seasonID);
        if (userID is not null)
            query = query.Where(g => g.UserID == userID);
        var totals = await GameTotals.Of(query).ToListAsync(ct);
        if (totals.Count == 0)
            return [];

        var gameVersions = await _context.GameVersions.AsNoTracking().ToDictionaryAsync(g => g.GameVesionID, ct);
        var teamIDs = totals.Where(t => t.TeamID is not null).Select(t => t.TeamID!.Value).Distinct().ToList();
        var teamNames = await _context.Teams.AsNoTracking().Where(t => teamIDs.Contains(t.TeamID))
            .ToDictionaryAsync(t => t.TeamID, t => t.TeamName, ct);

        return totals
            .GroupBy(t => (t.SeasonID, t.UserID, t.IsPlayoffs))
            .SelectMany(phase => phase.OrderBy(t => t.TeamID).Select((t, index) =>
            {
                var line = new SeasonStatLine
                {
                    Season = new SeasonInfoDto { SeasonID = t.SeasonID },
                    Gamertag = t.Gamertag,
                    TeamID = t.TeamID,
                    TeamName = t.TeamID is int teamID ? teamNames.GetValueOrDefault(teamID) : null,
                    StintIndex = index + 1,
                    IsPlayoffs = t.IsPlayoffs,
                };
                line.SetGames(t, gameVersions.GetValueOrDefault(t.GameVersionID));
                return line;
            }))
            .ToList();
    }
}

