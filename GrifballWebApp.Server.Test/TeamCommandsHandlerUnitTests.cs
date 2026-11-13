using GrifballWebApp.Application;
using GrifballWebApp.Application.Historical.Vods;
using GrifballWebApp.Application.TeamPage;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// TeamCommandsHandler's unit tests: the team lookup (<see cref="CommandLookups.Team"/>) every action goes through first
/// is real, on a temp-file SQLite GrifballContext (#402); <see cref="Handler"/> leaves the team page services null, for
/// the lookup guard; <see cref="DeepHandler"/> makes them real too, for the actions themselves.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class TeamCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private TeamCommandsHandler Handler() => new(new CommandLookups(Db, null!), null!, null!, null!, Urls());

    /// <summary>The team page services real too (all IApplicationDbContext-backed).</summary>
    private TeamCommandsHandler DeepHandler() => new(new CommandLookups(Db, null!),
        new TeamPageService(Db), new TeamHistoryService(Db),
        new VodService(Db, null!, Options.Create(new YouTubeOptions()), null!), Urls());

    [TestCase(nameof(TeamCommandsHandler.Info))]
    [TestCase(nameof(TeamCommandsHandler.Roster))]
    [TestCase(nameof(TeamCommandsHandler.History))]
    [TestCase(nameof(TeamCommandsHandler.Matches))]
    [TestCase(nameof(TeamCommandsHandler.Vods))]
    public async Task Actions_ReplyNoSuchTeam_WhenTheNamedTeamIsNotFound(string method)
    {
        var discord = new CommandContextRecorder();
        var handler = Handler();

        Task task = method switch
        {
            nameof(TeamCommandsHandler.Info) => handler.Info(discord.Context, "nonsense"),
            nameof(TeamCommandsHandler.Roster) => handler.Roster(discord.Context, "nonsense"),
            nameof(TeamCommandsHandler.History) => handler.History(discord.Context, "nonsense"),
            nameof(TeamCommandsHandler.Matches) => handler.Matches(discord.Context, "nonsense"),
            _ => handler.Vods(discord.Context, "nonsense"),
        };
        await task;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(discord.DeferredPublicly(), Is.True);
            Assert.That(discord.Content(), Is.EqualTo(TeamCommandsHandler.NoSuchTeam));
        }
    }

    [Test]
    public async Task Info_ShowsTheTeam_ForAFoundTeam()
    {
        var season = await AddSeason("Winter 2026");
        await AddTeam(season, "Grifters");
        var discord = new CommandContextRecorder();

        await DeepHandler().Info(discord.Context, "Grifters");

        Assert.That(discord.Embed().Title, Is.EqualTo("Grifters"));
    }

    [Test]
    public async Task Roster_RepliesNoPlayersYet_ForAFoundTeamWithNoRoster()
    {
        var season = await AddSeason("Winter 2026");
        await AddTeam(season, "Grifters");
        var discord = new CommandContextRecorder();

        await DeepHandler().Roster(discord.Context, "Grifters");

        Assert.That(discord.Embed().Description, Does.Contain("No players yet."));
    }

    [Test]
    public async Task Matches_RepliesNoMatchesYet_ForAFoundTeamWithNoMatches()
    {
        var season = await AddSeason("Winter 2026");
        await AddTeam(season, "Grifters");
        var discord = new CommandContextRecorder();

        await DeepHandler().Matches(discord.Context, "Grifters");

        Assert.That(discord.Embed().Description, Does.Contain("No matches yet."));
    }
}
