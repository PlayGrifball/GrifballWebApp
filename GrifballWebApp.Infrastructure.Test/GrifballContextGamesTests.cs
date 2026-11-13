using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// GrifballContext's views of a game's parts and its removals that take a game with its parts: a recorded game with its
/// sides and lines, a series game out of its season match (one, every one, or an imported series' one), on a real
/// (temp-file SQLite) database with its foreign keys, read back after each save.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GrifballContextGamesTests : SqliteContextFixture
{
    private static readonly int[] OnlyGameTwo = [2];
    private static readonly int[] BothGames = [1, 2];
    private int _seq;

    private async Task<User> AddUserAsync()
    {
        var user = User.Seed(userName: $"player{++_seq}");
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    /// <summary>A recorded game of two sides, a line each.</summary>
    private async Task<RecordedGame> AddRecordedGameAsync(string key)
    {
        var home = await AddUserAsync();
        var away = await AddUserAsync();
        var game = RecordedGame.Seed(key);
        var homeSide = game.SeedSide(1, outcome: Outcomes.Won, score: 5);
        var awaySide = game.SeedSide(2, outcome: Outcomes.Lost, score: 2);
        game.SeedPlayer(new RecordedPlayerLine(home.Id, 1, "Home", 10, 2, 1), homeSide);
        game.SeedPlayer(new RecordedPlayerLine(away.Id, 2, "Away", 3, 9, 0), awaySide);
        Context.RecordedGames.Add(game);
        await Context.SaveChangesAsync();
        return game;
    }

    private async Task<SeasonMatch> AddSeasonMatchAsync()
    {
        var season = Season.Seed(seasonName: "Games Season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, null, null, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        return match;
    }

    [Test]
    public async Task RemoveRecordedGame_TakesItsSidesAndLinesWithIt()
    {
        var kept = await AddRecordedGameAsync("kept");
        var removed = await AddRecordedGameAsync("removed");
        var linesBefore = await Context.RecordedGameLines.CountAsync();

        Context.RemoveRecordedGame(removed);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(linesBefore, Is.EqualTo(4));
            Assert.That(await Context.RecordedGames.Select(g => g.SourceKey).ToListAsync(), Is.EqualTo(new List<string> { kept.SourceKey }));
            Assert.That(await Context.RecordedGameLines.CountAsync(), Is.EqualTo(2));
            Assert.That(await Context.Set<RecordedGameTeam>().CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task RemoveSeriesGame_TakesTheGameOutOfTheSeries_KeepingTheRecordedGame()
    {
        var match = await AddSeasonMatchAsync();
        var first = match.AddGame(await AddRecordedGameAsync("g1"), 1);
        match.AddGame(await AddRecordedGameAsync("g2"), 2);
        await Context.SaveChangesAsync();

        Context.RemoveSeriesGame(match, first);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Context.SeasonSeriesGames.Select(g => g.GameNumber).ToListAsync(), Is.EqualTo(OnlyGameTwo));
            Assert.That(await Context.RecordedGames.CountAsync(), Is.EqualTo(2), "the recorded game stays, only its place in the series goes");
        }
    }

    [Test]
    public async Task ClearSeries_TakesEveryGameOut_AndReturnsThem()
    {
        var match = await AddSeasonMatchAsync();
        match.AddGame(await AddRecordedGameAsync("g1"), 1);
        match.AddGame(await AddRecordedGameAsync("g2"), 2);
        await Context.SaveChangesAsync();

        var cleared = Context.ClearSeries(match);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared.Select(g => g.GameNumber), Is.EquivalentTo(BothGames));
            Assert.That(await Context.SeasonSeriesGames.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task RemoveImportedSeriesGame_SaysWhetherTheSeriesHasGamesLeft()
    {
        var match = await AddSeasonMatchAsync();
        var first = match.AddImportedGame(null, await AddRecordedGameAsync("g1"), 1);
        var second = match.AddImportedGame(null, await AddRecordedGameAsync("g2"), 2);
        await Context.SaveChangesAsync();

        var leftAfterFirst = Context.RemoveImportedSeriesGame(match, first, homeWon: true, awayWon: false);
        var leftAfterSecond = Context.RemoveImportedSeriesGame(match, second, homeWon: false, awayWon: true);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((leftAfterFirst, leftAfterSecond), Is.EqualTo((true, false)));
            Assert.That(await Context.SeasonSeriesGames.AnyAsync(), Is.False);
        }
    }
}
