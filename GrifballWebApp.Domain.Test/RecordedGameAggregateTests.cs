using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>A recorded game begins through its factories, and its sides and players' lines change only through its methods.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RecordedGameAggregateTests
{
    private static readonly DateTime At = new(2024, 3, 1, 20, 0, 0, DateTimeKind.Utc);
    private static readonly RecordedGameFacts Facts = new(1, "Pit", "Grifball", At, IsPlayoffs: true);
    private static readonly int[] expected = new[] { 200, 300 };

    private static RecordedSideFacts Side(int side, int? teamID, Outcomes outcome, int? score = null, string? name = null) => new(side, teamID, name, outcome, score);

    private static RecordedPlayerLine Line(int userID, int? side, string gamertag = "Player", int kills = 1) => new(userID, side, gamertag, kills, 2, 3);

    [Test]
    public void AnImportedGame_HasItsFacts_InItsSeason()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((game.SourceKey, game.Origin, game.SeasonID), Is.EqualTo(("archive/1", RecordedGameOrigin.HistoricalImport, (int?)7)));
            Assert.That((game.GameVersionID, game.Map, game.Gametype, game.PlayedAt, game.IsPlayoffs), Is.EqualTo((1, "Pit", "Grifball", (DateTime?)At, true)));
            Assert.That(game.IsManual, Is.False);
        }
    }

    [Test]
    public void AGameEnteredByHand_IsManual_WithAKeyOfItsOwn_AndNoSeason()
    {
        var game = RecordedGame.EnterByHand(Facts);
        var other = RecordedGame.EnterByHand(Facts);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.IsManual, Is.True);
            Assert.That(game.Origin, Is.EqualTo(RecordedGameOrigin.Manual));
            Assert.That(game.SeasonID, Is.Null);
            Assert.That(game.SourceKey, Does.StartWith(RecordedGame.ManualSourceKeyPrefix));
            Assert.That(game.SourceKey, Is.Not.EqualTo(other.SourceKey));
        }
    }

    [Test]
    public void AGame_IsRefused_WithoutItsFacts()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => RecordedGame.Import(" ", Facts, null), Throws.ArgumentException, "A key");
            Assert.That(() => RecordedGame.Import(new string('k', RecordedGame.SourceKeyMaxLength + 1), Facts, null), Throws.ArgumentException, "A key a row holds");
            Assert.That(() => RecordedGame.Import("archive/1", null!, null), Throws.ArgumentNullException);
            Assert.That(() => RecordedGame.Import("archive/1", Facts with { GameVersionID = 0 }, null), Throws.ArgumentException, "A game version");
            Assert.That(() => RecordedGame.EnterByHand(Facts with { Map = new string('m', RecordedGame.MapMaxLength + 1) }), Throws.ArgumentException, "A map a row holds");
            Assert.That(() => RecordedGame.EnterByHand(Facts with { Gametype = new string('g', RecordedGame.GametypeMaxLength + 1) }), Throws.ArgumentException, "A gametype a row holds");
        }
    }

    [Test]
    public void TheImport_ChangesOnlyItsOwnGames_AndTheMatchPage_OnlyGamesEnteredByHand()
    {
        var imported = RecordedGame.Import("archive/1", Facts, 7);
        var manual = RecordedGame.EnterByHand(Facts);

        imported.Reimport(Facts with { Map = "Lockout", IsPlayoffs = false }, null);
        manual.EditByHand(Facts with { Gametype = "Hammerball" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That((imported.Map, imported.IsPlayoffs, imported.SeasonID), Is.EqualTo(("Lockout", false, (int?)null)));
            Assert.That(manual.Gametype, Is.EqualTo("Hammerball"));
            Assert.That(() => manual.Reimport(Facts, 7), Throws.InvalidOperationException, "The import never touches a game entered by hand");
            Assert.That(() => imported.EditByHand(Facts), Throws.InvalidOperationException, "The match page edits only a game entered by hand");
            Assert.That(manual.SeasonID, Is.Null, "Still in no season");
        }
    }

    [Test]
    public void ItsSides_AreFoundByNumber_KeptChangedAddedAndRemoved()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetSides([Side(1, 10, Outcomes.Won, 6, "Red"), Side(2, 20, Outcomes.Lost, 2), Side(3, null, Outcomes.Lost)]);
        var first = game.Teams.Single(t => t.Side == 1);

        game.SetSides([Side(1, 11, Outcomes.Lost, 3, "Red Two"), Side(2, 20, Outcomes.Won, 5), Side(4, null, Outcomes.Tie)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.Teams.OrderBy(t => t.Side).Select(t => (t.Side, t.TeamID, t.Name, t.Outcome, t.Score)), Is.EqualTo(new[]
            {
                (1, (int?)11, (string?)"Red Two", Outcomes.Lost, (int?)3),
                (2, (int?)20, (string?)null, Outcomes.Won, (int?)5),
                (4, (int?)null, (string?)null, Outcomes.Tie, (int?)null),
            }));
            Assert.That(game.Teams, Does.Contain(first), "Side 1 is changed in place, not replaced");
        }
    }

    [Test]
    public void ItsSides_AreRefused_TwiceOrWithAnUnknownResult()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => game.SetSides([Side(1, 10, Outcomes.Won), Side(1, 20, Outcomes.Lost)]), Throws.ArgumentException, "A side listed twice");
            Assert.That(() => game.SetSides([Side(1, 10, (Outcomes)0)]), Throws.ArgumentException, "A result a game knows");
            Assert.That(game.Teams, Is.Empty, "Nothing is changed by a refused call");
        }
    }

    [Test]
    public void ItsPlayers_AreFoundByUser_OnTheirSideByNumber()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetSides([Side(1, 10, Outcomes.Won), Side(2, 20, Outcomes.Lost)]);
        game.SetPlayers([Line(100, 1, "Alpha"), Line(200, 2, "Bravo"), Line(300, 2, "Charlie")]);
        var alpha = game.Participants.Single(p => p.UserID == 100);

        game.SetPlayers([Line(100, 2, "Alpha Two", kills: 9) with { Goals = 4, Punches = 1 }, Line(200, 5, "Bravo"), Line(400, null, "Delta")]);

        using (Assert.EnterMultipleScope())
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
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.Participants.Select(p => (p.UserID, p.Gamertag, p.Kills)), Is.EqualTo(new[] { (100, "Alpha", 7) }),
                "One line for the player listed, none for the one gone: the stats count each once");
            Assert.That(game.Participants, Does.Contain(first), "The oldest line is the one kept, changed in place");
        }
    }

    [Test]
    public void AMerge_IntoAPlayerOfTheGame_KeepsTheirLine_AndTheSourcesGoes()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetPlayers([Line(100, null, "Old Tag", kills: 3), Line(300, null, "Real", kills: 5), Line(200, null)]);
        var real = game.Participants.Single(p => p.UserID == 300);

        var moved = game.ReassignPlayer(100, 300);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved, Is.Zero, "Nothing moved: the source's line went");
            Assert.That(game.Participants.Select(p => p.UserID).Order(), Is.EqualTo(expected));
            Assert.That(game.Participants, Does.Contain(real), "The target's line stays");
            Assert.That(real.Kills, Is.EqualTo(5));
            Assert.That(game.ReassignPlayer(300, 300), Is.Zero, "Into itself: nothing to do");
            Assert.That(game.Participants, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void AMerge_OfAUserWithTwoLines_MovesOneAndTheOtherGoes()
    {
        var game = RecordedGame.Seed("archive/1");
        game.SeedPlayer(Line(100, null, "Old Tag", kills: 3)).RecordedGameParticipantID = 1;
        game.SeedPlayer(Line(100, null, "Old Tag", kills: 4)).RecordedGameParticipantID = 2;

        var moved = game.ReassignPlayer(100, 300);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved, Is.EqualTo(1));
            Assert.That(game.Participants.Select(p => (p.UserID, p.Kills)), Is.EqualTo(new[] { (300, 3) }), "The oldest moves");
        }
    }

    [Test]
    public void AMerge_MovesOnlyThatPlayersLines()
    {
        var game = RecordedGame.Import("archive/1", Facts, 7);
        game.SetPlayers([Line(100, null), Line(200, null)]);

        var moved = game.ReassignPlayer(100, 300);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(moved, Is.EqualTo(1));
            Assert.That(game.Participants.Select(p => p.UserID).Order(), Is.EqualTo(expected));
            Assert.That(game.ReassignPlayer(999, 300), Is.Zero, "Nobody's lines to move");
        }
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

        using (Assert.EnterMultipleScope())
        {
            Assert.That(game.WonBy(10), Is.True);
            Assert.That(game.WonBy(20), Is.False);
            Assert.That(game.WonBy(30), Is.False, "A tie is no win");
            Assert.That(game.WonBy(40), Is.False, "No side of it");
        }
    }

    [Test]
    public void TheRules_ForQueries_AgreeWithTheLoadedGame()
    {
        var games = new[] { RecordedGame.Import("archive/1", Facts, null), RecordedGame.EnterByHand(Facts) };
        var manual = RecordedGame.Manual.Compile();
        var imported = RecordedGame.Imported.Compile();

        using (Assert.EnterMultipleScope())
        {
            foreach (var game in games)
            {
                Assert.That(manual(game), Is.EqualTo(game.IsManual), game.SourceKey);
                Assert.That(imported(game), Is.EqualTo(!game.IsManual), game.SourceKey);
            }
        }
    }
}
