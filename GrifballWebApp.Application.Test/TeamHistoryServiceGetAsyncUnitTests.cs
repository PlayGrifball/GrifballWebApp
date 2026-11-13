using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>TeamHistoryService.GetAsync against the fake IApplicationDbContext. Franchises.IsPlaceholderName's own
/// cases stay in TeamHistoryServiceUnitTests, which has no database.</summary>
[TestFixture]
internal sealed class TeamHistoryServiceGetAsyncUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private TeamHistoryService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new TeamHistoryService(Context, _time);
    }

    [Test]
    public async Task GetAsync_NoSuchTeam_ReturnsNull()
    {
        var history = await _service.GetAsync(999);

        Assert.That(history, Is.Null);
    }

    [Test]
    public async Task GetAsync_SameNameAcrossSeasons_AggregatesAsOneFranchise()
    {
        var older = Season.Seed(seasonName: "Season One", seasonID: 1);
        older.Schedule(Now.AddYears(-2).AddDays(-30), Now.AddYears(-2).AddDays(-20), Now.AddYears(-2).AddDays(-10),
            Now.AddYears(-2), Now.AddYears(-2).AddMonths(5));
        var newer = Season.Seed(seasonName: "Season Two", seasonID: 2);
        newer.Schedule(Now.AddYears(-1).AddDays(-30), Now.AddYears(-1).AddDays(-20), Now.AddYears(-1).AddDays(-10),
            Now.AddYears(-1), Now.AddYears(-1).AddMonths(5));
        Context.Seasons.AddRange(older, newer);

        var oldTeam = Team.Seed(teamName: "Reclaimers", seasonID: 1, teamID: 10);
        var newTeam = Team.Seed(teamName: "Reclaimers", seasonID: 2, teamID: 20);
        Context.Teams.AddRange(oldTeam, newTeam);
        Context.Users.Add(User.Seed(userName: "captain", displayName: "Captain", id: 1));
        await Context.SaveChangesAsync();

        Context.Add(TeamPlayer.Seed(teamID: 20, userID: 1, draftCaptainOrder: 1));
        await Context.SaveChangesAsync();
        newTeam.SetCaptain(1);
        await Context.SaveChangesAsync();

        var history = await _service.GetAsync(20);

        Assert.That(history, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(history!.TeamName, Is.EqualTo("Reclaimers"));
            Assert.That(history.Appearances, Is.EqualTo(2));
            Assert.That(history.Seasons, Has.Length.EqualTo(2));
            // Newest season first
            Assert.That(history.Seasons[0].TeamID, Is.EqualTo(20));
            Assert.That(history.Seasons[1].TeamID, Is.EqualTo(10));
            Assert.That(history.Players, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task GetAsync_PlaceholderName_IsItsOwnSeasonAlone()
    {
        var season = Season.Seed(seasonName: "Imported", seasonID: 1);
        Context.Seasons.Add(season);
        // "Team 1" is a generic archive placeholder name (Franchises.IsPlaceholderName): never grouped with
        // another season's "Team 1", even in the same league.
        var team = Team.Seed(teamName: "Team 1", seasonID: 1, teamID: 10);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();

        var history = await _service.GetAsync(10);

        Assert.That(history!.Appearances, Is.EqualTo(1));
    }
}
