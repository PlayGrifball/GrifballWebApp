using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Stats;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Database.Models.Match;

namespace GrifballWebApp.Server.SeasonMatchPage;

/// <summary>
/// Manual match entry: a commissioner enters a game of a season match's series by hand, as a <see cref="RecordedGame"/>
/// in the series, when the stat bot or Halo Infinite's stats have no record of it, or for a game that is not Halo
/// Infinite (MCC, a newer Halo). Its players' lines count in the stats like any recorded game's
/// (<see cref="Stats.PlayerGameStatsQuery"/>), and the series is scored from all its games, Waypoint and recorded alike.
/// </summary>
public partial class SeasonMatchService
{
    /// <summary>The <see cref="RecordedGame.SourceKey"/> prefix of a game entered by hand (the import's keys are its
    /// source files'): what makes a recorded game editable here.</summary>
    public const string ManualSourceKeyPrefix = "manual/";

    /// <summary>The longest map or gametype name (Recorded.Games).</summary>
    private const int MapAndGametypeLength = 100;

    /// <summary>How close to a hand-entered Halo Infinite game's time a Waypoint game must start to be taken for the
    /// same game (<see cref="RefuseIfEnteredByHand"/>): a game lasts about ten minutes and a time typed in is rough.</summary>
    internal static readonly TimeSpan SameGameWindow = TimeSpan.FromMinutes(15);

    internal static bool IsManual(RecordedGame? game) => game is not null && game.SourceKey.StartsWith(ManualSourceKeyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Deletes the games entered by hand of the season matches about to be deleted (regenerating a schedule or a
    /// bracket): the database removes a match's series games with it, but not the recorded games they point to, which
    /// would be left in no series.
    /// </summary>
    internal static async Task DeleteManualGamesAsync(GrifballContext context, IQueryable<SeasonMatch> seasonMatches, CancellationToken ct)
    {
        var ids = await context.SeriesGames
            .Where(g => g.RecordedGameID != null && g.RecordedGame!.SourceKey.StartsWith(ManualSourceKeyPrefix)
                && seasonMatches.Any(sm => sm.SeasonMatchID == g.SeasonMatchID))
            .Select(g => g.RecordedGameID!.Value)
            .ToListAsync(ct);
        if (ids.Count == 0)
            return;

        await context.RecordedGameParticipants.Where(p => ids.Contains(p.RecordedGameID)).ExecuteDeleteAsync(ct);
        await context.RecordedGameTeams.Where(t => ids.Contains(t.RecordedGameID)).ExecuteDeleteAsync(ct);
        await context.RecordedGames.Where(g => ids.Contains(g.RecordedGameID)).ExecuteDeleteAsync(ct);
    }

    /// <summary>
    /// Refuses a Waypoint game the series already has entered by hand: a Halo Infinite game entered with a time close to
    /// the Waypoint game's start. Without a time, a game entered by hand can't be told apart, so it does not refuse.
    /// </summary>
    private static void RefuseIfEnteredByHand(SeasonMatch seasonMatch, Match match)
    {
        var same = seasonMatch.SeriesGames.FirstOrDefault(g => IsManual(g.RecordedGame)
            && g.RecordedGame!.GameVersionID == PlayerGameStatsQuery.InfiniteGameVersionID
            && g.RecordedGame.PlayedAt is { } playedAt
            && (playedAt - match.StartTime).Duration() <= SameGameWindow);
        if (same is not null)
            throw new SeasonMatchActionNotAllowedException(
                $"Game {same.GameNumber} was entered by hand as a Halo Infinite game played around the same time: if it is this game, delete it first, then report this one");
    }

    /// <summary>
    /// Adds a game entered by hand to the series, then scores the series: a game that wins it the series decides the
    /// match (and, in a bracket, sends the teams on). Returns the new game's RecordedGameID.
    /// </summary>
    public async Task<int> AddManualGame(int seasonMatchID, ManualGameRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetManualEntryMatch(seasonMatchID, ct);

        if (seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null)
            throw new SeasonMatchActionNotAllowedException("Results for this match have already been decided");

        if (seasonMatch.SeriesGames.Count >= seasonMatch.BestOf)
            throw new SeasonMatchActionNotAllowedException($"This series already has all {seasonMatch.BestOf} of its games");

        var users = await ValidateManualGame(dto, seasonMatch, null, ct);

        var game = new RecordedGame
        {
            SourceKey = ManualSourceKeyPrefix + Guid.NewGuid().ToString("N"),
            // No SeasonID: the game counts for its season through its series game only, so it stops counting if its
            // season match goes (the stats count a recorded game with a SeasonID even outside any series)
        };
        ApplyManualGame(game, seasonMatch, dto, users);
        _context.RecordedGames.Add(game);
        seasonMatch.SeriesGames.Add(new SeriesGame { RecordedGame = game });

        await ScoreSeriesAsync(seasonMatch, ct);

        await _context.SaveChangesAsync(ct);
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
        return game.RecordedGameID;
    }

    /// <summary>
    /// Replaces a game entered by hand with what was entered now, then scores the series again. A match its games
    /// had decided is reopened first (in a bracket, what it sent on is taken back), so the edit can change who won.
    /// </summary>
    public async Task EditManualGame(int seasonMatchID, int recordedGameID, ManualGameRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetManualEntryMatch(seasonMatchID, ct);
        var seriesGame = ManualGameOf(seasonMatch, recordedGameID);
        var users = await ValidateManualGame(dto, seasonMatch, seriesGame.RecordedGame!, ct);

        await ReopenAsync(seasonMatch, ct);
        ApplyManualGame(seriesGame.RecordedGame!, seasonMatch, dto, users);
        await ScoreSeriesAsync(seasonMatch, ct);

        await _context.SaveChangesAsync(ct);
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>Deletes a game entered by hand from the series, then scores the series again (reopened first, as
    /// <see cref="EditManualGame"/>).</summary>
    public async Task DeleteManualGame(int seasonMatchID, int recordedGameID, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetManualEntryMatch(seasonMatchID, ct);
        var seriesGame = ManualGameOf(seasonMatch, recordedGameID);

        await ReopenAsync(seasonMatch, ct);
        seasonMatch.SeriesGames.Remove(seriesGame);
        _context.SeriesGames.Remove(seriesGame);
        RemoveRecordedGame(seriesGame.RecordedGame!);
        // Gone before the games after it move up into its number
        await _context.SaveChangesAsync(ct);

        await ScoreSeriesAsync(seasonMatch, ct);

        await _context.SaveChangesAsync(ct);
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>The season match, with what scoring it needs, if games can be entered by hand for it.</summary>
    private async Task<SeasonMatch> GetManualEntryMatch(int seasonMatchID, CancellationToken ct)
    {
        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct)
            ?? throw new SeasonMatchActionNotAllowedException("Season match does not exist");

        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            throw new SeasonMatchActionNotAllowedException("Games can only be entered for a season match that has both teams assigned");

        // The import replaces an imported season's games with its archive's: one entered here would be removed
        if (await _context.Seasons.AnyAsync(s => s.SeasonID == seasonMatch.SeasonID && s.HistoricalSourceID != null, ct))
            throw new SeasonMatchActionNotAllowedException("This season was imported from the historical archive: correct its games in its spreadsheet");

        return seasonMatch;
    }

    private static SeriesGame ManualGameOf(SeasonMatch seasonMatch, int recordedGameID)
    {
        var seriesGame = seasonMatch.SeriesGames.FirstOrDefault(g => g.RecordedGameID == recordedGameID)
            ?? throw new SeasonMatchActionNotAllowedException("That game is not one of this match's games");
        if (!IsManual(seriesGame.RecordedGame))
            throw new SeasonMatchActionNotAllowedException("Only a game entered by hand can be changed here");
        return seriesGame;
    }

    /// <summary>
    /// Takes a result the games decided back, so changing them can decide it again. A forfeit or a BYE is not the
    /// games' result: those are undone with Undo Result.
    /// </summary>
    private async Task ReopenAsync(SeasonMatch seasonMatch, CancellationToken ct)
    {
        if (seasonMatch.HomeTeamResult is SeasonMatchResult.Bye or SeasonMatchResult.Forfeit
            || seasonMatch.AwayTeamResult is SeasonMatchResult.Bye or SeasonMatchResult.Forfeit)
            throw new SeasonMatchActionNotAllowedException("This match was decided by a forfeit or a BYE, not its games: undo its result first");

        if (seasonMatch.HomeTeamResult is null && seasonMatch.AwayTeamResult is null)
            return;

        if (seasonMatch.BracketMatch is not null)
            await _bracketService.UndoAdvancementAsync(seasonMatch, ct);

        seasonMatch.HomeTeamResult = null;
        seasonMatch.AwayTeamResult = null;
    }

    /// <summary>
    /// Checks what was entered; returns its players' users, by ID. A rostered player plays for their team's side; a
    /// player on neither roster (a sub, or one who has left the team) only stays on a game they were already on, when
    /// it is edited.
    /// </summary>
    private async Task<Dictionary<int, User>> ValidateManualGame(ManualGameRequestDto dto, SeasonMatch seasonMatch, RecordedGame? editing, CancellationToken ct)
    {
        if (dto.HomeTeamScore < 0 || dto.AwayTeamScore < 0)
            throw new SeasonMatchActionNotAllowedException("A team's score can't be negative");
        if (dto.HomeTeamScore == dto.AwayTeamScore)
            throw new SeasonMatchActionNotAllowedException("A game needs a winner: the two scores can't be equal");
        if ((dto.Map?.Trim().Length ?? 0) > MapAndGametypeLength || (dto.Gametype?.Trim().Length ?? 0) > MapAndGametypeLength)
            throw new SeasonMatchActionNotAllowedException($"The map and gametype are at most {MapAndGametypeLength} characters");
        if (!await _context.GameVersions.AnyAsync(g => g.GameVesionID == dto.GameVersionID, ct))
            throw new SeasonMatchActionNotAllowedException("Unknown game version");

        var players = dto.Players ?? [];
        if (players.Select(p => p.UserID).Distinct().Count() != players.Count)
            throw new SeasonMatchActionNotAllowedException("A player can only be entered once per game");
        if (players.Any(p => p.Kills < 0 || p.Deaths < 0 || p.Assists < 0))
            throw new SeasonMatchActionNotAllowedException("Kills, deaths and assists can't be negative");

        var ids = players.Select(p => p.UserID).ToList();
        var users = await _context.Users
            .Include(u => u.XboxUser)
            .Include(u => u.DiscordUser)
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);
        if (users.Count != ids.Count)
            throw new SeasonMatchActionNotAllowedException("A player entered is not a user of the site");

        var home = seasonMatch.HomeTeam!;
        var away = seasonMatch.AwayTeam!;
        var homeRoster = home.TeamPlayers.Select(tp => tp.UserID).ToHashSet();
        var awayRoster = away.TeamPlayers.Select(tp => tp.UserID).ToHashSet();
        var alreadyOn = editing?.Participants.Select(p => p.UserID).ToHashSet() ?? [];
        foreach (var player in players)
        {
            var name = users[player.UserID].ToDisplayName() ?? users[player.UserID].UserName;
            if (homeRoster.Contains(player.UserID) && !player.IsHomeTeam)
                throw new SeasonMatchActionNotAllowedException($"{name} is on {home.TeamName}'s roster: they can't play for {away.TeamName}");
            if (awayRoster.Contains(player.UserID) && player.IsHomeTeam)
                throw new SeasonMatchActionNotAllowedException($"{name} is on {away.TeamName}'s roster: they can't play for {home.TeamName}");
            if (!homeRoster.Contains(player.UserID) && !awayRoster.Contains(player.UserID) && !alreadyOn.Contains(player.UserID))
                throw new SeasonMatchActionNotAllowedException($"{name} is on neither team's roster");
        }
        return users;
    }

    /// <summary>Writes what was entered onto the game: side 1 is the home team, side 2 the away team; its players'
    /// lines are replaced.</summary>
    private void ApplyManualGame(RecordedGame game, SeasonMatch seasonMatch, ManualGameRequestDto dto, Dictionary<int, User> users)
    {
        game.GameVersionID = dto.GameVersionID;
        game.Map = string.IsNullOrWhiteSpace(dto.Map) ? null : dto.Map.Trim();
        game.Gametype = string.IsNullOrWhiteSpace(dto.Gametype) ? null : dto.Gametype.Trim();
        game.PlayedAt = dto.PlayedAt;
        game.IsPlayoffs = seasonMatch.BracketMatch is not null;

        var homeWon = dto.HomeTeamScore > dto.AwayTeamScore;
        var home = Side(game, 1, seasonMatch.HomeTeam!, dto.HomeTeamScore, homeWon);
        var away = Side(game, 2, seasonMatch.AwayTeam!, dto.AwayTeamScore, !homeWon);

        foreach (var old in game.Participants.ToList())
        {
            game.Participants.Remove(old);
            _context.RecordedGameParticipants.Remove(old);
        }

        foreach (var player in dto.Players ?? [])
        {
            var user = users[player.UserID];
            game.Participants.Add(new RecordedGameParticipant
            {
                RecordedGame = game,
                RecordedGameTeam = player.IsHomeTeam ? home : away,
                UserID = user.Id,
                // A user always has a user name: what shows when they have no gamertag, Discord name or display name
                Gamertag = user.ToDisplayName() ?? user.UserName!,
                Kills = player.Kills,
                Deaths = player.Deaths,
                Assists = player.Assists,
                Score = player.Score,
            });
        }
    }

    private static RecordedGameTeam Side(RecordedGame game, int side, Team team, int score, bool won)
    {
        var gameTeam = game.Teams.FirstOrDefault(t => t.Side == side);
        if (gameTeam is null)
            game.Teams.Add(gameTeam = new RecordedGameTeam { RecordedGame = game, Side = side });
        gameTeam.TeamID = team.TeamID;
        gameTeam.Name = team.TeamName;
        gameTeam.Score = score;
        gameTeam.Outcome = won ? Outcomes.Won : Outcomes.Lost;
        return gameTeam;
    }

    /// <summary>Deletes a recorded game with its sides and players' lines (all loaded).</summary>
    private void RemoveRecordedGame(RecordedGame game)
    {
        _context.RecordedGameParticipants.RemoveRange(game.Participants);
        _context.RecordedGameTeams.RemoveRange(game.Teams);
        _context.RecordedGames.Remove(game);
    }

    /// <summary>
    /// Numbers the series' games in the order they were played and scores the series from them, Waypoint and recorded
    /// alike: a team with the wins it needs (more than half the best-of) wins the match, and in a bracket goes on.
    /// The match's results are undecided when this is called.
    /// </summary>
    private async Task ScoreSeriesAsync(SeasonMatch seasonMatch, CancellationToken ct)
    {
        // A rostered player without a gamertag can't have played a Waypoint match: they are left out, not a 500.
        var homeTeamIDs = XboxIDs(seasonMatch.HomeTeam!);
        var awayTeamIDs = XboxIDs(seasonMatch.AwayTeam!);

        // A game with no time (one entered without it) keeps its place after the timed ones
        var orderedGames = seasonMatch.SeriesGames
            .OrderBy(x => (x.Match is not null ? x.Match.StartTime : x.RecordedGame!.PlayedAt) ?? DateTime.MaxValue)
            .ThenBy(x => x.GameNumber > 0 ? x.GameNumber : int.MaxValue)
            .ToList();

        var homeScore = 0;
        var awayScore = 0;
        foreach (var game in orderedGames)
        {
            var (homeWon, awayWon) = Winners(game, seasonMatch, homeTeamIDs, awayTeamIDs);
            if (homeWon)
                homeScore++;
            if (awayWon)
                awayScore++;
        }

        // A series has one game 1, one game 2...: games moving to a number another holds go through numbers nobody holds
        if (orderedGames.Where((game, index) => game.SeriesGameID != 0 && game.GameNumber != index + 1).Any())
        {
            foreach (var (game, index) in orderedGames.Select((game, index) => (game, index)))
                game.GameNumber = -(index + 1);
            await _context.SaveChangesAsync(ct);
        }
        foreach (var (game, index) in orderedGames.Select((game, index) => (game, index)))
            game.GameNumber = index + 1;

        // No games left: back to no score, as before any were reported
        seasonMatch.HomeTeamScore = orderedGames.Count > 0 ? homeScore : null;
        seasonMatch.AwayTeamScore = orderedGames.Count > 0 ? awayScore : null;

        var winsRequired = Math.Ceiling(seasonMatch.BestOf * 0.5f);

        if (homeScore >= winsRequired)
        {
            seasonMatch.HomeTeamResult = SeasonMatchResult.Won;
            seasonMatch.AwayTeamResult = SeasonMatchResult.Loss;
        }
        else if (awayScore >= winsRequired)
        {
            seasonMatch.HomeTeamResult = SeasonMatchResult.Loss;
            seasonMatch.AwayTeamResult = SeasonMatchResult.Won;
        }

        if ((seasonMatch.HomeTeamResult is not null || seasonMatch.AwayTeamResult is not null) && seasonMatch.BracketMatch is not null)
        {
            HandleSettingNextMatchesInBracket(seasonMatch);
            // A team that lands opposite a BYE goes straight on to its next match
            await _bracketService.AdvanceByesAsync(seasonMatch.SeasonID, ct);
        }
    }

    private static List<long> XboxIDs(Team team) => team.TeamPlayers
        .Where(x => x.User.XboxUser is not null)
        .Select(x => x.User.XboxUser!.XboxUserID)
        .ToList();

    /// <summary>Which side of the season match won a game of its series: a Waypoint match's by its players, a recorded
    /// game's by its sides' teams.</summary>
    private (bool HomeWon, bool AwayWon) Winners(SeriesGame game, SeasonMatch seasonMatch, List<long> homeTeamIDs, List<long> awayTeamIDs)
    {
        if (game.Match is not null)
        {
            var teams = GetTeams(game.Match, homeTeamIDs: homeTeamIDs, awayTeamIDs: awayTeamIDs);
            if (teams is not null)
                return (teams.HomeTeam.Outcome is Outcomes.Won, teams.AwayTeam.Outcome is Outcomes.Won);
            // The game being reported now must match the rosters exactly
            if (game.SeriesGameID == 0)
                throw new Exception("Unable to verify teams");
            return ReportedWinners(game, homeTeamIDs, awayTeamIDs);
        }

        var sides = game.RecordedGame!.Teams;
        return (sides.Any(t => t.TeamID == seasonMatch.HomeTeamID && t.Outcome is Outcomes.Won),
            sides.Any(t => t.TeamID == seasonMatch.AwayTeamID && t.Outcome is Outcomes.Won));
    }

    /// <summary>
    /// A Waypoint game reported before whose players no longer match the rosters exactly (a player has since been
    /// traded, dropped or merged): its home side is the one with more of the home roster than of the away roster left.
    /// </summary>
    private static (bool HomeWon, bool AwayWon) ReportedWinners(SeriesGame game, List<long> homeTeamIDs, List<long> awayTeamIDs)
    {
        var sides = game.Match!.MatchTeams
            .Select(t => (Team: t, Lean: t.MatchParticipants.Count(p => homeTeamIDs.Contains(p.XboxUserID)) - t.MatchParticipants.Count(p => awayTeamIDs.Contains(p.XboxUserID))))
            .OrderByDescending(x => x.Lean)
            .ToList();
        if (sides.Count != 2 || sides[0].Lean == sides[1].Lean)
            throw new SeasonMatchActionNotAllowedException(
                $"Game {game.GameNumber}, from Waypoint, no longer matches the teams' rosters: undo the result and report the series again");
        return (sides[0].Team.Outcome is Outcomes.Won, sides[1].Team.Outcome is Outcomes.Won);
    }

    /// <summary>A recorded game of the series, for the match page: the home side is the one of the home team (side 1
    /// when no side has a team).</summary>
    private static RecordedSeriesGameDto ToRecordedSeriesGameDto(SeriesGame seriesGame, SeasonMatch seasonMatch)
    {
        var game = seriesGame.RecordedGame!;
        var home = game.Teams.FirstOrDefault(t => t.TeamID == seasonMatch.HomeTeamID)
            ?? game.Teams.FirstOrDefault(t => t.Side == 1);
        var away = game.Teams.FirstOrDefault(t => t.TeamID == seasonMatch.AwayTeamID)
            ?? game.Teams.FirstOrDefault(t => t.Side == 2);
        return new RecordedSeriesGameDto
        {
            RecordedGameID = game.RecordedGameID,
            GameNumber = seriesGame.GameNumber,
            GameVersionID = game.GameVersionID,
            GameVersionName = game.GameVersion.GameVersionName,
            Map = game.Map,
            Gametype = game.Gametype,
            PlayedAt = game.PlayedAt,
            HomeTeamScore = home?.Score,
            AwayTeamScore = away?.Score,
            HomeTeamWon = home?.Outcome is Outcomes.Won,
            AwayTeamWon = away?.Outcome is Outcomes.Won,
            IsManual = IsManual(game),
            Players = game.Participants
                .OrderBy(p => p.RecordedGameTeamID != home?.RecordedGameTeamID)
                .ThenBy(p => p.RecordedGameParticipantID)
                .Select(p => new RecordedSeriesGamePlayerDto
                {
                    UserID = p.UserID,
                    Gamertag = p.Gamertag,
                    IsHomeTeam = home is not null && p.RecordedGameTeamID == home.RecordedGameTeamID,
                    Kills = p.Kills,
                    Deaths = p.Deaths,
                    Assists = p.Assists,
                    Score = p.Score,
                })
                .ToArray(),
        };
    }
}
