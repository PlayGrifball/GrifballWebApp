namespace GrifballWebApp.Server.Historical.Parsing;

/// <summary>A players list (the Late League master list): headed Gamertag, Discord, Discord ID, Location, System.</summary>
public static class PlayersListParser
{
    public static List<ArchivePlayerInput> Parse(string sourceId, SheetTable sheet)
    {
        var players = new List<ArchivePlayerInput>();
        if (sheet.Rows.Count < 2)
            return players;
        var headers = sheet.Rows[0].Select(ArchiveCells.HeaderKey).ToList();
        int Column(string name) => headers.IndexOf(name);
        var (gamertag, discord, discordId, location, system) = (Column("gamertag"), Column("discord"), Column("discordid"), Column("location"), Column("system"));
        if (gamertag < 0)
            return players;
        string? Value(int row, int column)
        {
            var text = column < 0 ? "" : sheet.Cell(row, column).Trim();
            return text.Length == 0 ? null : text;
        }
        for (var r = 1; r < sheet.Rows.Count; r++)
        {
            if (Value(r, gamertag) is not string tag)
                continue;
            players.Add(new ArchivePlayerInput
            {
                Gamertag = tag,
                DiscordUsername = Value(r, discord),
                DiscordId = Value(r, discordId),
                Region = Value(r, location),
                System = Value(r, system),
                SourceKey = $"{sourceId}/{sheet.Name}",
            });
        }
        return players;
    }
}
