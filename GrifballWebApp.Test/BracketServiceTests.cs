using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Dtos;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class BracketServiceTests
{
    private GrifballContext _context;
    private TeamStandingsService _teamStandingsService;
    private BracketService _service;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _teamStandingsService = new TeamStandingsService(_context);
        _service = new BracketService(_context, _teamStandingsService);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task SetCustomSeeds_Should_SetTeamIDsCorrectly_When_ValidCustomSeedsProvided()
    {
        // Arrange
        const int seasonID = 1;
        var team1 = new SeasonTeam { TeamID = 1, SeasonID = seasonID, TeamName = "Team1" };
        var team2 = new SeasonTeam { TeamID = 2, SeasonID = seasonID, TeamName = "Team2" };
        var team3 = new SeasonTeam { TeamID = 3, SeasonID = seasonID, TeamName = "Team3" };
        var team4 = new SeasonTeam { TeamID = 4, SeasonID = seasonID, TeamName = "Team4" };

        await _context.SeasonTeams.AddRangeAsync([team1, team2, team3, team4]);

        var bracket1 = new MatchBracketInfo
        {
            MatchNumber = 1,
            RoundNumber = 1,
            HomeTeamSeedNumber = 1,
            AwayTeamSeedNumber = 4
        };

        var bracket2 = new MatchBracketInfo
        {
            MatchNumber = 2,
            RoundNumber = 1,
            HomeTeamSeedNumber = 2,
            AwayTeamSeedNumber = 3
        };

        var match1 = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = bracket1,
            HomeTeamID = null,
            AwayTeamID = null
        };

        var match2 = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = bracket2,
            HomeTeamID = null,
            AwayTeamID = null
        };

        await _context.SeasonMatches.AddRangeAsync([match1, match2]);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 2, Seed = 1 }, // Team2 becomes seed 1
            new() { TeamID = 1, Seed = 2 }, // Team1 becomes seed 2  
            new() { TeamID = 4, Seed = 3 }, // Team4 becomes seed 3
            new() { TeamID = 3, Seed = 4 }  // Team3 becomes seed 4
        };

        // Act
        await _service.SetCustomSeeds(seasonID, customSeeds);

        // Assert
        var updatedMatches = await _context.SeasonMatches
            .Include(sm => sm.BracketMatch)
            .Where(x => x.SeasonID == seasonID)
            .OrderBy(x => x.BracketMatch.MatchNumber)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            // Match 1: seed 1 vs seed 4 should now be Team2 vs Team3
            Assert.That(updatedMatches[0].HomeTeamID, Is.EqualTo(2), "Match 1 home team should be Team2 (custom seed 1)");
            Assert.That(updatedMatches[0].AwayTeamID, Is.EqualTo(3), "Match 1 away team should be Team3 (custom seed 4)");

            // Match 2: seed 2 vs seed 3 should now be Team1 vs Team4
            Assert.That(updatedMatches[1].HomeTeamID, Is.EqualTo(1), "Match 2 home team should be Team1 (custom seed 2)");
            Assert.That(updatedMatches[1].AwayTeamID, Is.EqualTo(4), "Match 2 away team should be Team4 (custom seed 3)");
        });
    }

    [Test]
    public async Task SetCustomSeeds_Should_ThrowException_When_TeamsAlreadySeeded()
    {
        // Arrange
        const int seasonID = 1;
        var bracket = new MatchBracketInfo
        {
            MatchNumber = 1,
            RoundNumber = 1,
            HomeTeamSeedNumber = 1,
            AwayTeamSeedNumber = 2
        };

        var match = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = bracket,
            HomeTeamID = 1, // Already seeded
            AwayTeamID = 2  // Already seeded
        };

        await _context.SeasonMatches.AddAsync(match);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 },
            new() { TeamID = 2, Seed = 2 }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _service.SetCustomSeeds(seasonID, customSeeds));
        
        Assert.That(exception.Message, Is.EqualTo("Teams have already been seeded"));
    }

    [Test]
    public async Task SetCustomSeeds_Should_ThrowException_When_MissingSeedNumber()
    {
        // Arrange
        const int seasonID = 1;
        var bracket = new MatchBracketInfo
        {
            MatchNumber = 1,
            RoundNumber = 1,
            HomeTeamSeedNumber = 1,
            AwayTeamSeedNumber = 3 // Seed 2 is missing from custom seeds
        };

        var match = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = bracket,
            HomeTeamID = null,
            AwayTeamID = null
        };

        await _context.SeasonMatches.AddAsync(match);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 },
            // Missing seed 3
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _service.SetCustomSeeds(seasonID, customSeeds));
        
        Assert.That(exception.Message, Is.EqualTo("Missing home or away team. Byes are currently not supported"));
    }

    [Test]
    public async Task SetCustomSeeds_Should_ThrowException_When_HomeTeamSeedNumberIsNull()
    {
        // Arrange
        const int seasonID = 1;
        var bracket = new MatchBracketInfo
        {
            MatchNumber = 1,
            RoundNumber = 1,
            HomeTeamSeedNumber = null, // Missing home team seed number
            AwayTeamSeedNumber = 2
        };

        var match = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = bracket,
            HomeTeamID = null,
            AwayTeamID = null
        };

        await _context.SeasonMatches.AddAsync(match);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 },
            new() { TeamID = 2, Seed = 2 }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _service.SetCustomSeeds(seasonID, customSeeds));
        
        Assert.That(exception.Message, Is.EqualTo("Missing home team seed number"));
    }

    [Test]
    public async Task SetCustomSeeds_Should_ThrowException_When_AwayTeamSeedNumberIsNull()
    {
        // Arrange
        const int seasonID = 1;
        var bracket = new MatchBracketInfo
        {
            MatchNumber = 1,
            RoundNumber = 1,
            HomeTeamSeedNumber = 1,
            AwayTeamSeedNumber = null // Missing away team seed number
        };

        var match = new SeasonMatch
        {
            SeasonID = seasonID,
            BracketMatch = bracket,
            HomeTeamID = null,
            AwayTeamID = null
        };

        await _context.SeasonMatches.AddAsync(match);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 },
            new() { TeamID = 2, Seed = 2 }
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<Exception>(
            () => _service.SetCustomSeeds(seasonID, customSeeds));
        
        Assert.That(exception.Message, Is.EqualTo("Missing away team seed number"));
    }

    [Test]
    public async Task SetCustomSeeds_Should_HandleMultipleRounds_When_BracketHasMultipleRounds()
    {
        // Arrange
        const int seasonID = 1;
        var team1 = new SeasonTeam { TeamID = 1, SeasonID = seasonID, TeamName = "Team1" };
        var team2 = new SeasonTeam { TeamID = 2, SeasonID = seasonID, TeamName = "Team2" };
        var team3 = new SeasonTeam { TeamID = 3, SeasonID = seasonID, TeamName = "Team3" };
        var team4 = new SeasonTeam { TeamID = 4, SeasonID = seasonID, TeamName = "Team4" };

        await _context.SeasonTeams.AddRangeAsync([team1, team2, team3, team4]);

        // Round 1 matches
        var bracket1 = new MatchBracketInfo { MatchNumber = 1, RoundNumber = 1, HomeTeamSeedNumber = 1, AwayTeamSeedNumber = 4 };
        var bracket2 = new MatchBracketInfo { MatchNumber = 2, RoundNumber = 1, HomeTeamSeedNumber = 2, AwayTeamSeedNumber = 3 };
        
        // Round 2 match (final) - no seed numbers, should be ignored
        var bracket3 = new MatchBracketInfo { MatchNumber = 3, RoundNumber = 2, HomeTeamSeedNumber = null, AwayTeamSeedNumber = null };

        var match1 = new SeasonMatch { SeasonID = seasonID, BracketMatch = bracket1, HomeTeamID = null, AwayTeamID = null };
        var match2 = new SeasonMatch { SeasonID = seasonID, BracketMatch = bracket2, HomeTeamID = null, AwayTeamID = null };
        var match3 = new SeasonMatch { SeasonID = seasonID, BracketMatch = bracket3, HomeTeamID = null, AwayTeamID = null };

        await _context.SeasonMatches.AddRangeAsync([match1, match2, match3]);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 3, Seed = 1 },
            new() { TeamID = 4, Seed = 2 },
            new() { TeamID = 1, Seed = 3 },
            new() { TeamID = 2, Seed = 4 }
        };

        // Act
        await _service.SetCustomSeeds(seasonID, customSeeds);

        // Assert
        var round1Matches = await _context.SeasonMatches
            .Include(sm => sm.BracketMatch)
            .Where(x => x.SeasonID == seasonID && x.BracketMatch.RoundNumber == 1)
            .OrderBy(x => x.BracketMatch.MatchNumber)
            .ToListAsync();

        var round2Matches = await _context.SeasonMatches
            .Include(sm => sm.BracketMatch)
            .Where(x => x.SeasonID == seasonID && x.BracketMatch.RoundNumber == 2)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            // Only round 1 matches should be seeded
            Assert.That(round1Matches[0].HomeTeamID, Is.EqualTo(3), "Round 1 Match 1 home team should be seeded");
            Assert.That(round1Matches[0].AwayTeamID, Is.EqualTo(2), "Round 1 Match 1 away team should be seeded");
            Assert.That(round1Matches[1].HomeTeamID, Is.EqualTo(4), "Round 1 Match 2 home team should be seeded");
            Assert.That(round1Matches[1].AwayTeamID, Is.EqualTo(1), "Round 1 Match 2 away team should be seeded");

            // Round 2 matches should remain unchanged
            Assert.That(round2Matches[0].HomeTeamID, Is.Null, "Round 2 match home team should remain null");
            Assert.That(round2Matches[0].AwayTeamID, Is.Null, "Round 2 match away team should remain null");
        });
    }

    [Test]
    public async Task SetCustomSeeds_Should_IgnoreMatchesWithoutSeedNumbers_When_BracketHasMixedMatches()
    {
        // Arrange
        const int seasonID = 1;
        var bracket1 = new MatchBracketInfo
        {
            MatchNumber = 1,
            RoundNumber = 1,
            HomeTeamSeedNumber = 1,
            AwayTeamSeedNumber = 2
        };

        var bracket2 = new MatchBracketInfo
        {
            MatchNumber = 2,
            RoundNumber = 1,
            HomeTeamSeedNumber = null,
            AwayTeamSeedNumber = null
        };

        var match1 = new SeasonMatch { SeasonID = seasonID, BracketMatch = bracket1, HomeTeamID = null, AwayTeamID = null };
        var match2 = new SeasonMatch { SeasonID = seasonID, BracketMatch = bracket2, HomeTeamID = null, AwayTeamID = null };

        await _context.SeasonMatches.AddRangeAsync([match1, match2]);
        await _context.SaveChangesAsync();

        var customSeeds = new CustomSeedDto[]
        {
            new() { TeamID = 1, Seed = 1 },
            new() { TeamID = 2, Seed = 2 }
        };

        // Act
        await _service.SetCustomSeeds(seasonID, customSeeds);

        // Assert
        var allMatches = await _context.SeasonMatches
            .Include(sm => sm.BracketMatch)
            .Where(x => x.SeasonID == seasonID)
            .OrderBy(x => x.BracketMatch.MatchNumber)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            // Match with seed numbers should be updated
            Assert.That(allMatches[0].HomeTeamID, Is.EqualTo(1), "Match with seed numbers should have home team set");
            Assert.That(allMatches[0].AwayTeamID, Is.EqualTo(2), "Match with seed numbers should have away team set");

            // Match without seed numbers should remain unchanged
            Assert.That(allMatches[1].HomeTeamID, Is.Null, "Match without seed numbers should have null home team");
            Assert.That(allMatches[1].AwayTeamID, Is.Null, "Match without seed numbers should have null away team");
        });
    }
}