using GrifballWebApp.Application;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Server.Commands;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// SeasonCommandsHandler's unit tests: the season lookup (<see cref="CommandLookups.SeasonFor"/>) and name
/// (<see cref="SeasonService.GetSeasonName"/>) every action goes through first are real, on a temp-file SQLite
/// GrifballContext (#402). <see cref="Handler"/> leaves the season page services null, for the lookup guard;
/// <see cref="DeepHandler"/> makes them real too, for the actions themselves. <see cref="SeasonCommandsHandler.Draft"/>
/// needs nothing past that, so it is covered fully by <see cref="Handler"/> alone.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private SeasonCommandsHandler Handler()
    {
        var seasons = new SeasonService(Db, null!);
        return new SeasonCommandsHandler(new CommandLookups(Db, seasons), seasons, null!, null!, null!, null!, null!, Urls());
    }

    /// <summary>The deeper services real too (all IApplicationDbContext-backed): every action but Bracket (IBracketService,
    /// substituted interfaces aside, stays GrifballWebApp.Integration.Test's).</summary>
    private SeasonCommandsHandler DeepHandler()
    {
        var seasons = new SeasonService(Db, new GrifballWebApp.Application.Stats.SeasonStatsService(Db));
        return new SeasonCommandsHandler(new CommandLookups(Db, seasons), seasons,
            new GrifballWebApp.Application.TeamStandings.TeamStandingsService(Db),
            new GrifballWebApp.Application.MatchPlanner.MatchPlannerService(Db),
            new GrifballWebApp.Application.Grades.GradesService(Db),
            new GrifballWebApp.Application.Signups.SignupsService(Db),
            null!, Urls());
    }

    [TestCase(nameof(SeasonCommandsHandler.Info))]
    [TestCase(nameof(SeasonCommandsHandler.Standings))]
    [TestCase(nameof(SeasonCommandsHandler.Schedule))]
    [TestCase(nameof(SeasonCommandsHandler.Grades))]
    [TestCase(nameof(SeasonCommandsHandler.Signups))]
    [TestCase(nameof(SeasonCommandsHandler.Bracket))]
    [TestCase(nameof(SeasonCommandsHandler.Draft))]
    public async Task Actions_ReplyNoSuchSeason_WhenTheNamedSeasonIsNotFound(string method)
    {
        var discord = new CommandContextRecorder();
        var handler = Handler();

        Task task = method switch
        {
            nameof(SeasonCommandsHandler.Info) => handler.Info(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Standings) => handler.Standings(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Schedule) => handler.Schedule(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Grades) => handler.Grades(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Signups) => handler.Signups(discord.Context, "nonsense"),
            nameof(SeasonCommandsHandler.Bracket) => handler.Bracket(discord.Context, "nonsense"),
            _ => handler.Draft(discord.Context, "nonsense"),
        };
        await task;

        Assert.That(discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task Stats_RepliesNoSuchSeason_WhenTheNamedSeasonIsNotFound()
    {
        var discord = new CommandContextRecorder();

        await Handler().Stats(discord.Context, "nonsense", SeasonStat.Goals);

        Assert.That(discord.Content(), Is.EqualTo(SeasonCommandsHandler.NoSuchSeason));
    }

    [Test]
    public async Task Draft_LinksToTheLiveDraftBoard_WhenTheSeasonIsFound()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await Handler().Draft(discord.Context, "Winter 2026");

        var embed = discord.Embed();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(embed.Title, Is.EqualTo("Winter 2026 draft"));
            Assert.That(embed.Description, Is.EqualTo("The draft board is live on the site."));
        }
    }

    [Test]
    public async Task Info_ShowsTheSeasonSummary_ForAFoundSeason()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await DeepHandler().Info(discord.Context, "Winter 2026");

        Assert.That(discord.Embed().Title, Is.EqualTo("Winter 2026"));
    }

    [Test]
    public async Task Standings_RepliesNoTeamsYet_ForAFoundSeasonWithNoTeams()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await DeepHandler().Standings(discord.Context, "Winter 2026");

        Assert.That(discord.Embed().Description, Does.Contain("No teams yet."));
    }

    [Test]
    public async Task Schedule_RepliesNoUpcomingMatches_ForAFoundSeasonWithNoMatches()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await DeepHandler().Schedule(discord.Context, "Winter 2026");

        Assert.That(discord.Embed().Description, Does.Contain("No upcoming matches are scheduled."));
    }

    [Test]
    public async Task Grades_RepliesNoGradesYet_ForAFoundSeasonWithNoGrades()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await DeepHandler().Grades(discord.Context, "Winter 2026");

        Assert.That(discord.Embed().Description, Does.Contain("No grades yet."));
    }

    [Test]
    public async Task Signups_RepliesNobodyHasSignedUpYet_ForAFoundSeasonWithNoSignups()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await DeepHandler().Signups(discord.Context, "Winter 2026");

        Assert.That(discord.Embed().Description, Does.Contain("Nobody has signed up yet."));
    }

    [Test]
    public async Task Stats_RepliesNoStatsYet_ForAFoundSeasonWithNoStats()
    {
        await AddSeason("Winter 2026");
        var discord = new CommandContextRecorder();

        await DeepHandler().Stats(discord.Context, "Winter 2026", SeasonStat.Goals);

        Assert.That(discord.Embed().Description, Does.Contain("No stats yet."));
    }
}
