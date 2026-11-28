using GrifballWebApp.Application.Historical;
using GrifballWebApp.Domain.Archive;

namespace GrifballWebApp.Historical.Import;

/// <summary>Where the rows of a season come from (HistoricalProvenance): the records' origins, gathered for each row.</summary>
public partial class HistoricalImportService
{
    /// <summary>An entry's tab: as its parser gave it, or its workbook and sheet (an uploaded archive's entry).</summary>
    private static ArchiveOrigin? OriginOf(ArchiveSeason entry)
        => entry.Origin ?? (string.IsNullOrWhiteSpace(entry.SourceWorkbook) ? null : new ArchiveOrigin(entry.SourceWorkbook.Trim(), entry.SourceSheet?.Trim()));

    /// <summary>The season (or matchmaking season) is read from each of its entries' tabs.</summary>
    private void NoteSeason(object season, SeasonGroup group)
    {
        foreach (var entry in group.Entries)
            _provenance.Read(season, OriginOf(entry), $"{(entry.IsPlayoffs ? "Playoffs" : "Regular season")} of {entry.SeasonName}");
    }

    /// <summary>The records that name a team of the season: its standings, roster and draft rows, its players' stat rows, its
    /// titles and the schedule rows naming it.</summary>
    private static IEnumerable<(ArchiveOrigin? Origin, string Why)> TeamOrigins(SeasonGroup group, string name)
    {
        bool Names(string? other) => !HistoricalKeys.IsExcludedTeamName(other) && string.Equals(CleanTeamName(other!), name, StringComparison.OrdinalIgnoreCase);
        foreach (var entry in group.Entries)
        {
            foreach (var team in entry.Teams.Where(t => Names(t.TeamName)))
                yield return (team.Origin, $"Standings, roster or draft of {name}");
            foreach (var player in entry.Players.Where(p => Names(p.TeamName)))
                yield return (player.Origin, $"{player.Gamertag}'s stat row names {name}");
        }
        foreach (var title in group.Championships.Where(c => Names(c.Team)))
            yield return (title.Origin, $"{name}'s {title.Tier} title");
        foreach (var game in group.Waypoint?.Schedule.Where(g => Names(g.Home) || Names(g.Away)) ?? [])
            yield return (game.Origin, $"A schedule row names {name}");
    }

    /// <summary>The cell each of the season's Waypoint matches was listed in, by its match ID.</summary>
    private static Dictionary<Guid, ArchiveOrigin> WaypointOrigins(SeasonGroup group)
    {
        var origins = new Dictionary<Guid, ArchiveOrigin>();
        foreach (var (link, origin) in group.Waypoint?.Origins ?? [])
            if (HistoricalKeys.MatchId(link) is Guid id)
                origins.TryAdd(id, origin);
        return origins;
    }

    /// <summary>A recorded game, its sides and its players' lines are read from the game's rows (each line from its player's row, when the
    /// parser gives one).</summary>
    private void NoteRecordedGame(RecordedGame game, RecordedGameInput input, List<(RecordedPlayerInput Input, int UserId)> players)
    {
        _provenance.Read(game, input.Origin, $"Recorded game {game.SourceKey}");
        _provenance.Read(game, input.DatedBy, $"The day of recorded game {game.SourceKey}, {input.PlayedAt:yyyy-MM-dd HH:mm} UTC, from the schedule");
        foreach (var side in game.Teams)
            _provenance.Read(side, input.Origin, $"Side {side.Side} of recorded game {game.SourceKey}");
        foreach (var (player, userId) in players)
            if (game.Participants.FirstOrDefault(p => p.UserID == userId) is { } line)
                _provenance.Read(line, player.Origin ?? input.Origin, $"{player.Gamertag}'s line of recorded game {game.SourceKey}");
    }

    /// <summary>Each of the season's listed Waypoint matches in the database is read from the cell that lists it.</summary>
    private void NoteListedMatches(SeasonGroup group, HashSet<Guid> present, Dictionary<Guid, bool> matchPhases)
    {
        var listedIn = WaypointOrigins(group);
        foreach (var id in present)
            _provenance.Read("Match", id.ToString(), listedIn.GetValueOrDefault(id), $"Listed as a {Phase(matchPhases[id])} Waypoint match of {group.SeasonName}");
    }

    /// <summary>Why a Waypoint player is on their team: their stat row names it, or the schedule does.</summary>
    private static string WaypointJoinWhy(ArchivePlayer? sheetRow, string gamertag, string? teamName) => sheetRow?.Origin is null
        ? $"Played the Waypoint matches listed here; on {teamName} by the schedule"
        : $"{gamertag}'s stat row names {teamName}";

    /// <summary>Why a stat row's player is on their team: the row names it, or the schedule does.</summary>
    private static string StatRowWhy(ArchivePlayer player, string? teamName) => HistoricalKeys.IsExcludedTeamName(player.TeamName)
        ? $"{player.Gamertag}'s stat row; on {teamName} by the schedule"
        : $"{player.Gamertag}'s stat row names {teamName}";

    /// <summary>A new series' games, in order, each read from where its game was.</summary>
    private void AddSeriesGames(Domain.League.SeasonMatch match, List<ImportedGame> played)
    {
        foreach (var (game, index) in played.Select((g, i) => (g, i + 1)))
        {
            match.AddImportedGame(game.MatchID, game.Recorded, index);
            _provenance.Read(match, game.Origin, game.MatchID is Guid id
                ? $"Waypoint match {id}, game {index} of the series"
                : $"Recorded game {game.Recorded?.SourceKey}, game {index} of the series");
        }
    }

    /// <summary>A tier's titles are read from its champions record row.</summary>
    private void NoteTitles(Domain.League.Season season, string tier, Domain.League.Team team, ArchiveOrigin? origin)
    {
        foreach (var title in season.PlayerChampionships.Where(c => string.Equals(c.Tier, tier, StringComparison.OrdinalIgnoreCase)))
            _provenance.Read(title, origin, $"{title.GamertagUsed} on {team.TeamName}'s {tier} title");
    }
}
