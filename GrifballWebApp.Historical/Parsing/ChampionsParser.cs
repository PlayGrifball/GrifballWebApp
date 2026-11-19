using GrifballWebApp.Infrastructure.Excel;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Historical.Import;
using GrifballWebApp.Historical.Sources;

namespace GrifballWebApp.Historical.Parsing;

/// <summary>The AGLA champions record: from its third row, each row is a title (season, game, tier, team, then up to eight
/// players, the captain first). A tier of "-" is Open. A team the record spells otherwise than its season's sheets is the name
/// its config's <see cref="HistoricalSourceConfig.TeamNames"/> gives (the record's "Yung Hummu" is the sheets' Yung Humma).</summary>
public static class ChampionsParser
{
    public static ChampionsArchive Parse(string sourceId, string? sourceUrl, SheetTable sheet, HistoricalSourceConfig? config = null)
    {
        var champions = new List<ArchiveChampionship>();
        for (var r = 2; r < sheet.Rows.Count; r++)
        {
            var season = sheet.Cell(r, 0).Trim().TrimEnd(':');
            var team = sheet.Cell(r, 3).Trim();
            team = config?.TeamName(team) ?? team;
            if (season.Length == 0 || team.Length == 0)
                continue;
            var tier = sheet.Cell(r, 2).Trim();
            var roster = Enumerable.Range(4, 8).Select(c => sheet.Cell(r, c).Trim()).Where(p => p.Length > 0).ToList();
            champions.Add(new ArchiveChampionship
            {
                SourceId = sourceId,
                SourceUrl = sourceUrl,
                Season = season,
                Game = sheet.Cell(r, 1).Trim(),
                Tier = tier == "-" ? "Open" : tier,
                Team = team,
                Captain = roster.FirstOrDefault(),
                Roster = roster,
            });
        }
        return new ChampionsArchive { SourceId = sourceId, SourceUrl = sourceUrl, Champions = champions };
    }

    /// <summary>A normalized champions table with a header row (league, season_name, game, tier, champion_team, p1..p8).</summary>
    public static ChampionsArchive ParseTable(string sourceId, string? sourceUrl, SheetTable sheet, List<string>? warnings = null, HistoricalSourceConfig? config = null)
    {
        if (sheet.Rows.Count < 2)
        {
            warnings?.Add($"{sourceId}/{sheet.Name}: champions table has no data rows");
            return new ChampionsArchive { SourceId = sourceId, SourceUrl = sourceUrl, Champions = [] };
        }

        var header = sheet.Rows[0];
        var leagueCol = Index(header, "league");
        var seasonCol = Index(header, "season_name", "season");
        var gameCol = Index(header, "game");
        var tierCol = Index(header, "tier");
        var teamCol = Index(header, "champion_team", "team");
        if (seasonCol < 0 || teamCol < 0)
        {
            warnings?.Add($"{sourceId}/{sheet.Name}: champions table is missing required season or team columns");
            return new ChampionsArchive { SourceId = sourceId, SourceUrl = sourceUrl, Champions = [] };
        }
        var playerCols = Enumerable.Range(1, 8).Select(n => Index(header, $"p{n}")).Where(c => c >= 0).ToList();

        var champions = new List<ArchiveChampionship>();
        for (var r = 1; r < sheet.Rows.Count; r++)
        {
            var season = Cell(sheet, r, seasonCol);
            var team = Cell(sheet, r, teamCol);
            team = config?.TeamName(team) ?? team;
            if (season.Length == 0 || team.Length == 0)
                continue;
            var tier = Cell(sheet, r, tierCol);
            var roster = playerCols.Select(c => Cell(sheet, r, c)).Where(p => p.Length > 0).ToList();
            champions.Add(new ArchiveChampionship
            {
                League = Cell(sheet, r, leagueCol) is { Length: > 0 } l ? l : null,
                SourceId = sourceId,
                SourceUrl = sourceUrl,
                Season = season,
                Game = Cell(sheet, r, gameCol) is { Length: > 0 } g ? g : null,
                Tier = tier.Length == 0 || tier == "-" ? "Open" : tier,
                Team = team,
                Captain = roster.FirstOrDefault(),
                Roster = roster,
            });
        }
        return new ChampionsArchive { SourceId = sourceId, SourceUrl = sourceUrl, Champions = champions };
    }

    private static string Cell(SheetTable sheet, int row, int col)
        => col >= 0 ? sheet.Cell(row, col).Trim() : "";

    private static int Index(string[] header, params string[] names)
    {
        foreach (var name in names)
        {
            for (var c = 0; c < header.Length; c++)
                if (string.Equals(header[c].Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return c;
        }
        return -1;
    }
}
