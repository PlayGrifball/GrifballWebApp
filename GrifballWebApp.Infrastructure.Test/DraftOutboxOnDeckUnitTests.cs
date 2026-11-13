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
public class DraftOutboxOnDeckUnitTests
{
    private string _path = null!;
    private GrifballContext _context = null!;
    private TeamService _teams = null!;
    private IOutbox _outbox = null!;

    [SetUp]
    public async Task Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"grif-draft-outbox-{Guid.NewGuid():N}.db");
        _context = new GrifballContext(new DbContextOptionsBuilder<GrifballContext>()
            .UseGrifballDatabase(DatabaseProvider.Sqlite, $"Data Source={_path};Pooling=False").Options);
        await _context.Database.EnsureCreatedAsync();
        _teams = new TeamService((IApplicationDbContext)_context, Substitute.For<IPublisher>(), Substitute.For<IDraftChannel>(), Substitute.For<IDatabaseLocks>());
        _outbox = new ContextOutbox(_context);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DisposeAsync();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file))
                File.Delete(file);
    }

    private async Task<Season> AddSeason()
    {
        var season = Season.Seed(seasonName: "Test Season");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        return season;
    }

    private async Task<User> AddUser(string displayName)
    {
        var user = User.Seed(userName: displayName, displayName: displayName);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Team> AddCaptainedTeam(Season season, User captain, string teamName = "Team", int captainOrder = 1)
    {
        var team = Team.Drafted(season.SeasonID, teamName, captain.Id, captainOrder);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        Assert.That(team.SetCaptain(captain.Id), Is.EqualTo(TeamCaptainResult.Appointed));
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task AddSignup(Season season, User user)
    {
        _context.Add(SeasonSignup.Seed(season.SeasonID, user.Id));
        await _context.SaveChangesAsync();
    }

    private async Task<DraftOnDeckPayload> EnqueueAndRead(int seasonId)
    {
        await _outbox.EnqueueOnDeckAsync(_context, _teams, seasonId, CancellationToken.None);
        await _context.SaveChangesAsync();
        var message = await _context.Outbox.AsNoTracking().SingleAsync(m => m.Kind == DraftOutbox.OnDeck);
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
        await _context.SaveChangesAsync();
        await AddSignup(season, already);
        var stillPooled = await AddUser("Pooled");
        await AddSignup(season, stillPooled);

        var payload = await EnqueueAndRead(season.SeasonID);

        var options = payload.Messages.Single().Menus.Single().Options;
        Assert.That(options.Select(o => o.Label), Is.EqualTo(expectedPool), "Already on a roster: left out of the pool");
    }
}
