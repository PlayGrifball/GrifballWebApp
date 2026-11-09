using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Server.Stats;

/// <summary>
/// A player's totals over a run of games, a season's (<see cref="SeasonStatLine"/>) or a matchmaking season's
/// (<see cref="MatchmakingStatLine"/>): from the games in the app, or the totals a stats sheet gives (<see cref="PlayerStatTotals"/>).
/// Career totals add them up alike (<see cref="CareerTotals"/>).
/// </summary>
public abstract class StatLine
{
    /// <summary>What the line is of, for counting a career's seasons: one key per season or matchmaking season; null for
    /// games in none (the matchmaking queue's).</summary>
    public abstract string? SeasonKey { get; }

    /// <summary>The line's game: its games', which can differ from the season's (a season with Halo 3 and Reach tiers).</summary>
    public int? GameVersionID { get; set; }
    public string? GameVersionName { get; set; }
    public string? GameVersionSlug { get; set; }

    /// <summary>Totalled from the games in the app (true), or the totals stored for a player with no game data.</summary>
    public bool FromMatches { get; set; }
    public int UserID { get; set; }
    public required string Gamertag { get; set; }

    public int GamesPlayed { get; set; }
    /// <summary>Of <see cref="GamesPlayed"/>, the Late League games decided by their players' vote: a win or a loss with no box
    /// score (<see cref="GameSource.Vote"/>).</summary>
    public int GamesDecidedByVote { get; set; }
    /// <summary>The games with a box score: what a stat per game is over.</summary>
    public int GamesWithStats => GamesPlayed - GamesDecidedByVote;
    public int Wins { get; set; }
    public int Losses { get; set; }
    public decimal? WinPercent { get; set; }
    public long Goals { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public decimal? KdRatio { get; set; }
    public int? KdSpread { get; set; }
    public int CarrierKills { get; set; }
    /// <summary>The line counts carrier stops: stored totals do, games only where their source records them (Waypoint's where the mode packs them in its score: layouts B and C).</summary>
    public bool RecordsCarrierKills { get; set; } = true;
    public int Sprees { get; set; }
    public int MultiKills { get; set; }
    public int Exterminations { get; set; }
    public int Betrayals { get; set; }
    public int? HammerKills { get; set; }
    public int? SwordKills { get; set; }
    public int? BallKills { get; set; }
    public int? Punches { get; set; }
    public long? Score { get; set; }
    public int? DamageDealt { get; set; }
    public int? DamageTaken { get; set; }

    /// <summary>The totals a stats sheet gives, its own ratios where it has them, otherwise counted as the games' are.</summary>
    public void SetStored(PlayerStatTotals s, string? gameVersionName, string? gameVersionSlug)
    {
        GameVersionID = s.GameVersionID;
        GameVersionName = gameVersionName;
        GameVersionSlug = gameVersionSlug;
        UserID = s.UserID;
        Gamertag = s.Gamertag;
        GamesPlayed = s.GamesPlayed;
        Wins = s.Wins;
        Losses = s.Losses;
        WinPercent = s.WinPercent ?? StatMath.WinPercent(s.Wins, s.GamesPlayed);
        Goals = s.Goals;
        Kills = s.Kills;
        Deaths = s.Deaths;
        Assists = s.Assists;
        KdRatio = s.KdRatio ?? StatMath.KdRatio(s.Kills, s.Deaths);
        KdSpread = s.KdSpread ?? s.Kills - s.Deaths;
        CarrierKills = s.CarrierKills;
        Sprees = s.Sprees;
        MultiKills = s.MultiKills;
        Exterminations = s.Exterminations;
        Betrayals = s.Betrayals;
        HammerKills = s.HammerKills;
        SwordKills = s.SwordKills;
        BallKills = s.BallKills;
        Punches = s.Punches;
        Score = s.Score;
        DamageDealt = s.DamageDealt;
        DamageTaken = s.DamageTaken;
    }

    /// <summary>The totals of the player's games (<see cref="GameTotals"/>). A stat no game records is null.</summary>
    public void SetGames(GameTotals t, GameVersion? gameVersion)
    {
        static int? IfRecorded(int total, int recording) => recording > 0 ? total : null;

        GameVersionID = gameVersion?.GameVesionID;
        GameVersionName = gameVersion?.GameVersionName;
        GameVersionSlug = gameVersion?.Slug;
        FromMatches = true;
        UserID = t.UserID;
        GamesPlayed = t.Played;
        GamesDecidedByVote = t.DecidedByVote;
        Wins = t.Wins;
        Losses = t.Losses;
        WinPercent = StatMath.WinPercent(t.Wins, t.Played);
        Goals = t.Goals;
        Kills = t.Kills;
        Deaths = t.Deaths;
        Assists = t.Assists;
        KdRatio = StatMath.KdRatio(t.Kills, t.Deaths);
        KdSpread = t.Kills - t.Deaths;
        CarrierKills = t.CarrierKills;
        RecordsCarrierKills = t.RecordingCarrierKills > 0;
        Betrayals = t.Betrayals;
        HammerKills = IfRecorded(t.HammerKills, t.RecordingHammerKills);
        SwordKills = IfRecorded(t.SwordKills, t.RecordingSwordKills);
        BallKills = IfRecorded(t.BallKills, t.RecordingBallKills);
        Punches = IfRecorded(t.Punches, t.RecordingPunches);
        Score = t.RecordingScore > 0 ? t.Score : null;
        DamageDealt = IfRecorded(t.DamageDealt, t.RecordingDamageDealt);
        DamageTaken = IfRecorded(t.DamageTaken, t.RecordingDamageTaken);
    }
}

/// <summary>A player's games of one season (or matchmaking season), phase and team, totalled in the database: each stat
/// with how many of the games record it.</summary>
public class GameTotals
{
    public int SeasonID { get; set; }
    public int UserID { get; set; }
    public bool IsPlayoffs { get; set; }
    public int? TeamID { get; set; }
    public string Gamertag { get; set; } = "";
    public int GameVersionID { get; set; }
    public int Played { get; set; }
    /// <summary>Of <see cref="Played"/>, the games decided by vote, with no box score (<see cref="GameSource.Vote"/>).</summary>
    public int DecidedByVote { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Assists { get; set; }
    public long Goals { get; set; }
    public int CarrierKills { get; set; }
    public int RecordingCarrierKills { get; set; }
    public int Betrayals { get; set; }
    public int HammerKills { get; set; }
    public int RecordingHammerKills { get; set; }
    public int SwordKills { get; set; }
    public int RecordingSwordKills { get; set; }
    public int BallKills { get; set; }
    public int RecordingBallKills { get; set; }
    public int Punches { get; set; }
    public int RecordingPunches { get; set; }
    public long Score { get; set; }
    public int RecordingScore { get; set; }
    public int DamageDealt { get; set; }
    public int RecordingDamageDealt { get; set; }
    public int DamageTaken { get; set; }
    public int RecordingDamageTaken { get; set; }

    /// <summary>The games' totals by season, player, phase and team, added up in the database (both providers).</summary>
    public static IQueryable<GameTotals> Of(IQueryable<PlayerGameStat> games) => games
        .GroupBy(g => new { g.SeasonID, g.UserID, g.IsPlayoffs, g.TeamID })
        .Select(t => new GameTotals
        {
            SeasonID = t.Key.SeasonID,
            UserID = t.Key.UserID,
            IsPlayoffs = t.Key.IsPlayoffs,
            TeamID = t.Key.TeamID,
            Gamertag = t.Max(g => g.Gamertag)!,
            GameVersionID = t.Max(g => g.GameVersionID),
            Played = t.Count(),
            DecidedByVote = t.Count(g => g.Source == GameSource.Vote),
            Wins = t.Count(g => g.Won),
            Losses = t.Count(g => g.Lost),
            Kills = t.Sum(g => g.Kills),
            Deaths = t.Sum(g => g.Deaths),
            Assists = t.Sum(g => g.Assists),
            Goals = t.Sum(g => (long)(g.Goals ?? 0)),
            CarrierKills = t.Sum(g => g.CarrierKills ?? 0),
            RecordingCarrierKills = t.Count(g => g.CarrierKills != null),
            Betrayals = t.Sum(g => g.Betrayals ?? 0),
            HammerKills = t.Sum(g => g.HammerKills ?? 0),
            RecordingHammerKills = t.Count(g => g.HammerKills != null),
            SwordKills = t.Sum(g => g.SwordKills ?? 0),
            RecordingSwordKills = t.Count(g => g.SwordKills != null),
            BallKills = t.Sum(g => g.BallKills ?? 0),
            RecordingBallKills = t.Count(g => g.BallKills != null),
            Punches = t.Sum(g => g.Punches ?? 0),
            RecordingPunches = t.Count(g => g.Punches != null),
            Score = t.Sum(g => (long)(g.Score ?? 0)),
            RecordingScore = t.Count(g => g.Score != null),
            DamageDealt = t.Sum(g => g.DamageDealt ?? 0),
            RecordingDamageDealt = t.Count(g => g.DamageDealt != null),
            DamageTaken = t.Sum(g => g.DamageTaken ?? 0),
            RecordingDamageTaken = t.Count(g => g.DamageTaken != null),
        });
}
