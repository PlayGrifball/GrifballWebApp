using GrifballWebApp.Application.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using GrifballWebApp.Application.Queries;

namespace GrifballWebApp.Application.Stats;

/// <summary>Where a <see cref="PlayerGameStat"/> comes from.</summary>
public enum GameSource
{
    /// <summary>A Halo Infinite match pulled from Waypoint (Infinite.MatchParticipants).</summary>
    Infinite = 1,
    /// <summary>A recorded game, e.g. an MCC carnage report (Recorded.GameParticipants).</summary>
    Recorded = 2,
    /// <summary>A Late League game decided by its players' vote (<see cref="MatchedMatchStatus.VoteDecided"/>): a result with
    /// no box score, counted in games played, wins and losses only (<see cref="GameTotals.DecidedByVote"/>).</summary>
    Vote = 3,
}

/// <summary>One player's line of one game that counts for a season: in a season match's series, or a recorded game of
/// the season outside any series. For a matchmaking game (<see cref="PlayerGameStatsQuery.Matchmaking"/>) SeasonID is its
/// matchmaking season's, 0 for the queue's games (in none).</summary>
public class PlayerGameStat
{
    /// <summary>The <see cref="UserID"/> of a Waypoint player whose Xbox account has no user (no user's ID is 0).</summary>
    public const int NoUser = 0;

    public int SeasonID { get; set; }
    public int? SeasonMatchID { get; set; }
    public Guid? MatchID { get; set; }
    public int? RecordedGameID { get; set; }
    /// <summary>The player's user; <see cref="NoUser"/> for a Waypoint player with none, which only
    /// <see cref="PlayerGameStatsQuery.Series"/> has.</summary>
    public int UserID { get; set; }
    /// <summary>A Waypoint player's Xbox account: tells players with no user apart, and places them on a roster.</summary>
    public long? XboxUserID { get; set; }
    /// <summary>The team in the game: a Waypoint match team's TeamID, a recorded game's side.</summary>
    public int? InGameTeam { get; set; }
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
    /// <summary>Infinite's personal score; null where a game doesn't record one, or its mode packs its stats into it.</summary>
    public int? Score { get; set; }
    /// <summary>Grifball goals: an Infinite player's as <see cref="GrifballGoals"/> defines them (decoded from their mode score, #217;
    /// null when it can't be), a recorded game's as recorded.</summary>
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

    public static IQueryable<PlayerGameStat> Query(IApplicationDbContext context) =>
        // A Waypoint game's player is their Xbox account's user; an account with none is left out (nobody to show it for)
        SeriesInfinite(context, context.HaloMatchParticipants.Where(p => p.MatchTeam.Match.SeriesGame != null && p.XboxUser.User != null))
            .Concat(SeasonRecorded(context.RecordedGameLines.Where(p => p.RecordedGame.SeriesGame != null || p.RecordedGame.SeasonID != null)));

    /// <summary>
    /// Every player line of one season match's series, Waypoint and recorded alike: the box score's lines, so it counts
    /// as the season stats do. Unlike <see cref="Query"/> it keeps a Waypoint player with no user (a ringer), as
    /// <see cref="PlayerGameStat.NoUser"/>: the box score still shows them, by their gamertag.
    /// </summary>
    public static IQueryable<PlayerGameStat> Series(IApplicationDbContext context, int seasonMatchID) =>
        SeriesInfinite(context, context.HaloMatchParticipants.Where(p => p.MatchTeam.Match.SeriesGame!.SeasonMatchID == seasonMatchID))
            .Concat(SeasonRecorded(context.RecordedGameLines.Where(p => p.RecordedGame.SeriesGame!.SeasonMatchID == seasonMatchID)));

    /// <summary>The lines of Waypoint players of series games.</summary>
    private static IQueryable<PlayerGameStat> SeriesInfinite(IApplicationDbContext context, IQueryable<MatchParticipant> participants) =>
        participants
            .Select(GrifballGoals.Inline((MatchParticipant p) => new PlayerGameStat
            {
                SeasonID = p.MatchTeam.Match.SeriesGame!.SeasonMatch.SeasonID,
                SeasonMatchID = (int?)p.MatchTeam.Match.SeriesGame.SeasonMatchID,
                MatchID = (Guid?)p.MatchID,
                RecordedGameID = (int?)null,
                UserID = p.XboxUser.User != null ? p.XboxUser.User.Id : PlayerGameStat.NoUser,
                XboxUserID = (long?)p.XboxUserID,
                InGameTeam = (int?)p.TeamID,
                Gamertag = p.XboxUser.Gamertag,
                GameVersionID = InfiniteGameVersionID,
                Source = GameSource.Infinite,
                IsPlayoffs = p.MatchTeam.Match.SeriesGame.SeasonMatch.BracketMatch != null,
                // The side of the season match whose roster the player is on
                TeamID = context.Teams.Rosters().Any(tp => tp.TeamID == p.MatchTeam.Match.SeriesGame.SeasonMatch.HomeTeamID && tp.UserID == p.XboxUser.User!.Id)
                    ? p.MatchTeam.Match.SeriesGame.SeasonMatch.HomeTeamID
                    : context.Teams.Rosters().Any(tp => tp.TeamID == p.MatchTeam.Match.SeriesGame.SeasonMatch.AwayTeamID && tp.UserID == p.XboxUser.User!.Id)
                        ? p.MatchTeam.Match.SeriesGame.SeasonMatch.AwayTeamID
                        : null,
                Won = p.MatchTeam.Outcome == Outcomes.Won,
                Lost = p.MatchTeam.Outcome == Outcomes.Lost,
                Kills = p.Kills,
                Deaths = p.Deaths,
                Assists = p.Assists,
                // Halo's points, unless the mode packs its stats into it (#217): the model's rule, inlined
                Score = p.MatchTeam.Match.Apply(Match.WithHalosPoints) ? (int?)p.PersonalScore : null,
                // Grifball's goals (GrifballGoals: decoded from the mode's Score, #217)
                Goals = GrifballGoals.Infinite(p),
                CarrierKills = p.CarrierKills,
                Betrayals = (int?)p.Betrayals,
                Suicides = (int?)p.Suicides,
                HammerKills = (int?)null,
                SwordKills = p.SwordKills,
                BallKills = (int?)null,
                Punches = p.BallPunches,
                DamageDealt = (int?)p.DamageDealt,
                DamageTaken = (int?)p.DamageTaken,
            }));

    /// <summary>The lines of a season's recorded games, in a series or not. Same members, same order as
    /// <see cref="SeriesInfinite"/>: the two halves are one UNION ALL.</summary>
    private static IQueryable<PlayerGameStat> SeasonRecorded(IQueryable<RecordedGameParticipant> participants) =>
        participants
            .Select(p => new PlayerGameStat
            {
                SeasonID = p.RecordedGame.SeriesGame != null ? p.RecordedGame.SeriesGame.SeasonMatch.SeasonID : p.RecordedGame.SeasonID!.Value,
                SeasonMatchID = p.RecordedGame.SeriesGame != null ? (int?)p.RecordedGame.SeriesGame.SeasonMatchID : null,
                MatchID = (Guid?)null,
                RecordedGameID = (int?)p.RecordedGameID,
                UserID = p.UserID,
                XboxUserID = (long?)null,
                InGameTeam = p.RecordedGameTeam != null ? (int?)p.RecordedGameTeam.Side : null,
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

    /// <summary>
    /// Every player game of matchmaking (Late League), as one query: the queue's Halo Infinite matches once reported, and
    /// the archive's recorded games, each a <see cref="MatchedMatch"/>, and the queue's matches decided by vote (results with
    /// no box score, <see cref="GameSource.Vote"/>). SeasonID is the matchmaking season's (0: the queue's,
    /// in none); there are no playoffs or teams.
    /// </summary>
    public static IQueryable<PlayerGameStat> Matchmaking(IApplicationDbContext context)
    {
        var infinite = context.HaloMatchParticipants
            .Where(p => p.MatchTeam.Match.MatchedMatch != null && p.XboxUser.User != null)
            .Select(GrifballGoals.Inline((MatchParticipant p) => new PlayerGameStat
            {
                SeasonID = p.MatchTeam.Match.MatchedMatch!.MatchmakingSeasonID ?? 0,
                SeasonMatchID = (int?)null,
                MatchID = (Guid?)p.MatchID,
                RecordedGameID = (int?)null,
                UserID = p.XboxUser.User!.Id,
                XboxUserID = (long?)p.XboxUserID,
                InGameTeam = (int?)p.TeamID,
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
                // Halo's points, unless the mode packs its stats into it (#217): the model's rule, inlined
                Score = p.MatchTeam.Match.Apply(Match.WithHalosPoints) ? (int?)p.PersonalScore : null,
                // Grifball's goals (GrifballGoals: decoded from the mode's Score, #217)
                Goals = GrifballGoals.Infinite(p),
                CarrierKills = p.CarrierKills,
                Betrayals = (int?)p.Betrayals,
                Suicides = (int?)p.Suicides,
                HammerKills = (int?)null,
                SwordKills = p.SwordKills,
                BallKills = (int?)null,
                Punches = p.BallPunches,
                DamageDealt = (int?)p.DamageDealt,
                DamageTaken = (int?)p.DamageTaken,
            }));

        var recorded = context.RecordedGameLines
            .Where(p => p.RecordedGame.MatchedMatch != null)
            .Select(p => new PlayerGameStat
            {
                SeasonID = p.RecordedGame.MatchedMatch!.MatchmakingSeasonID ?? 0,
                SeasonMatchID = (int?)null,
                MatchID = (Guid?)null,
                RecordedGameID = (int?)p.RecordedGameID,
                UserID = p.UserID,
                XboxUserID = (long?)null,
                InGameTeam = p.RecordedGameTeam != null ? (int?)p.RecordedGameTeam.Side : null,
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

        // A game decided by its players' vote has no Halo match, so no box score: a game played, won or lost, by the players
        // still in it (MatchedPlayer.CountedByVote), and nothing else (#296). Named as the app names a user
        // (User.Named): a player whose Xbox account was unlinked still has a name (their Discord or display name)
        var voted = context.MatchedMatches.VoteResults()
            .Join(context.Users.Select(User.Named), r => r.UserID, u => u.UserID, (r, u) => new PlayerGameStat
            {
                SeasonID = r.MatchmakingSeasonID ?? 0,
                SeasonMatchID = (int?)null,
                MatchID = (Guid?)null,
                RecordedGameID = (int?)null,
                UserID = r.UserID,
                XboxUserID = (long?)null,
                InGameTeam = (int?)null,
                Gamertag = u.Name ?? "",
                GameVersionID = InfiniteGameVersionID,
                Source = GameSource.Vote,
                IsPlayoffs = false,
                TeamID = (int?)null,
                Won = r.Won,
                Lost = !r.Won,
                Kills = 0,
                Deaths = 0,
                Assists = 0,
                Score = (int?)null,
                Goals = (int?)null,
                CarrierKills = (int?)null,
                Betrayals = (int?)null,
                Suicides = (int?)null,
                HammerKills = (int?)null,
                SwordKills = (int?)null,
                BallKills = (int?)null,
                Punches = (int?)null,
                DamageDealt = (int?)null,
                DamageTaken = (int?)null,
            });

        return infinite.Concat(recorded).Concat(voted);
    }
}
