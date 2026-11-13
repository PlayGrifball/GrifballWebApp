using System.Text.Json;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.Notifications;
using GrifballWebApp.Application.Outbox;
using GrifballWebApp.Application.Teams;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Infrastructure.Outbox;
using GrifballWebApp.Infrastructure.Teams;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// DraftOutbox.EnqueueOnDeckAsync: who is on deck and the player pool's pick menus, or that no team was found, or the
/// draft is done, read back from a real (temp-file SQLite) GrifballContext's outbox row. TeamService's own rules
/// (fewest players, then lowest captain order) stay GrifballWebApp.Integration.Test's; this only covers what the
/// on-deck message renders from what TeamService gives it.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DraftOutboxOnDeckUnitTests : SqliteContextFixture
{
    private TeamService _teams = null!;
    private IOutbox _outbox = null!;

    [SetUp]
    public void Setup()
    {
        _teams = new TeamService((IApplicationDbContext)Context, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), Substitute.For<IDatabaseLocks>());
        _outbox = new ContextOutbox(Context);
    }

    private async Task<Season> AddSeason()
    {
        var season = Season.Seed(seasonName: "Test Season");
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season;
    }

    private async Task<User> AddUser(string displayName)
    {
        var user = User.Seed(userName: displayName, displayName: displayName);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> AddCaptainedTeam(Season season, User captain, string teamName = "Team", int captainOrder = 1)
    {
        var team = Team.Drafted(season.SeasonID, teamName, captain.Id, captainOrder);
        Context.Teams.Add(team);
        await Context.SaveChangesAsync();
        Assert.That(team.SetCaptain(captain.Id), Is.EqualTo(TeamCaptainResult.Appointed));
        await Context.SaveChangesAsync();
        return team;
    }

    private async Task AddSignup(Season season, User user)
    {
        Context.Add(SeasonSignup.Seed(season.SeasonID, user.Id));
        await Context.SaveChangesAsync();
    }

    private async Task<DraftOnDeckPayload> EnqueueAndRead(int seasonId)
    {
        await _outbox.EnqueueOnDeckAsync(Context, _teams, seasonId, CancellationToken.None);
        await Context.SaveChangesAsync();
        var message = await Context.Outbox.AsNoTracking().SingleAsync(m => m.Kind == DraftOutbox.OnDeck);
        return JsonSerializer.Deserialize<DraftOnDeckPayload>(message.Payload, OutboxJson.Options)!;
    }

    [Test]
    public async Task NoTeamInTheSeason_SaysFailedToFindTeam()
    {
        var season = await AddSeason();

        var payload = await EnqueueAndRead(season.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(payload.SeasonId, Is.EqualTo(season.SeasonID));
            Assert.That(payload.Messages.Select(m => (m.Content, m.Menus.Count)), Is.EqualTo(new[] { ("Failed to find team", 0) }));
        }
    }

    [Test]
    public async Task AnEmptyPlayerPool_SaysTheDraftIsComplete()
    {
        var season = await AddSeason();
        var captain = await AddUser("Cappy");
        await AddCaptainedTeam(season, captain);

        var payload = await EnqueueAndRead(season.SeasonID);

        Assert.That(payload.Messages.Select(m => (m.Content, m.Menus.Count)), Is.EqualTo(new[] { ("Draft has been completed", 0) }));
    }

    [Test]
    public async Task APoolOfPlayers_IsOneMenu_NamedForWhoIsOnDeck()
    {
        var season = await AddSeason();
        var captain = await AddUser("Cappy");
        await AddCaptainedTeam(season, captain);
        var amy = await AddUser("Amy");
        var bob = await AddUser("Bob");
        await AddSignup(season, amy);
        await AddSignup(season, bob);

        var payload = await EnqueueAndRead(season.SeasonID);

        var message = payload.Messages.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(message.Content, Is.EqualTo("Cappy is on deck"));
            var menu = message.Menus.Single();
            Assert.That(menu.Placeholder, Is.EqualTo("Make pick - Amy - Bob"));
            Assert.That(menu.Options.Select(o => (o.Label, o.Value)), Is.EqualTo(new[] { ("Amy", amy.Id.ToString()), ("Bob", bob.Id.ToString()) }));
        }
    }

    private static readonly string[] expectedPool = ["Pooled"];

    [Test]
    public async Task ARosteredSignup_IsNotInThePool()
    {
        var season = await AddSeason();
        var captain = await AddUser("Cappy");
        var team = await AddCaptainedTeam(season, captain);
        var already = await AddUser("Already");
        team.SeedPlayer(already.Id, draftRound: 1);
        await Context.SaveChangesAsync();
        await AddSignup(season, already);
        var stillPooled = await AddUser("Pooled");
        await AddSignup(season, stillPooled);

        var payload = await EnqueueAndRead(season.SeasonID);

        var options = payload.Messages.Single().Menus.Single().Options;
        Assert.That(options.Select(o => o.Label), Is.EqualTo(expectedPool), "Already on a roster: left out of the pool");
    }
}
