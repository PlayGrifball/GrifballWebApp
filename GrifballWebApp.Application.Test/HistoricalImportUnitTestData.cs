using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Application.Test;

/// <summary>The archive input and the pulled Waypoint matches the historical import's unit tests build
/// (HistoricalImportServiceUnitTests, HistoricalImportServiceMoreUnitTests, HistoricalImportLateLeagueUnitTests).</summary>
internal static class HistoricalImportUnitTestData
{
    /// <summary>An archive of these season sheets.</summary>
    public static HistoricalArchiveInput Input(params ArchiveSeason[] entries) => new() { Seasons = new UnifiedArchive { Seasons = [.. entries] } };

    /// <summary>An archive of one season sheet and these recorded games (null: the archive has none at all).</summary>
    public static HistoricalArchiveInput Input(ArchiveSeason season, List<RecordedGameInput>? games)
        => new() { Seasons = new UnifiedArchive { Seasons = [season] }, RecordedGames = games };

    /// <summary>A season sheet: "{slug of the name}/Stats", or "/Playoff Stats" for the playoffs.</summary>
    public static ArchiveSeason Entry(string name, string game, string league, bool playoffs, params ArchivePlayer[] players) => new()
    {
        SeasonName = name,
        GameVersion = game,
        League = league,
        Division = "Open",
        IsPlayoffs = playoffs,
        SourceWorkbook = HistoricalKeys.Slug(name),
        SourceSheet = playoffs ? "Playoff Stats" : "Stats",
        Players = [.. players],
    };

    /// <summary>A sheet's row: one game played, one death.</summary>
    public static ArchivePlayer Player(string gamertag, int kills = 0, int stint = 1, string? team = null, int wins = 0, int losses = 0) => new()
    {
        Gamertag = gamertag,
        Kills = kills,
        Wins = wins,
        Losses = losses,
        Deaths = 1,
        GamesPlayed = 1,
        StintIndex = stint,
        TeamName = team,
        KdRatio = 1.23456m,
    };

    /// <summary>A Waypoint match already in the database (GetAndSaveMatch's result), as if it had been pulled: a team per
    /// outcome, one participant per player.</summary>
    public static async Task AddMatch(FakeApplicationDbContext context, Guid matchId, params (long Xuid, string Gamertag, Outcomes Outcome, int Kills, int Deaths)[] players)
    {
        var match = Match.Seed(matchId, duration: TimeSpan.FromMinutes(10));
        var teams = new Dictionary<Outcomes, int>();
        foreach (var player in players)
        {
            var xbox = await context.XboxUsers.FindAsync(player.Xuid) ?? context.XboxUsers.Add(XboxUser.FromXbox(player.Xuid, player.Gamertag)).Entity;
            if (!teams.TryGetValue(player.Outcome, out var teamID))
            {
                teamID = teams.Count;
                teams.Add(player.Outcome, teamID);
                match.AddTeam(teamID, 0, player.Outcome);
            }
            match.AddParticipant(teamID, player.Xuid, new MatchParticipantStats
            {
                Kills = player.Kills,
                Deaths = player.Deaths,
                PersonalScore = player.Kills * 100,
                TimePlayed = TimeSpan.FromMinutes(10),
                AverageLife = TimeSpan.FromSeconds(12),
                DamageDealt = 1000,
                DamageTaken = 900,
                FirstJoinedTime = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            }, xboxUser: xbox);
        }
        context.Matches.Add(match);
        await context.SaveChangesAsync();
    }
}
