using System.ComponentModel;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Dtos;
using GrifballWebApp.Application.Grades;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Signups;
using GrifballWebApp.Application.TeamStandings;

namespace GrifballWebApp.Application.Tools;

/// <summary>A season's schedule: the matches with a time, and those still waiting for one.</summary>
public sealed record SeasonScheduleDto(List<ScheduledMatchDto> Scheduled, List<UnscheduledMatchDto> Unscheduled);

/// <summary>Seasons: what the season pages show, open to everyone on the site. A season left out is the running one, else the latest.</summary>
public class SeasonTools
{
    private const int PageSize = 25;

    private readonly SeasonService _seasons;
    private readonly TeamStandingsService _standings;
    private readonly MatchPlannerService _matchPlanner;
    private readonly GradesService _grades;
    private readonly SignupsService _signups;
    private readonly IBracketService _brackets;

    public SeasonTools(SeasonService seasons, TeamStandingsService standings, MatchPlannerService matchPlanner, GradesService grades,
        SignupsService signups, IBracketService brackets)
    {
        _seasons = seasons;
        _standings = standings;
        _matchPlanner = matchPlanner;
        _grades = grades;
        _signups = signups;
        _brackets = brackets;
    }

    /// <summary>The refusal when no season is named and none is running or announced: a season can still be named.</summary>
    public const string NoSeasonYet = "No season is running or announced yet: pass a seasonID (list_seasons has their IDs) to ask about one.";

    /// <summary>The season asked for, or the running one, else the latest; a <see cref="ToolException"/> when there is none.</summary>
    private async Task<int> SeasonOrCurrent(int? seasonID, CancellationToken ct)
    {
        if (seasonID is int id)
            return await _seasons.GetSeasonName(id, ct) is null ? throw new ToolException($"There is no season {id}: list_seasons has their IDs.") : id;
        // The same default as the slash commands: the running season, else the latest
        var current = await _seasons.GetCurrentOrLatestSeasonID(ct);
        return current != 0 ? current : throw new ToolException(NoSeasonYet);
    }

    [Description("Lists the league's seasons, 25 to a page: each one's ID, name, league, game, dates and whether it has ended.")]
    public Task<PaginationResult<SeasonListItemDto>> ListSeasons([Description("The page, from 1.")] int page = 1, CancellationToken ct = default)
        => _seasons.GetSeasons(new SeasonListFilter { PageNumber = page, PageSize = PageSize }, ct);

    [Description("A season's summary: its league, format, dates, how many teams, signups and matches it has, and its champions.")]
    public async Task<SeasonSummaryDto> GetSeason([Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null, CancellationToken ct = default)
        => (await _seasons.GetSeasonSummary(await SeasonOrCurrent(seasonID, ct), ct))!;

    [Description("A season's standings: each team's wins and losses, best first.")]
    public async Task<TeamStandingDto[]> GetSeasonStandings([Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null, CancellationToken ct = default)
        => await _standings.GetTeamStandings(await SeasonOrCurrent(seasonID, ct), ct);

    [Description("A season's schedule: its matches with a time (and whether each is complete), and those not scheduled yet.")]
    public async Task<SeasonScheduleDto> GetSeasonSchedule([Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null, CancellationToken ct = default)
    {
        var id = await SeasonOrCurrent(seasonID, ct);
        return new SeasonScheduleDto(await _matchPlanner.GetScheduledMatches(id, ct), await _matchPlanner.GetUnscheduledMatches(id, ct));
    }

    [Description("Each player's stats in a season (goals, kills, deaths, wins, K/D and more), or one team's players only.")]
    public async Task<List<SeasonPlayerStatDto>> GetSeasonPlayerStats(
        [Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null,
        [Description("Only this team's players; left out, every player.")] int? teamID = null,
        CancellationToken ct = default)
        => await _seasons.GetSeasonPlayerStats(await SeasonOrCurrent(seasonID, ct), teamID, ct);

    [Description("A season's player grades: each player's totals, per-minute rates and letter grades.")]
    public async Task<GradesDto> GetSeasonGrades([Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null, CancellationToken ct = default)
        => await _grades.GetGrades(await SeasonOrCurrent(seasonID, ct), ct);

    [Description("Who signed up for a season, and whether each will captain.")]
    public async Task<List<SignupResponseDto>> GetSeasonSignups([Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null, CancellationToken ct = default)
        => await _signups.GetSignups(await SeasonOrCurrent(seasonID, ct), ct);

    [Description("A season's playoff bracket: its winners' and losers' rounds, each match's teams and result, and the grand final.")]
    public async Task<BracketDto> GetSeasonBracket([Description("The season's ID; left out, the running season, else the latest.")] int? seasonID = null, CancellationToken ct = default)
        => await _brackets.GetBracketsAsync(await SeasonOrCurrent(seasonID, ct), ct);
}
