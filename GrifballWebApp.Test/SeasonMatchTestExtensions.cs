using GrifballWebApp.Database.Models;

namespace GrifballWebApp.Test;

/// <summary>
/// A test's season match in a state, reached through the match's own methods (its state has private setters). Seed one
/// with <see cref="SeasonMatch.Seed(int, int?, int?, int, DateTime?, MatchBracketInfo?)"/>, then decide it here.
/// </summary>
internal static class SeasonMatchTestExtensions
{
    /// <summary>A key, as the database would give (tests that never save it).</summary>
    public static SeasonMatch WithId(this SeasonMatch match, int id)
    {
        match.SeasonMatchID = id;
        return match;
    }

    /// <summary>Won by <paramref name="winner"/> on its games: the wins it needs (more than half the best-of) unless given,
    /// to <paramref name="loserWins"/>.</summary>
    public static SeasonMatch WonBy(this SeasonMatch match, SeasonMatchSide winner, int? winnerWins = null, int loserWins = 0)
    {
        var wins = winnerWins ?? (int)Math.Ceiling(match.BestOf * 0.5);
        var decided = winner is SeasonMatchSide.Home ? match.DecideByGames(wins, loserWins) : match.DecideByGames(loserWins, wins);
        if (!decided)
            throw new InvalidOperationException($"{wins}-{loserWins} does not decide a best of {match.BestOf}");
        return match;
    }

    /// <summary>Its series scored <paramref name="home"/> to <paramref name="away"/> so far: still undecided (short of the
    /// wins either team needs).</summary>
    public static SeasonMatch Scored(this SeasonMatch match, int home, int away)
    {
        if (match.DecideByGames(home, away))
            throw new InvalidOperationException($"{home}-{away} decides a best of {match.BestOf}: use WonBy");
        return match;
    }

    /// <summary>Forfeited by a side, or both.</summary>
    public static SeasonMatch Forfeited(this SeasonMatch match, Forfeiter forfeiter)
    {
        var result = match.Forfeit(forfeiter);
        if (result is not ForfeitResult.Forfeited)
            throw new InvalidOperationException($"The forfeit was refused: {result}");
        return match;
    }

    /// <summary>A BYE on a side (an empty one).</summary>
    public static SeasonMatch WithBye(this SeasonMatch match, SeasonMatchSide side)
    {
        match.PlaceBye(side);
        return match;
    }

    /// <summary>A BYE on a side, and the win of the team facing it.</summary>
    public static SeasonMatch WonOnBye(this SeasonMatch match, SeasonMatchSide byeSide)
    {
        match.PlaceBye(byeSide);
        match.AwardByeWin();
        return match;
    }

    /// <summary>Scheduled at a time.</summary>
    public static SeasonMatch At(this SeasonMatch match, DateTime? time)
    {
        match.ScheduleAt(time);
        return match;
    }

    /// <summary>A series game of it, seeded directly (the service's own way, AddGame, scores the series after).</summary>
    public static SeriesGame Game(this SeasonMatch match, int gameNumber, Match? halo = null, RecordedGame? recorded = null)
        => halo is not null ? match.AddGame(halo, gameNumber) : match.AddGame(recorded!, gameNumber);
}
