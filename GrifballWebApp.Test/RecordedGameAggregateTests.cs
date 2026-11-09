using GrifballWebApp.Database;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GrifballWebApp.Test;

/// <summary>A recorded game begins through its factories, and its sides and players' lines change only through its methods.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RecordedGameAggregateTests
{
    private static readonly DateTime At = new(2024, 3, 1, 20, 0, 0, DateTimeKind.Utc);
    private static readonly RecordedGameFacts Facts = new(1, "Pit", "Grifball", At, IsPlayoffs: true);

    private static RecordedSideFacts Side(int side, int? teamID, Outcomes outcome, int? score = null, string? name = null) => new(side, teamID, name, outcome, score);

    private static RecordedPlayerLine Line(int userID, int? side, string gamertag = "Player", int kills = 1) => new(userID, side, gamertag, kills, 2, 3);

    [Test]
    public void AnImportedGame_HasItsFacts_InItsSeason()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);

        Assert.Multiple(() =>
        {
            Assert.That((game.SourceKey, game.Origin, game.SeasonID), Is.EqualTo(("archive/1", RecordedGameOrigin.HistoricalImport, (int?)7)));
            Assert.That((game.GameVersionID, game.Map, game.Gametype, game.PlayedAt, game.IsPlayoffs), Is.EqualTo((1, "Pit", "Grifball", (DateTime?)At, true)));
            Assert.That(game.IsManual, Is.False);
        });
    }

    [Test]
    public void AGameEnteredByHand_IsManual_WithAKeyOfItsOwn_AndNoSeason()
    {
        var game = RecordedGame.EnterByHand(Facts);
        var other = RecordedGame.EnterByHand(Facts);

        Assert.Multiple(() =>
        {
            Assert.That(game.IsManual, Is.True);
            Assert.That(game.Origin, Is.EqualTo(RecordedGameOrigin.Manual));
            Assert.That(game.SeasonID, Is.Null);
            Assert.That(game.SourceKey, Does.StartWith(RecordedGame.ManualSourceKeyPrefix));
            Assert.That(game.SourceKey, Is.Not.EqualTo(other.SourceKey));
        });
    }

    [Test]
    public void AGame_IsRefused_WithoutItsFacts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => RecordedGame.Import(" ", Facts, null), Throws.ArgumentException, "A key");
            Assert.That(() => RecordedGame.Import(new string('k', RecordedGame.SourceKeyMaxLength + 1), Facts, null), Throws.ArgumentException, "A key a row holds");
            Assert.That(() => RecordedGame.Import("archive/1", null!, null), Throws.ArgumentNullException);
            Assert.That(() => RecordedGame.Import("archive/1", Facts with { GameVersionID = 0 }, null), Throws.ArgumentException, "A game version");
            Assert.That(() => RecordedGame.EnterByHand(Facts with { Map = new string('m', RecordedGame.MapMaxLength + 1) }), Throws.ArgumentException, "A map a row holds");
            Assert.That(() => RecordedGame.EnterByHand(Facts with { Gametype = new string('g', RecordedGame.GametypeMaxLength + 1) }), Throws.ArgumentException, "A gametype a row holds");
        });
    }

    [Test]
    public void TheImport_ChangesOnlyItsOwnGames_AndTheMatchPage_OnlyGamesEnteredByHand()
    {
        var imported = RecordedGame.Import("archive/1", Facts, 7);
        var manual = RecordedGame.EnterByHand(Facts);

        imported.Reimport(Facts with { Map = "Lockout", IsPlayoffs = false }, null);
        manual.EditByHand(Facts with { Gametype = "Hammerball" });

        Assert.Multiple(() =>
        {
            Assert.That((imported.Map, imported.IsPlayoffs, imported.SeasonID), Is.EqualTo(("Lockout", false, (int?)null)));
            Assert.That(manual.Gametype, Is.EqualTo("Hammerball"));
            Assert.That(() => manual.Reimport(Facts, 7), Throws.InvalidOperationException, "The import never touches a game entered by hand");
            Assert.That(() => imported.EditByHand(Facts), Throws.InvalidOperationException, "The match page edits only a game entered by hand");
            Assert.That(manual.SeasonID, Is.Null, "Still in no season");
        });
    }

    [Test]
    public void ItsSides_AreFoundByNumber_KeptChangedAddedAndRemoved()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetSides([Side(1, 10, Outcomes.Won, 6, "Red"), Side(2, 20, Outcomes.Lost, 2), Side(3, null, Outcomes.Lost)]);
        var first = game.Teams.Single(t => t.Side == 1);

        game.SetSides([Side(1, 11, Outcomes.Lost, 3, "Red Two"), Side(2, 20, Outcomes.Won, 5), Side(4, null, Outcomes.Tie)]);

        Assert.Multiple(() =>
        {
            Assert.That(game.Teams.OrderBy(t => t.Side).Select(t => (t.Side, t.TeamID, t.Name, t.Outcome, t.Score)), Is.EqualTo(new[]
            {
                (1, (int?)11, (string?)"Red Two", Outcomes.Lost, (int?)3),
                (2, (int?)20, (string?)null, Outcomes.Won, (int?)5),
                (4, (int?)null, (string?)null, Outcomes.Tie, (int?)null),
            }));
            Assert.That(game.Teams, Does.Contain(first), "Side 1 is changed in place, not replaced");
        });
    }

    [Test]
    public void ItsSides_AreRefused_TwiceOrWithAnUnknownResult()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);

        Assert.Multiple(() =>
        {
            Assert.That(() => game.SetSides([Side(1, 10, Outcomes.Won), Side(1, 20, Outcomes.Lost)]), Throws.ArgumentException, "A side listed twice");
            Assert.That(() => game.SetSides([Side(1, 10, (Outcomes)0)]), Throws.ArgumentException, "A result a game knows");
            Assert.That(game.Teams, Is.Empty, "Nothing is changed by a refused call");
        });
    }

    [Test]
    public void ItsPlayers_AreFoundByUser_OnTheirSideByNumber()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetSides([Side(1, 10, Outcomes.Won), Side(2, 20, Outcomes.Lost)]);
        game.SetPlayers([Line(100, 1, "Alpha"), Line(200, 2, "Bravo"), Line(300, 2, "Charlie")]);
        var alpha = game.Participants.Single(p => p.UserID == 100);

        game.SetPlayers([Line(100, 2, "Alpha Two", kills: 9) with { Goals = 4, Punches = 1 }, Line(200, 5, "Bravo"), Line(400, null, "Delta")]);

        Assert.Multiple(() =>
        {
            Assert.That(game.Participants.OrderBy(p => p.UserID).Select(p => (p.UserID, p.Gamertag, p.RecordedGameTeam?.Side)), Is.EqualTo(new[]
            {
                (100, "Alpha Two", (int?)2),
                (200, "Bravo", (int?)null),
                (400, "Delta", (int?)null),
            }), "Charlie is gone; a side the game doesn't have, or none, puts a player on no side");
            Assert.That(game.Participants, Does.Contain(alpha), "Changed in place, not replaced");
            Assert.That((alpha.Kills, alpha.Deaths, alpha.Assists, alpha.Goals, alpha.Punches, alpha.Score), Is.EqualTo((9, 2, 3, (int?)4, (int?)1, (int?)null)));
            Assert.That(() => game.SetPlayers([Line(100, 1), Line(100, 2)]), Throws.ArgumentException, "One line a player");
        });
    }

    [Test]
    public void ItsPlayers_LeaveAUserWithTwoLines_WithOne()
    {
        // Two lines for one user, as a merge before ReassignPlayer folded them left a game
        var game = RecordedGame.Seed("manual/1", origin: RecordedGameOrigin.Manual);
        var side = game.SeedSide(1);
        var first = game.SeedPlayer(Line(100, 1, "Alpha"), side);
        first.RecordedGameParticipantID = 1;
        game.SeedPlayer(Line(100, 1, "Alpha Alt"), side).RecordedGameParticipantID = 2;
        game.SeedPlayer(Line(200, 1, "Bravo"), side).RecordedGameParticipantID = 3;
        game.SeedPlayer(Line(200, 1, "Bravo Alt"), side).RecordedGameParticipantID = 4;

        game.SetPlayers([Line(100, 1, "Alpha", kills: 7)]);

        Assert.Multiple(() =>
        {
            Assert.That(game.Participants.Select(p => (p.UserID, p.Gamertag, p.Kills)), Is.EqualTo(new[] { (100, "Alpha", 7) }),
                "One line for the player listed, none for the one gone: the stats count each once");
            Assert.That(game.Participants, Does.Contain(first), "The oldest line is the one kept, changed in place");
        });
    }

    [Test]
    public void AMerge_IntoAPlayerOfTheGame_KeepsTheirLine_AndTheSourcesGoes()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetPlayers([Line(100, null, "Old Tag", kills: 3), Line(300, null, "Real", kills: 5), Line(200, null)]);
        var real = game.Participants.Single(p => p.UserID == 300);

        var moved = game.ReassignPlayer(100, 300);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.Zero, "Nothing moved: the source's line went");
            Assert.That(game.Participants.Select(p => p.UserID).Order(), Is.EqualTo(new[] { 200, 300 }));
            Assert.That(game.Participants, Does.Contain(real), "The target's line stays");
            Assert.That(real.Kills, Is.EqualTo(5));
            Assert.That(game.ReassignPlayer(300, 300), Is.Zero, "Into itself: nothing to do");
            Assert.That(game.Participants, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void AMerge_OfAUserWithTwoLines_MovesOneAndTheOtherGoes()
    {
        var game = RecordedGame.Seed("archive/1");
        game.SeedPlayer(Line(100, null, "Old Tag", kills: 3)).RecordedGameParticipantID = 1;
        game.SeedPlayer(Line(100, null, "Old Tag", kills: 4)).RecordedGameParticipantID = 2;

        var moved = game.ReassignPlayer(100, 300);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.EqualTo(1));
            Assert.That(game.Participants.Select(p => (p.UserID, p.Kills)), Is.EqualTo(new[] { (300, 3) }), "The oldest moves");
        });
    }

    [Test]
    public void AMerge_MovesOnlyThatPlayersLines()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetPlayers([Line(100, null), Line(200, null)]);

        var moved = game.ReassignPlayer(100, 300);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.EqualTo(1));
            Assert.That(game.Participants.Select(p => p.UserID).Order(), Is.EqualTo(new[] { 200, 300 }));
            Assert.That(game.ReassignPlayer(999, 300), Is.Zero, "Nobody's lines to move");
        });
    }

    [Test]
    public void TeamsGoing_LeaveTheirSidesWithoutATeam_ByName()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetSides([Side(1, 10, Outcomes.Won, name: "Red"), Side(2, 20, Outcomes.Lost, name: "Blue")]);

        game.UnlinkTeams([10, 30]);

        Assert.That(game.Teams.OrderBy(t => t.Side).Select(t => (t.TeamID, t.Name)), Is.EqualTo(new[] { ((int?)null, (string?)"Red"), ((int?)20, (string?)"Blue") }));
    }

    [Test]
    public void ATeamWinsTheGame_ByASideOfItThatWon()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetSides([Side(1, 10, Outcomes.Won), Side(2, 20, Outcomes.Lost), Side(3, 30, Outcomes.Tie)]);

        Assert.Multiple(() =>
        {
            Assert.That(game.WonBy(10), Is.True);
            Assert.That(game.WonBy(20), Is.False);
            Assert.That(game.WonBy(30), Is.False, "A tie is no win");
            Assert.That(game.WonBy(40), Is.False, "No side of it");
        });
    }

    [Test]
    public void TheRules_ForQueries_AgreeWithTheLoadedGame()
    {
        var games = new[] { RecordedGame.Import("archive/1", Facts, null), RecordedGame.EnterByHand(Facts) };
        var manual = RecordedGame.Manual.Compile();
        var imported = RecordedGame.Imported.Compile();

        Assert.Multiple(() =>
        {
            foreach (var game in games)
            {
                Assert.That(manual(game), Is.EqualTo(game.IsManual), game.SourceKey);
                Assert.That(imported(game), Is.EqualTo(!game.IsManual), game.SourceKey);
            }
        });
    }
}

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
