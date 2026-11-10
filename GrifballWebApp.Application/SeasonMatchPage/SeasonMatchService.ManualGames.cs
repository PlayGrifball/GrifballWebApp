using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Domain.Halo.Match;
using GrifballWebApp.Application.Extensions;

namespace GrifballWebApp.Application.SeasonMatchPage;

/// <summary>
/// Manual match entry: a commissioner enters a game of a season match's series by hand, as a <see cref="RecordedGame"/>
/// in the series (RecordedGameQueries.DeleteManualForAsync), when the stat bot or Halo Infinite's stats have no record of it, or for a
/// game that is not Halo Infinite (MCC, a newer Halo). Its players' lines count in the stats like any recorded game's
/// (<see cref="Stats.PlayerGameStatsQuery"/>), and the series is scored from all its games, Waypoint and recorded alike.
/// </summary>
public partial class SeasonMatchService
{
    /// <summary>What a save that collides with another commissioner's change to the same series answers (two games
    /// added at once both take the next game number).</summary>
    public const string SeriesChangedMessage = "The series changed while this was being saved (another game was entered or changed at the same time): reload the page and try again";

    /// <summary>
    /// Refuses a new game, one not replacing a game of the series, where the series has no room for it: decided, or
    /// with all its games. The commissioner says which game a game replaces; the site never guesses it.
    /// </summary>
    private static void RefuseNewGame(SeasonMatch seasonMatch)
    {
        switch (seasonMatch.WhyNoNewGame())
        {
            case NewGameRefusal.Decided:
                throw new SeasonMatchActionNotAllowedException("Results for this match have already been decided: to correct a game, replace it");
            // Only inconsistent data gets here: a full series is always decided
            case NewGameRefusal.Full:
                throw new SeasonMatchActionNotAllowedException($"This series already has all {seasonMatch.BestOf} of its games");
        }
    }

    /// <summary>
    /// Makes room for a game that replaces one of the series (the commissioner's choice, never a guess): the result its
    /// games decided is taken back (in a bracket, what it sent on, as <see cref="RemoveGame"/>), and the game replaced
    /// is unreported (<see cref="Unreport"/>). Returns the game number the new game takes, so it plays the same part.
    /// </summary>
    private async Task<int> ReplaceAsync(SeasonMatch seasonMatch, int seriesGameID, CancellationToken ct)
    {
        var replaced = SeriesGameOf(seasonMatch, seriesGameID);
        await ReopenAsync(seasonMatch, ct);
        Unreport(seasonMatch, replaced);
        // Gone before the new game takes its number
        await SavingSeries(() => _context.SaveChangesAsync(ct));
        return replaced.GameNumber;
    }

    /// <summary>
    /// Takes a game out of the series: a Waypoint game goes back to the possible matches, to be reported again; a game
    /// entered by hand has nothing to go back to, so it is deleted.
    /// </summary>
    private void Unreport(SeasonMatch seasonMatch, SeriesGame game)
    {
        _context.RemoveSeriesGame(seasonMatch, game);
        if (game.RecordedGame is { IsManual: true } manual)
            _context.RemoveRecordedGame(manual);
    }

    private static SeriesGame SeriesGameOf(SeasonMatch seasonMatch, int seriesGameID) =>
        seasonMatch.SeriesGames.FirstOrDefault(g => g.SeriesGameID == seriesGameID)
        ?? throw new SeasonMatchActionNotAllowedException("That game is not one of this match's games");

    /// <summary>
    /// Adds a game entered by hand to the series, as a new game or replacing the one the commissioner picked
    /// (<see cref="ManualGameRequestDto.ReplacesSeriesGameID"/>), then scores the series: a game that wins it the series
    /// decides the match (and, in a bracket, sends the teams on). Returns the new game's RecordedGameID.
    /// </summary>
    public async Task<int> AddManualGame(int seasonMatchID, ManualGameRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetManualEntryMatch(seasonMatchID, ct);

        if (dto.ReplacesSeriesGameID is null)
            RefuseNewGame(seasonMatch);

        var users = await ValidateManualGame(dto, seasonMatch, null, ct);
        var number = dto.ReplacesSeriesGameID is int replaces ? await ReplaceAsync(seasonMatch, replaces, ct) : 0;

        // No season of its own: the game counts for its season through its series game only (RecordedGame.EnterByHand)
        var game = RecordedGame.EnterByHand(ManualFacts(seasonMatch, dto));
        SetManualSidesAndPlayers(game, seasonMatch, dto, users);
        _context.RecordedGames.Add(game);
        seasonMatch.AddGame(game, number);

        await SavingSeries(async () =>
        {
            await ScoreSeriesAsync(seasonMatch, ct);
            await _context.SaveChangesAsync(ct);
        });
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
        return game.RecordedGameID;
    }

    /// <summary>
    /// Replaces a game entered by hand with what was entered now. An edit that changes who won the game scores the
    /// series again: a match its games had decided is reopened first (in a bracket, what it sent on is taken back), so
    /// the edit can change who won the match. An edit that leaves the winner as it was (a kill count, a map) only
    /// rewrites the game: the result, and what it sent on, stay.
    /// </summary>
    public async Task EditManualGame(int seasonMatchID, int recordedGameID, ManualGameRequestDto dto, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetManualEntryMatch(seasonMatchID, ct);
        var seriesGame = ManualGameOf(seasonMatch, recordedGameID);
        var game = seriesGame.RecordedGame!;
        var users = await ValidateManualGame(dto, seasonMatch, game, ct);

        var homeWon = dto.HomeTeamScore > dto.AwayTeamScore;
        var sameWinner = RecordedWinners(game, seasonMatch) == (homeWon, !homeWon);
        // A new time places the game again among the others; without one it keeps its place
        var moved = dto.PlayedAt is not null && dto.PlayedAt != game.PlayedAt ? seriesGame : null;

        await SavingSeries(async () =>
        {
            if (sameWinner)
            {
                RewriteManualGame(game, seasonMatch, dto, users);
                await NumberGamesAsync(seasonMatch, moved, ct);
            }
            else
            {
                await ReopenAsync(seasonMatch, ct);
                RewriteManualGame(game, seasonMatch, dto, users);
                await ScoreSeriesAsync(seasonMatch, ct, moved);
            }
            await _context.SaveChangesAsync(ct);
        });
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Unreports one game of the series, at any point, Waypoint or entered by hand (<see cref="Unreport"/>), then scores
    /// the series again: a result its games decided is reopened first (in a bracket, what it sent on is taken back,
    /// refused once a later match has games of its own), so the games left can decide it again.
    /// </summary>
    public async Task RemoveGame(int seasonMatchID, int seriesGameID, CancellationToken ct = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var seasonMatch = await GetManualEntryMatch(seasonMatchID, ct);
        var seriesGame = SeriesGameOf(seasonMatch, seriesGameID);

        await ReopenAsync(seasonMatch, ct);
        Unreport(seasonMatch, seriesGame);

        await SavingSeries(async () =>
        {
            // Gone before the games after it move up into its number
            await _context.SaveChangesAsync(ct);
            await ScoreSeriesAsync(seasonMatch, ct);
            await _context.SaveChangesAsync(ct);
        });
        await AwardChampionship(seasonMatch, ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>
    /// Saves a change to a series' games. A save another commissioner's change to the same series got in ahead of (two
    /// games added at once take the same next game number, which the series' unique game numbers refuse) is answered
    /// as a rule, not a server error: nothing is saved, and the commissioner reloads and tries again.
    /// </summary>
    private static async Task SavingSeries(Func<Task> save)
    {
        try
        {
            await save();
        }
        catch (DbUpdateException e) when (e is not DbUpdateConcurrencyException)
        {
            throw new SeasonMatchActionNotAllowedException(SeriesChangedMessage);
        }
    }

    /// <summary>The season match, with what scoring it needs, if games can be entered by hand for it.</summary>
    private async Task<SeasonMatch> GetManualEntryMatch(int seasonMatchID, CancellationToken ct)
    {
        var seasonMatch = await GetSeasonMatch(seasonMatchID, ct)
            ?? throw new SeasonMatchActionNotAllowedException("Season match does not exist");

        if (seasonMatch.HomeTeamID is null || seasonMatch.AwayTeamID is null)
            throw new SeasonMatchActionNotAllowedException("Games can only be entered for a season match that has both teams assigned");

        // The import replaces an imported season's games with its archive's: one entered here would be removed
        if (await _context.Seasons.Where(Season.ImportedFromArchive).AnyAsync(s => s.SeasonID == seasonMatch.SeasonID, ct))
            throw new SeasonMatchActionNotAllowedException("This season was imported from the historical archive: correct its games in its spreadsheet");

        return seasonMatch;
    }

    private static SeriesGame ManualGameOf(SeasonMatch seasonMatch, int recordedGameID)
    {
        var seriesGame = seasonMatch.SeriesGames.FirstOrDefault(g => g.RecordedGameID == recordedGameID)
            ?? throw new SeasonMatchActionNotAllowedException("That game is not one of this match's games");
        if (seriesGame.RecordedGame is not { IsManual: true })
            throw new SeasonMatchActionNotAllowedException("Only a game entered by hand can be changed here");
        return seriesGame;
    }

    /// <summary>
    /// Takes a result the games decided back, so changing them can decide it again. A forfeit or a BYE is not the
    /// games' result: those are undone with Undo Result.
    /// </summary>
    private async Task ReopenAsync(SeasonMatch seasonMatch, CancellationToken ct)
    {
        if (seasonMatch.DecidedBy is SeasonMatchDecision.Forfeit or SeasonMatchDecision.Bye)
            throw new SeasonMatchActionNotAllowedException("This match was decided by a forfeit or a BYE, not its games: undo its result first");

        if (!seasonMatch.IsDecided)
            return;

        if (seasonMatch.BracketMatch is not null)
            await _bracketService.UndoAdvancementAsync(seasonMatch, ct);

        seasonMatch.Reopen();
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
        if ((dto.Map?.Trim().Length ?? 0) > RecordedGame.MapMaxLength || (dto.Gametype?.Trim().Length ?? 0) > RecordedGame.GametypeMaxLength)
            throw new SeasonMatchActionNotAllowedException($"The map and gametype are at most {RecordedGame.MapMaxLength} characters");
        if (!await _context.GameVersions.AnyAsync(g => g.GameVesionID == dto.GameVersionID, ct))
            throw new SeasonMatchActionNotAllowedException("Unknown game version");

        var players = dto.Players ?? [];
        if (players.Select(p => p.UserID).Distinct().Count() != players.Count)
            throw new SeasonMatchActionNotAllowedException("A player can only be entered once per game");
        if (players.Any(p => p.Kills < 0 || p.Deaths < 0 || p.Assists < 0 || p.Goals < 0))
            throw new SeasonMatchActionNotAllowedException("Kills, deaths, assists and goals can't be negative");

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

    /// <summary>What was entered, as the game's facts: a game in a bracket match is a playoff game.</summary>
    private static RecordedGameFacts ManualFacts(SeasonMatch seasonMatch, ManualGameRequestDto dto) => new(
        dto.GameVersionID,
        string.IsNullOrWhiteSpace(dto.Map) ? null : dto.Map.Trim(),
        string.IsNullOrWhiteSpace(dto.Gametype) ? null : dto.Gametype.Trim(),
        dto.PlayedAt,
        seasonMatch.BracketMatch is not null);

    /// <summary>Writes what was entered now onto a game entered by hand (<see cref="RecordedGame.EditByHand"/>).</summary>
    private static void RewriteManualGame(RecordedGame game, SeasonMatch seasonMatch, ManualGameRequestDto dto, Dictionary<int, User> users)
    {
        game.EditByHand(ManualFacts(seasonMatch, dto));
        SetManualSidesAndPlayers(game, seasonMatch, dto, users);
    }

    /// <summary>Writes the sides and players' lines entered onto the game: side 1 is the home team, side 2 the away team;
    /// its players' lines become those entered.</summary>
    private static void SetManualSidesAndPlayers(RecordedGame game, SeasonMatch seasonMatch, ManualGameRequestDto dto, Dictionary<int, User> users)
    {
        var homeWon = dto.HomeTeamScore > dto.AwayTeamScore;
        game.SetSides([
            new RecordedSideFacts(1, seasonMatch.HomeTeam!.TeamID, seasonMatch.HomeTeam.TeamName, homeWon ? Outcomes.Won : Outcomes.Lost, dto.HomeTeamScore),
            new RecordedSideFacts(2, seasonMatch.AwayTeam!.TeamID, seasonMatch.AwayTeam.TeamName, homeWon ? Outcomes.Lost : Outcomes.Won, dto.AwayTeamScore),
        ]);

        game.SetPlayers((dto.Players ?? []).Select(player =>
        {
            var user = users[player.UserID];
            // Never their user name, their private login name (UserNames.Pick)
            var gamertag = UserNames.Pick(user.XboxUser?.Gamertag, user.DiscordUser?.DiscordUsername, user.DisplayName, user.UserName);
            // Goals are read by the stats and the box score like an imported game's
            return new RecordedPlayerLine(user.Id, player.IsHomeTeam ? 1 : 2, gamertag, player.Kills, player.Deaths, player.Assists)
            {
                Goals = player.Goals,
                Score = player.Score,
            };
        }).ToList());
    }

    /// <summary>
    /// The series' games in order. The games already in it keep their order (their game numbers), so a game entered
    /// without a time keeps its place. A game being placed, one just added or <paramref name="moved"/> (edited with a
    /// new time), goes before the first game in the series known to have started after it, else last.
    /// </summary>
    private static List<SeriesGame> InSeriesOrder(SeasonMatch seasonMatch, SeriesGame? moved)
    {
        static DateTime? StartedAt(SeriesGame g) => g.Match is not null ? g.Match.StartTime : g.RecordedGame!.PlayedAt;

        var ordered = seasonMatch.SeriesGames
            .Where(g => g.GameNumber > 0 && g != moved)
            .OrderBy(g => g.GameNumber)
            .ToList();
        foreach (var game in seasonMatch.SeriesGames.Where(g => g.GameNumber <= 0 || g == moved).ToList())
        {
            var at = StartedAt(game) is { } time ? ordered.FindIndex(g => StartedAt(g) > time) : -1;
            ordered.Insert(at < 0 ? ordered.Count : at, game);
        }
        return ordered;
    }

    /// <summary>Numbers the series' games 1, 2, ... in <see cref="InSeriesOrder"/>; returns them in that order.</summary>
    private async Task<List<SeriesGame>> NumberGamesAsync(SeasonMatch seasonMatch, SeriesGame? moved, CancellationToken ct)
    {
        var orderedGames = InSeriesOrder(seasonMatch, moved);

        // A series has one game 1, one game 2...: games moving to a number another holds go through numbers nobody holds
        if (orderedGames.Where((game, index) => game.SeriesGameID != 0 && game.GameNumber != index + 1).Any())
        {
            foreach (var (game, index) in orderedGames.Select((game, index) => (game, index)))
                seasonMatch.NumberGame(game, -(index + 1));
            await _context.SaveChangesAsync(ct);
        }
        foreach (var (game, index) in orderedGames.Select((game, index) => (game, index)))
            seasonMatch.NumberGame(game, index + 1);
        return orderedGames;
    }

    /// <summary>
    /// Numbers the series' games (<see cref="NumberGamesAsync"/>) and scores the series from them, Waypoint and recorded
    /// alike: a team with the wins it needs (more than half the best-of) wins the match, and in a bracket goes on.
    /// The match's results are undecided when this is called.
    /// </summary>
    private async Task ScoreSeriesAsync(SeasonMatch seasonMatch, CancellationToken ct, SeriesGame? moved = null)
    {
        // A rostered player without a gamertag can't have played a Waypoint match: they are left out, not a 500.
        var rosters = await SeriesRosters.LoadAsync(_context, seasonMatch.HomeTeamID, seasonMatch.AwayTeamID, ct);

        var orderedGames = await NumberGamesAsync(seasonMatch, moved, ct);

        var homeScore = 0;
        var awayScore = 0;
        foreach (var game in orderedGames)
        {
            var (homeWon, awayWon) = Winners(game, seasonMatch, rosters);
            if (homeWon)
                homeScore++;
            if (awayWon)
                awayScore++;
        }

        // No games left: back to no score, as before any were reported (the match's rule)
        var decided = seasonMatch.DecideByGames(homeScore, awayScore);

        if (decided && seasonMatch.BracketMatch is not null)
        {
            HandleSettingNextMatchesInBracket(seasonMatch);
            // A team that lands opposite a BYE goes straight on to its next match
            await _bracketService.AdvanceByesAsync(seasonMatch.SeasonID, ct);
        }
    }

    /// <summary>Which side of the season match won a game of its series: a Waypoint match's by its players, a recorded
    /// game's by its sides' teams.</summary>
    private static (bool HomeWon, bool AwayWon) Winners(SeriesGame game, SeasonMatch seasonMatch, SeriesRosters rosters)
    {
        if (game.Match is not null)
        {
            // The game being reported now must match the rosters exactly
            if (game.SeriesGameID == 0 && GetTeams(game.Match, rosters) is null)
                throw new Exception("Unable to verify teams");
            return WaypointWinners(game, rosters);
        }

        return RecordedWinners(game.RecordedGame!, seasonMatch);
    }

    /// <summary>Which side of the season match won a recorded game: by its sides' teams.</summary>
    private static (bool HomeWon, bool AwayWon) RecordedWinners(RecordedGame game, SeasonMatch seasonMatch) =>
        (game.WonBy(seasonMatch.HomeTeamID), game.WonBy(seasonMatch.AwayTeamID));

    /// <summary>
    /// Which side of the season match won a Waypoint game, its sides decided by <see cref="SeriesSides"/> (as the box
    /// score and the match page decide them), so a game reported before whose players no longer match the rosters
    /// exactly (a player has since been traded, dropped or merged) still scores for the side it was. One that can't be
    /// decided any more is refused, not guessed.
    /// </summary>
    private static (bool HomeWon, bool AwayWon) WaypointWinners(SeriesGame game, SeriesRosters rosters)
    {
        var sides = SeriesSides.Of(game.Match!.MatchTeams.ToList(), t => rosters.Lean(t.MatchParticipants.Select(p => p.XboxUserID)))
            ?? throw new SeasonMatchActionNotAllowedException(
                $"Game {game.GameNumber}, from Waypoint, no longer matches the teams' rosters: undo the result and report the series again");
        return (sides.Home.Outcome is Outcomes.Won, sides.Away.Outcome is Outcomes.Won);
    }
}
