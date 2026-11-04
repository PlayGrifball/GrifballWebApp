using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Server.Stats;

/// <summary>Where a <see cref="PlayerGameStat"/> comes from.</summary>
public enum GameSource
{
    /// <summary>A Halo Infinite match pulled from Waypoint (Infinite.MatchParticipants).</summary>
    Infinite = 1,
    /// <summary>A recorded game, e.g. an MCC carnage report (Recorded.GameParticipants).</summary>
    Recorded = 2,
}

/// <summary>One player's line of one game that counts for a season: in a season match's series, or a recorded game of
/// the season outside any series. For a matchmaking game (<see cref="PlayerGameStatsQuery.Matchmaking"/>) SeasonID is its
/// matchmaking season's, 0 for the queue's games (in none).</summary>
public class PlayerGameStat
{
    public int SeasonID { get; set; }
    public int? SeasonMatchID { get; set; }
    public Guid? MatchID { get; set; }
    public int? RecordedGameID { get; set; }
    public int UserID { get; set; }
    public string Gamertag { get; set; } = "";
    public int GameVersionID { get; set; }
    public GameSource Source { get; set; }
    public bool IsPlayoffs { get; set; }
    /// <summary>The season's team the player played for, when known.</summary>
    public int? TeamID { get; set; }
    public bool Won { get; set; }
    public bool Lost { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    /// <summary>Infinite's personal score; null where a game doesn't record one.</summary>
    public int? Score { get; set; }
    /// <summary>Grifball goals: an Infinite player's as <see cref="GrifballGoals"/> defines them, a recorded game's as recorded.</summary>
    public int? Goals { get; set; }
    public int? CarrierKills { get; set; }
    public int? Betrayals { get; set; }
    public int? Suicides { get; set; }
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }
    public int? DamageDealt { get; set; }
    public int? DamageTaken { get; set; }
}

/// <summary>
/// Every player game that counts for a season, Halo Infinite and recorded alike, as one query (UNION ALL) the stats
/// read: the season page, team page, profiles and leaderboards (through <see cref="SeasonStatsService"/>). Translated
/// to SQL on both databases; filter it before materialising.
/// </summary>
public static class PlayerGameStatsQuery
{
    /// <summary>Halo Infinite's GameVersion (seeded): every Waypoint match is one.</summary>
    public const int InfiniteGameVersionID = 6;

    public static IQueryable<PlayerGameStat> Query(GrifballContext context)
    {
        // A Waypoint game's player is their Xbox account's user; an account with none is left out (nobody to show it for)
        var infinite = context.MatchParticipants
            .Where(p => p.MatchTeam.Match.SeriesGame != null && p.XboxUser.User != null)
            .Select(GrifballGoals.Inline((MatchParticipant p) => new PlayerGameStat
            {
                SeasonID = p.MatchTeam.Match.SeriesGame.SeasonMatch.SeasonID,
                SeasonMatchID = (int?)p.MatchTeam.Match.SeriesGame.SeasonMatchID,
                MatchID = (Guid?)p.MatchID,
                RecordedGameID = (int?)null,
                UserID = p.XboxUser.User!.Id,
                Gamertag = p.XboxUser.Gamertag,
                GameVersionID = InfiniteGameVersionID,
                Source = GameSource.Infinite,
                IsPlayoffs = p.MatchTeam.Match.SeriesGame.SeasonMatch.BracketMatch != null,
                // The side of the season match whose roster the player is on
                TeamID = context.TeamPlayers.Any(tp => tp.TeamID == p.MatchTeam.Match.SeriesGame.SeasonMatch.HomeTeamID && tp.UserID == p.XboxUser.User!.Id)
                    ? p.MatchTeam.Match.SeriesGame.SeasonMatch.HomeTeamID
                    : context.TeamPlayers.Any(tp => tp.TeamID == p.MatchTeam.Match.SeriesGame.SeasonMatch.AwayTeamID && tp.UserID == p.XboxUser.User!.Id)
                        ? p.MatchTeam.Match.SeriesGame.SeasonMatch.AwayTeamID
                        : null,
                Won = p.MatchTeam.Outcome == Outcomes.Won,
                Lost = p.MatchTeam.Outcome == Outcomes.Lost,
                Kills = p.Kills,
                Deaths = p.Deaths,
                Assists = p.Assists,
                Score = (int?)p.PersonalScore,
                Goals = (int?)GrifballGoals.Infinite(p),
                CarrierKills = (int?)null,
                Betrayals = (int?)p.Betrayals,
                Suicides = (int?)p.Suicides,
                HammerKills = (int?)null,
                SwordKills = (int?)null,
                BallKills = (int?)null,
                Punches = (int?)null,
                DamageDealt = (int?)p.DamageDealt,
                DamageTaken = (int?)p.DamageTaken,
            }));

        // Same members, same order: the two halves are one UNION ALL
        var recorded = context.RecordedGameParticipants
            .Where(p => p.RecordedGame.SeriesGame != null || p.RecordedGame.SeasonID != null)
            .Select(p => new PlayerGameStat
            {
                SeasonID = p.RecordedGame.SeriesGame != null ? p.RecordedGame.SeriesGame.SeasonMatch.SeasonID : p.RecordedGame.SeasonID!.Value,
                SeasonMatchID = p.RecordedGame.SeriesGame != null ? (int?)p.RecordedGame.SeriesGame.SeasonMatchID : null,
                MatchID = (Guid?)null,
                RecordedGameID = (int?)p.RecordedGameID,
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                GameVersionID = p.RecordedGame.GameVersionID,
                Source = GameSource.Recorded,
                IsPlayoffs = p.RecordedGame.SeriesGame != null ? p.RecordedGame.SeriesGame.SeasonMatch.BracketMatch != null : p.RecordedGame.IsPlayoffs,
                TeamID = p.RecordedGameTeam != null ? p.RecordedGameTeam.TeamID : null,
                Won = p.RecordedGameTeam != null && p.RecordedGameTeam.Outcome == Outcomes.Won,
                Lost = p.RecordedGameTeam != null && p.RecordedGameTeam.Outcome == Outcomes.Lost,
                Kills = p.Kills,
                Deaths = p.Deaths,
                Assists = p.Assists,
                Score = p.Score,
                Goals = p.Goals,
                CarrierKills = p.CarrierKills,
                Betrayals = p.Betrayals,
                Suicides = p.Suicides,
                HammerKills = p.HammerKills,
                SwordKills = p.SwordKills,
                BallKills = p.BallKills,
                Punches = p.Punches,
                DamageDealt = (int?)null,
                DamageTaken = (int?)null,
            });

        return infinite.Concat(recorded);
    }

    /// <summary>
    /// Every player game of matchmaking (Late League), as one query: the queue's Halo Infinite matches once reported, and
    /// the archive's recorded games, each a <see cref="MatchedMatch"/>. SeasonID is the matchmaking season's (0: the queue's,
    /// in none); there are no playoffs or teams.
    /// </summary>
    public static IQueryable<PlayerGameStat> Matchmaking(GrifballContext context)
    {
        var infinite = context.MatchParticipants
            .Where(p => p.MatchTeam.Match.MatchedMatch != null && p.XboxUser.User != null)
            .Select(GrifballGoals.Inline((MatchParticipant p) => new PlayerGameStat
            {
                SeasonID = p.MatchTeam.Match.MatchedMatch.MatchmakingSeasonID ?? 0,
                SeasonMatchID = (int?)null,
                MatchID = (Guid?)p.MatchID,
                RecordedGameID = (int?)null,
                UserID = p.XboxUser.User!.Id,
                Gamertag = p.XboxUser.Gamertag,
                GameVersionID = InfiniteGameVersionID,
                Source = GameSource.Infinite,
                IsPlayoffs = false,
                TeamID = (int?)null,
                Won = p.MatchTeam.Outcome == Outcomes.Won,
                Lost = p.MatchTeam.Outcome == Outcomes.Lost,
                Kills = p.Kills,
                Deaths = p.Deaths,
                Assists = p.Assists,
                Score = (int?)p.PersonalScore,
                Goals = (int?)GrifballGoals.Infinite(p),
                CarrierKills = (int?)null,
                Betrayals = (int?)p.Betrayals,
                Suicides = (int?)p.Suicides,
                HammerKills = (int?)null,
                SwordKills = (int?)null,
                BallKills = (int?)null,
                Punches = (int?)null,
                DamageDealt = (int?)p.DamageDealt,
                DamageTaken = (int?)p.DamageTaken,
            }));

        var recorded = context.RecordedGameParticipants
            .Where(p => p.RecordedGame.MatchedMatch != null)
            .Select(p => new PlayerGameStat
            {
                SeasonID = p.RecordedGame.MatchedMatch!.MatchmakingSeasonID ?? 0,
                SeasonMatchID = (int?)null,
                MatchID = (Guid?)null,
                RecordedGameID = (int?)p.RecordedGameID,
                UserID = p.UserID,
                Gamertag = p.Gamertag,
                GameVersionID = p.RecordedGame.GameVersionID,
                Source = GameSource.Recorded,
                IsPlayoffs = false,
                TeamID = (int?)null,
                Won = p.RecordedGameTeam != null && p.RecordedGameTeam.Outcome == Outcomes.Won,
                Lost = p.RecordedGameTeam != null && p.RecordedGameTeam.Outcome == Outcomes.Lost,
                Kills = p.Kills,
                Deaths = p.Deaths,
                Assists = p.Assists,
                Score = p.Score,
                Goals = p.Goals,
                CarrierKills = p.CarrierKills,
                Betrayals = p.Betrayals,
                Suicides = p.Suicides,
                HammerKills = p.HammerKills,
                SwordKills = p.SwordKills,
                BallKills = p.BallKills,
                Punches = p.Punches,
                DamageDealt = (int?)null,
                DamageTaken = (int?)null,
            });

        return infinite.Concat(recorded);
    }
}
