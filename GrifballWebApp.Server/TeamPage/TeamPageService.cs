using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Seasons;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>A team's page: its season, record, roster and titles.</summary>
public class TeamPageService
{
    private readonly GrifballContext _context;

    public TeamPageService(GrifballContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The team: its record (from its matches, or the one its source spreadsheet gives for a season whose matches are
    /// not in the app), roster and titles; its players' stats are Season/GetSeasonPlayerStats?teamID=. Null when there
    /// is no such team.
    /// </summary>
    public async Task<TeamDto?> GetAsync(int teamID, CancellationToken ct = default)
    {
        var team = await _context.Teams.AsNoTracking().Where(x => x.TeamID == teamID)
            .Select(x => new
            {
                x.TeamName,
                x.SeasonID,
                TBD = x.HomeMatches.Count(x => x.HomeTeamResult == null) + x.AwayMatches.Count(x => x.AwayTeamResult == null),
                // Made distinct after: a DISTINCT in a collection needs APPLY, which SQLite can't run
                Titles = x.PlayerChampionships.Select(c => c.Tier).ToArray(),
                Players = x.TeamPlayers.Select(x => new
                {
                    x.TeamPlayerID,
                    x.UserID,
                    Gamertag = x.User.XboxUser != null ? x.User.XboxUser.Gamertag : null,
                    Discord = x.User.DiscordUser != null ? x.User.DiscordUser.DiscordUsername : null,
                    x.User.DisplayName,
                    x.User.UserName,
                    x.DraftCaptainOrder,
                    x.DraftRound,
                    IsCaptain = x.CaptainTeam != null || x.DraftCaptainOrder != null,
                    Region = x.User.Region != null ? x.User.Region.RegionName : null,
                }).ToList(),
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(ct);
        if (team is null)
            return null;
        // Counted as the standings count it
        var record = await _context.Teams.Where(x => x.TeamID == teamID).Select(TeamRecord.Of).FirstAsync(ct);
        var season = (await SeasonInfoDto.LoadAsync(_context, [team.SeasonID], ct))[team.SeasonID];
        return new TeamDto
        {
            TeamName = team.TeamName,
            Season = season,
            Wins = record.Wins,
            Losses = record.Losses,
            RecordFromSource = record.FromSource,
            TBD = team.TBD,
            Titles = team.Titles.Distinct().ToArray(),
            Players = team.Players.Select(p => new PlayerDto
            {
                TeamPlayerID = p.TeamPlayerID,
                UserID = p.UserID,
                // As the app names a user everywhere: a placeholder of an imported season has no Xbox account
                Gamertag = UserNames.Pick(p.Gamertag, p.Discord, p.DisplayName, p.UserName),
                DraftCaptainOrder = p.DraftCaptainOrder,
                DraftRound = p.DraftRound,
                IsCaptain = p.IsCaptain,
                Region = p.Region,
            }).OrderBy(x => x.IsCaptain ? 0 : 1).ThenBy(x => x.DraftRound).ThenBy(x => x.Gamertag).ToArray(),
        };
    }
}
