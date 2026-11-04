using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Championships;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>
/// A franchise's history: a team is a row of one season, so a team that came back season after season (Trophy Hunters,
/// Texas Shock) is the teams of every season with its name. Names compare as the database compares text (case-insensitive)
/// and ignore surrounding whitespace.
/// </summary>
public class TeamHistoryService
{
    private readonly GrifballContext _context;

    public TeamHistoryService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Every season a team named <paramref name="teamName"/> played, newest first: its record (counted as the standings
    /// count it), roster and captain, and the titles it won; with the franchise's totals. Null when no team has the name.
    /// </summary>
    public async Task<TeamHistoryDto?> GetAsync(string? teamName, CancellationToken ct = default)
    {
        var name = teamName?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        var teams = await _context.Teams.AsNoTracking()
            .Where(t => t.TeamName.Trim() == name)
            .Select(t => new
            {
                t.TeamID,
                t.TeamName,
                t.SeasonID,
                Titles = t.PlayerChampionships.Select(c => c.Tier).Distinct().ToList(),
                Players = t.TeamPlayers.Select(p => new
                {
                    p.TeamPlayerID,
                    p.UserID,
                    Gamertag = p.User.XboxUser != null ? p.User.XboxUser.Gamertag : null,
                    Discord = p.User.DiscordUser != null ? p.User.DiscordUser.DiscordUsername : null,
                    p.User.DisplayName,
                    p.User.UserName,
                    p.DraftCaptainOrder,
                    p.DraftRound,
                    IsCaptain = p.CaptainTeam != null || p.DraftCaptainOrder != null,
                    Region = p.User.Region != null ? p.User.Region.RegionName : null,
                }).ToList(),
            })
            .AsSplitQuery()
            .ToListAsync(ct);
        if (teams.Count == 0)
            return null;

        var teamIDs = teams.Select(t => t.TeamID).ToList();
        var seasonIDs = teams.Select(t => t.SeasonID).Distinct().ToList();
        // Counted as the standings count it
        var records = await _context.Teams.AsNoTracking().Where(t => teamIDs.Contains(t.TeamID)).Select(TeamRecord.Of).ToDictionaryAsync(r => r.TeamID, ct);
        var seasons = await SeasonInfoDto.LoadAsync(_context, seasonIDs, ct);
        var champions = await BracketChampionsAsync(seasonIDs, ct);

        var lines = teams.Select(t =>
        {
            var season = seasons[t.SeasonID];
            var record = records[t.TeamID];
            return new TeamHistorySeasonDto
            {
                TeamID = t.TeamID,
                TeamName = t.TeamName,
                Season = season,
                Wins = record.Wins,
                Losses = record.Losses,
                RecordFromSource = record.FromSource,
                Titles = TitlesOf(t.TeamID, t.Titles, season, champions),
                Players = t.Players.Select(p => new PlayerDto
                {
                    TeamPlayerID = p.TeamPlayerID,
                    UserID = p.UserID,
                    Gamertag = UserNames.Pick(p.Gamertag, p.Discord, p.DisplayName, p.UserName),
                    DraftCaptainOrder = p.DraftCaptainOrder,
                    DraftRound = p.DraftRound,
                    IsCaptain = p.IsCaptain,
                    Region = p.Region,
                }).OrderBy(x => x.IsCaptain ? 0 : 1).ThenBy(x => x.DraftRound).ThenBy(x => x.Gamertag).ToArray(),
            };
        })
            .OrderByDescending(l => l.Season.Start)
            .ThenByDescending(l => l.Season.SeasonID)
            .ToArray();

        return new TeamHistoryDto
        {
            // The name as the franchise last went by
            TeamName = lines[0].TeamName,
            Appearances = lines.Length,
            Wins = lines.Sum(l => l.Wins),
            Losses = lines.Sum(l => l.Losses),
            Championships = lines.Sum(l => l.Titles.Length),
            Players = lines.SelectMany(l => l.Players).Select(p => p.UserID).Distinct().Count(),
            Seasons = lines,
        };
    }

    /// <summary>
    /// The tiers the team won: its titles (<see cref="PlayerChampionship"/>), or, for a season of the app's whose bracket
    /// is decided but recorded no title (a champion without a roster), its division's when the team won the final.
    /// </summary>
    private static string[] TitlesOf(int teamID, List<string> titles, SeasonInfoDto season, Dictionary<int, int?> champions)
    {
        if (titles.Count > 0)
            return titles.Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (!season.IsHistorical && champions.TryGetValue(season.SeasonID, out var champion) && champion == teamID)
            return [string.IsNullOrWhiteSpace(season.Division) ? ChampionshipService.DefaultTier : season.Division];
        return [];
    }

    /// <summary>The champion of each season's playoff bracket (<see cref="ChampionshipService.ChampionOf"/>), by SeasonID; a
    /// season without a bracket is not in it.</summary>
    private async Task<Dictionary<int, int?>> BracketChampionsAsync(List<int> seasonIDs, CancellationToken ct)
    {
        var bracket = await _context.SeasonMatches.AsNoTracking()
            .Where(m => seasonIDs.Contains(m.SeasonID) && m.BracketMatch != null)
            .Select(m => new
            {
                m.SeasonID,
                Result = new ChampionshipService.BracketResult(m.BracketMatch!.Bracket, m.BracketMatch.RoundNumber, m.HomeTeamID, m.HomeTeamResult, m.AwayTeamID, m.AwayTeamResult),
            })
            .ToListAsync(ct);
        return bracket.GroupBy(m => m.SeasonID)
            .ToDictionary(g => g.Key, g => ChampionshipService.ChampionOf(g.Select(m => m.Result).ToList()));
    }
}

/// <summary>A franchise: every season's team of one name, and its totals.</summary>
public class TeamHistoryDto
{
    /// <summary>The name of its latest season's team.</summary>
    public required string TeamName { get; set; }
    /// <summary>The seasons it played.</summary>
    public int Appearances { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    /// <summary>The titles it won, across seasons and tiers.</summary>
    public int Championships { get; set; }
    /// <summary>The players who played for it, each once.</summary>
    public int Players { get; set; }
    /// <summary>Newest first.</summary>
    public required TeamHistorySeasonDto[] Seasons { get; set; }
}

/// <summary>One season of a franchise: that season's team.</summary>
public class TeamHistorySeasonDto
{
    public required int TeamID { get; set; }
    /// <summary>The name as that season spelled it.</summary>
    public required string TeamName { get; set; }
    public required SeasonInfoDto Season { get; set; }
    public required int Wins { get; set; }
    public required int Losses { get; set; }
    /// <inheritdoc cref="TeamDto.RecordFromSource"/>
    public bool RecordFromSource { get; set; }
    /// <inheritdoc cref="TeamDto.Titles"/>
    public string[] Titles { get; set; } = [];
    /// <summary>Captain first, then by draft round.</summary>
    public required PlayerDto[] Players { get; set; }
}
