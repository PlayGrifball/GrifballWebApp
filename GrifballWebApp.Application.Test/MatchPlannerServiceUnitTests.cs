using GrifballWebApp.Application.EventOrganizer;
using GrifballWebApp.Application.MatchPlanner;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>MatchPlannerService against the fake IApplicationDbContext: the regular season's schedule (its bracket
/// is BracketService's, covered there).</summary>
[TestFixture]
internal sealed class MatchPlannerServiceUnitTests : ApplicationDbContextTests
{
    private const int SeasonID = 1;
    private MatchPlannerService _service = null!;
    private int _nextUserId = 1;
    private int _nextTeamId = 1;
    private static readonly string[] ExpectedTeamLabels =
    {
        "Gamer Tag's Team", "Discord (discord name)", "Display (Display Name)", "Plain (plain-user)", "No captain",
    };

    [SetUp]
    public void SetUp()
    {
        _service = new MatchPlannerService(Context);
    }

    private async Task SeedSeason()
    {
        Context.Seasons.Add(Season.Seed(seasonName: "S1", seasonID: SeasonID));
        await Context.SaveChangesAsync();
    }

    private async Task<Team> SeedTeam(string teamName, XboxUser? xboxUser = null, DiscordUser? discordUser = null,
        string? displayName = null, string? userName = null)
    {
        var teamID = _nextTeamId++;
        var team = Team.Seed(teamName: teamName, seasonID: SeasonID, teamID: teamID);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();

        if (userName is not null || xboxUser is not null || discordUser is not null || displayName is not null)
        {
            var userID = _nextUserId++;
            if (xboxUser is not null)
                Context.XboxUsers.Add(xboxUser);
            if (discordUser is not null)
                Context.DiscordUsers.Add(discordUser);
            var user = User.Seed(userName: userName ?? $"user{userID}", displayName: displayName, xboxUser: xboxUser, discordUser: discordUser, id: userID);
            Context.Users.Add(user);
            await Context.SaveChangesAsync();
            Context.Add(TeamPlayer.Seed(teamID: teamID, userID: userID));
            await Context.SaveChangesAsync();
            team.SetCaptain(userID);
            await Context.SaveChangesAsync();
        }
        return team;
    }

    private Task<List<SeasonMatch>> LoadMatches() => Context.SeasonMatches.Where(m => m.SeasonID == SeasonID).ToListAsync();

    [Test]
    public async Task CreateSeasonMatches_RoundRobin_CreatesMatchForEveryOrderedPair()
    {
        await SeedSeason();
        var a = await SeedTeam("A");
        await SeedTeam("B");
        await SeedTeam("C");

        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 3);

        var matches = await LoadMatches();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches, Has.Count.EqualTo(6)); // 3 teams, each hosts the other 2 once
            Assert.That(matches.All(m => m.BestOf == 3), Is.True);
            Assert.That(matches.Count(m => m.HomeTeamID == a.TeamID), Is.EqualTo(2));
            Assert.That(matches.Select(m => (m.HomeTeamID, m.AwayTeamID)).Distinct().ToList(), Has.Count.EqualTo(6));
        }
    }

    [Test]
    public async Task CreateSeasonMatches_HomeMatchesPerTeamMultiplier_RepeatsEachPairing()
    {
        await SeedSeason();
        await SeedTeam("A");
        await SeedTeam("B");

        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 3, bestOf: 1);

        var matches = await LoadMatches();
        Assert.That(matches, Has.Count.EqualTo(6)); // 2 teams * 3 home matches each
    }

    [Test]
    public async Task CreateSeasonMatches_Recreate_ReplacesPreviousMatches()
    {
        await SeedSeason();
        await SeedTeam("A");
        await SeedTeam("B");
        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 1);

        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 2, bestOf: 5);

        var matches = await LoadMatches();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches, Has.Count.EqualTo(4));
            Assert.That(matches.All(m => m.BestOf == 5), Is.True);
        }
    }

    [Test]
    public async Task CreateSeasonMatches_AlreadyPlayed_ThrowsConflictAndKeepsExistingMatches()
    {
        await SeedSeason();
        var a = await SeedTeam("A");
        await SeedTeam("B");
        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 1);
        Context.ChangeTracker.Clear();
        var played = await Context.SeasonMatches.SingleAsync(m => m.HomeTeamID == a.TeamID);
        played.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SeasonRefusedException>(
            () => _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo(MatchPlannerService.PlayedMatches));
            Assert.That(ex.Status, Is.EqualTo(409));
            Assert.That(await Context.SeasonMatches.CountAsync(m => m.SeasonID == SeasonID), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task GetUnscheduledMatches_NoMatches_ReturnsEmptyList()
    {
        await SeedSeason();

        var result = await _service.GetUnscheduledMatches(SeasonID);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetUnscheduledMatches_NamesCaptainsEachWayAndOrdersByTeamLabel()
    {
        await SeedSeason();
        await SeedTeam("Gamer Tag's Team", xboxUser: XboxUser.FromXbox(100, "Gamer Tag"));
        await SeedTeam("Discord", discordUser: DiscordUser.FromDiscord(200, "discord name"));
        await SeedTeam("Display", displayName: "Display Name");
        await SeedTeam("Plain", userName: "plain-user");
        await SeedTeam("No captain"); // no user: stays uncaptained
        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 1);

        var result = await _service.GetUnscheduledMatches(SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(20)); // 5 teams, each hosts the other 4
            Assert.That(result.Select(m => m.HomeTeam).Distinct(), Is.EquivalentTo(ExpectedTeamLabels));
            Assert.That(result.Select(m => m.HomeTeam), Is.Ordered, "ordered by home team label");
            Assert.That(result.All(m => m.Complete is false), Is.True);
        }
    }

    [Test]
    public async Task GetUnscheduledMatches_DecidedMatch_IsComplete()
    {
        await SeedSeason();
        var a = await SeedTeam("A");
        await SeedTeam("B");
        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 1);
        Context.ChangeTracker.Clear();
        var match = await Context.SeasonMatches.SingleAsync(m => m.HomeTeamID == a.TeamID);
        match.WonBy(SeasonMatchSide.Home);
        await Context.SaveChangesAsync();

        var result = await _service.GetUnscheduledMatches(SeasonID);

        // Two teams, one home match each: A's (decided) and B's (not)
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result.Single(m => m.SeasonMatchID == match.SeasonMatchID).Complete, Is.True);
            Assert.That(result.Single(m => m.SeasonMatchID != match.SeasonMatchID).Complete, Is.False);
        }
    }

    [Test]
    public async Task GetScheduledMatches_NoScheduledMatches_ReturnsEmptyList()
    {
        await SeedSeason();
        await SeedTeam("A");
        await SeedTeam("B");
        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 1);

        var result = await _service.GetScheduledMatches(SeasonID);

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task GetScheduledMatches_OrdersByScheduledTimeAndExcludesUnscheduled()
    {
        await SeedSeason();
        var a = await SeedTeam("A");
        var b = await SeedTeam("B");
        await SeedTeam("C");
        await _service.CreateSeasonMatches(SeasonID, homeMatchesPerTeam: 1, bestOf: 1);
        var matches = await LoadMatches();
        var later = matches.First(m => m.HomeTeamID == a.TeamID);
        var earlier = matches.First(m => m.HomeTeamID == b.TeamID && m.AwayTeamID != a.TeamID);
        await _service.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = later.SeasonMatchID, Time = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) });
        await _service.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = earlier.SeasonMatchID, Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        var result = await _service.GetScheduledMatches(SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].SeasonMatchID, Is.EqualTo(earlier.SeasonMatchID));
            Assert.That(result[0].Time, Is.EqualTo(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(result[1].SeasonMatchID, Is.EqualTo(later.SeasonMatchID));
        }
    }

    [Test]
    public async Task UpdateMatchTime_ExistingMatch_UpdatesScheduledTime()
    {
        await SeedSeason();
        var a = await SeedTeam("A");
        var b = await SeedTeam("B");
        var match = SeasonMatch.Between(SeasonID, a.TeamID, b.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var newTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        await _service.UpdateMatchTime(new UpdateMatchTimeDto { SeasonMatchID = match.SeasonMatchID, Time = newTime });

        Context.ChangeTracker.Clear();
        var updated = await Context.SeasonMatches.SingleAsync(m => m.SeasonMatchID == match.SeasonMatchID);
        Assert.That(updated.ScheduledTime, Is.EqualTo(newTime));
    }

    [Test]
    public async Task UpdateMatchTime_MatchNotFound_DoesNothing()
    {
        var dto = new UpdateMatchTimeDto { SeasonMatchID = 999, Time = DateTime.UtcNow };

        await Assert.DoesNotThrowAsync(() => _service.UpdateMatchTime(dto));
    }
}
