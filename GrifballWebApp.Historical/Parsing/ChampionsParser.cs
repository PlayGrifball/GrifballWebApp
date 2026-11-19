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
}
