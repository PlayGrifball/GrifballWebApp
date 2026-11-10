using GrifballWebApp.Infrastructure;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Integration.Test;

/// <summary>A recorded game in the database: its sides and lines go with it, and the database refuses a game no method
/// makes (CK_Recorded_*).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RecordedGameAggregateDatabaseTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ASideOrLineTakenOff_IsDeleted_AndARemovedGameGoesWhole()
    {
        var season = await _context.AddSeason();
        var (alpha, bravo) = (await _context.AddUser("alpha"), await _context.AddUser("bravo"));
        var game = RecordedGame.Import("archive/1", new RecordedGameFacts(1), season.SeasonID);
        game.SetSides([new RecordedSideFacts(1, null, "Red", Outcomes.Won, 6), new RecordedSideFacts(2, null, "Blue", Outcomes.Lost, 2)]);
        game.SetPlayers([new RecordedPlayerLine(alpha.Id, 1, "Alpha", 5, 1, 0), new RecordedPlayerLine(bravo.Id, 2, "Bravo", 1, 5, 0)]);
        _context.RecordedGames.Add(game);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var loaded = await _context.RecordedGames.Include(g => g.Teams).Include(g => g.Participants).SingleAsync();
        var red = loaded.Teams.Single(t => t.Side == 1);
        var redLines = red.Participants.Select(p => (p.RecordedGameParticipantID, p.UserID)).ToList();
        var redGame = red.RecordedGame;
        loaded.SetSides([new RecordedSideFacts(1, null, "Red", Outcomes.Won, 6)]);
        loaded.SetPlayers([new RecordedPlayerLine(alpha.Id, 1, "Alpha", 5, 1, 0)]);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var lines = await _context.RecordedGameLines.Select(p => new { p.UserID, Side = (int?)p.RecordedGameTeam!.Side }).ToListAsync();
        var sides = await _context.Set<RecordedGameTeam>().Select(t => t.Side).ToListAsync();

        var again = await _context.RecordedGames.Include(g => g.Teams).Include(g => g.Participants).SingleAsync();
        _context.RemoveRecordedGame(again);
        await _context.SaveChangesAsync();

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(redGame, Is.SameAs(loaded), "A side knows its game");
            Assert.That(redLines, Has.Count.EqualTo(1).And.All.Matches<(int ID, int UserID)>(l => l.ID > 0 && l.UserID == alpha.Id), "And its players' lines, saved");
            Assert.That(lines.Select(l => (l.UserID, l.Side)), Is.EqualTo(new[] { (alpha.Id, (int?)1) }), "Bravo's line is deleted");
            Assert.That(sides, Is.EqualTo(new[] { 1 }), "Side 2 is deleted");
            Assert.That(await _context.RecordedGames.AnyAsync(), Is.False);
            Assert.That(await _context.Set<RecordedGameTeam>().AnyAsync(), Is.False);
            Assert.That(await _context.Set<RecordedGameParticipant>().AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task AGameNoMethodMakes_IsRefused()
    {
        var season = await _context.AddSeason();
        var next = 0;

        // Past the methods, as only something outside the class could (raw SQL, a bad migration)
        async Task<bool> Refused(RecordedGameOrigin origin, int? seasonID, Outcomes outcome)
        {
            await using var ctx = _context.NewContextLike();
            var game = RecordedGame.Seed($"raw/{next++}");
            var side = game.SeedSide(1);
            var entry = ctx.RecordedGames.Add(game);
            entry.Property(g => g.Origin).CurrentValue = origin;
            entry.Property(g => g.SeasonID).CurrentValue = seasonID;
            ctx.Entry(side).Property(t => t.Outcome).CurrentValue = outcome;
            try
            {
                await ctx.SaveChangesAsync();
                return false;
            }
            catch (DbUpdateException)
            {
                return true;
            }
        }

        await Assert.MultipleAsync(async () =>
        {
            Assert.That(await Refused((RecordedGameOrigin)3, null, Outcomes.Won), Is.True, "An origin the model doesn't know");
            Assert.That(await Refused(RecordedGameOrigin.Manual, season.SeasonID, Outcomes.Won), Is.True, "A game entered by hand in a season of its own");
            Assert.That(await Refused(RecordedGameOrigin.HistoricalImport, null, (Outcomes)0), Is.True, "A side with a result the model doesn't know");

            Assert.That(await Refused(RecordedGameOrigin.Manual, null, Outcomes.Lost), Is.False);
            Assert.That(await Refused(RecordedGameOrigin.HistoricalImport, season.SeasonID, Outcomes.DidNotFinish), Is.False);
            Assert.That(await Refused(RecordedGameOrigin.HistoricalImport, null, Outcomes.Tie), Is.False);
        });
    }
}

/// <summary>
/// The RecordedGameAggregate migration settles the rows its constraints would refuse before adding them: an unknown origin
/// is the import's, a game entered by hand leaves the season it was given, and a side's unknown result is none
/// (DidNotFinish). Alike on whichever database the suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RecordedGameAggregateMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task RowsTheConstraintsWouldRefuse_AreSettled_BeforeThem()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_RecordedGameAggregate"))) - 1]);

        var season = await _context.AddSeason();
        // Rows as the old model's public setters could leave them (the columns are as they were)
        var games = new Dictionary<string, RecordedGame>();
        void Row(string key, RecordedGameOrigin origin, int? seasonID, Outcomes outcome)
        {
            var game = RecordedGame.Seed(key);
            var side = game.SeedSide(1);
            var entry = _context.RecordedGames.Add(game);
            entry.Property(g => g.Origin).CurrentValue = origin;
            entry.Property(g => g.SeasonID).CurrentValue = seasonID;
            _context.Entry(side).Property(t => t.Outcome).CurrentValue = outcome;
            games[key] = game;
        }
        Row("imported", RecordedGameOrigin.HistoricalImport, season.SeasonID, Outcomes.Won);
        Row("manual", RecordedGameOrigin.Manual, null, Outcomes.Lost);
        Row("manual in a season", RecordedGameOrigin.Manual, season.SeasonID, Outcomes.Won);
        Row("an unknown origin", (RecordedGameOrigin)7, season.SeasonID, Outcomes.Tie);
        Row("no result", RecordedGameOrigin.HistoricalImport, null, (Outcomes)0);
        await _context.SaveChangesAsync();

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var after = await _context.RecordedGames.AsNoTracking().Include(g => g.Teams)
            .ToDictionaryAsync(g => g.SourceKey, g => (g.Origin, g.SeasonID, g.Teams.Single().Outcome));
        Assert.Multiple(() =>
        {
            Assert.That(after["imported"], Is.EqualTo((RecordedGameOrigin.HistoricalImport, (int?)season.SeasonID, Outcomes.Won)), "A legal row stays as it was");
            Assert.That(after["manual"], Is.EqualTo((RecordedGameOrigin.Manual, (int?)null, Outcomes.Lost)));
            Assert.That(after["manual in a season"], Is.EqualTo((RecordedGameOrigin.Manual, (int?)null, Outcomes.Won)), "Counts through its series game only");
            Assert.That(after["an unknown origin"], Is.EqualTo((RecordedGameOrigin.HistoricalImport, (int?)season.SeasonID, Outcomes.Tie)), "The import's, as every game before manual entry");
            Assert.That(after["no result"], Is.EqualTo((RecordedGameOrigin.HistoricalImport, (int?)null, Outcomes.DidNotFinish)));
        });
    }
}

/// <summary>
/// The RecordedGameOneLinePerUser migration leaves each user one line a game, the oldest, before its unique index: the
/// lines a merge of two accounts that both played a game left counted the player twice. Alike on whichever database the
/// suite runs on.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class RecordedGameOneLinePerUserMigrationTests
{
    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task ASecondLineOfAUserInAGame_IsDeleted_TheOldestStays()
    {
        var migrator = _context.GetService<IMigrator>();
        var migrations = _context.Database.GetMigrations().ToList();
        await migrator.MigrateAsync(migrations[migrations.IndexOf(migrations.Single(m => m.EndsWith("_RecordedGameOneLinePerUser"))) - 1]);

        var (alpha, bravo) = (User.Seed(userName: "alpha"), User.Seed(userName: "bravo"));
        _context.Users.AddRange(alpha, bravo);
        await _context.SaveChangesAsync();
        // Lines as a merge of two accounts that both played a game left them (one per save, so the keys are in order)
        var first = RecordedGame.Seed("archive/1");
        var second = RecordedGame.Seed("archive/2");
        _context.RecordedGames.AddRange(first, second);
        foreach (var (game, user, kills) in new[] { (first, alpha, 1), (first, alpha, 2), (first, bravo, 3), (first, alpha, 4), (second, alpha, 5) })
        {
            game.SeedPlayer(new RecordedPlayerLine(user.Id, null, user.UserName!, kills, 0, 0));
            await _context.SaveChangesAsync();
        }

        await migrator.MigrateAsync();

        _context.ChangeTracker.Clear();
        var lines = await _context.Set<RecordedGameParticipant>().AsNoTracking().OrderBy(p => p.RecordedGameParticipantID)
            .Select(p => new { p.RecordedGame.SourceKey, p.UserID, p.Kills }).ToListAsync();
        Assert.That(lines, Is.EqualTo(new[]
        {
            new { SourceKey = "archive/1", UserID = alpha.Id, Kills = 1 },
            new { SourceKey = "archive/1", UserID = bravo.Id, Kills = 3 },
            new { SourceKey = "archive/2", UserID = alpha.Id, Kills = 5 },
        }), "Alpha's oldest line in game 1 stays; their line in another game and Bravo's are left alone");
    }
}
