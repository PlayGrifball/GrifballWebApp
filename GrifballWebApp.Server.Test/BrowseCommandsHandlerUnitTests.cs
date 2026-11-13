using GrifballWebApp.Application;
using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.LateLeague;
using GrifballWebApp.Application.Leaderboards;
using GrifballWebApp.Application.Search;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Server.Commands;
using Microsoft.Extensions.Caching.Distributed;
using NSubstitute;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// BrowseCommandsHandler's unit tests, on a temp-file SQLite GrifballContext (#402). <see cref="Handler"/> covers the
/// services that need nothing but it (HomeEventsService, QuickSearchService, MatchmakingStatsService);
/// <see cref="DeepHandler"/> makes LeaderboardsService real too (IDistributedCache substituted, a miss every time),
/// for Leaderboard and Rank.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class BrowseCommandsHandlerUnitTests : SqliteGrifballContextFixture
{
    private BrowseCommandsHandler Handler() => new(null!, new MatchmakingStatsService(Db), new HomeEventsService(Db), new QuickSearchService(Db), null!, Urls());

    /// <summary>LeaderboardsService real too (IApplicationDbContext/SeasonStatsService/MatchmakingStatsService-backed;
    /// IDistributedCache substituted, a miss every time), and the player lookup real.</summary>
    private BrowseCommandsHandler DeepHandler() => new(
        new LeaderboardsService(Db, new SeasonStatsService(Db), new MatchmakingStatsService(Db), Substitute.For<IDistributedCache>(), new RecordingLogger<LeaderboardsService>()),
        new MatchmakingStatsService(Db), new HomeEventsService(Db), new QuickSearchService(Db), new CommandLookups(Db, null!), Urls());

    [Test]
    public async Task Events_RepliesNothingIsComingUp_ForAnEmptyDatabase()
    {
        var discord = new CommandContextRecorder();

        await Handler().Events(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo("Nothing is coming up."));
    }

    [Test]
    public async Task Search_RepliesNoneForEveryKind_ForAnEmptyDatabase()
    {
        var discord = new CommandContextRecorder();

        await Handler().Search(discord.Context, "winter");

        var fields = discord.Embed().Fields!.ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(fields[0].Value, Is.EqualTo("None"));
            Assert.That(fields[1].Value, Is.EqualTo("None"));
            Assert.That(fields[2].Value, Is.EqualTo("None"));
        }
    }

    [Test]
    public async Task LateLeagueSeasons_RepliesNoSeasonsYet_ForAnEmptyDatabase()
    {
        var discord = new CommandContextRecorder();

        await Handler().LateLeagueSeasons(discord.Context);

        Assert.That(discord.Embed().Description, Is.EqualTo("No Late League seasons yet."));
    }

    [Test]
    public async Task LateLeagueSeason_RepliesNoSuchSeason_ForAnEmptyDatabase()
    {
        var discord = new CommandContextRecorder();

        await Handler().LateLeagueSeason(discord.Context, null);

        Assert.That(discord.Content(), Is.EqualTo(BrowseCommandsHandler.NoSuchLateLeagueSeason));
    }

    [Test]
    public async Task Leaderboard_RepliesNobodyIsRankedYet_ForAnEmptyDatabase()
    {
        var discord = new CommandContextRecorder();

        await DeepHandler().Leaderboard(discord.Context, LeaderboardBoard.Goals, LeaderboardFormat.All);

        Assert.That(discord.Embed().Description, Does.Contain("Nobody is ranked yet."));
    }

    [Test]
    public async Task Leaderboard_Rings_RepliesNobodyIsRankedYet_ForAnEmptyDatabase()
    {
        var discord = new CommandContextRecorder();

        await DeepHandler().Leaderboard(discord.Context, LeaderboardBoard.Rings, LeaderboardFormat.LeagueSeason);

        Assert.That(discord.Embed().Description, Does.Contain("Nobody is ranked yet."));
    }

    [Test]
    public async Task Rank_RepliesNoSuchPlayer_ForAnUnknownPlayer()
    {
        var discord = new CommandContextRecorder();

        await DeepHandler().Rank(discord.Context, "nonsense", LeaderboardFormat.All);

        Assert.That(discord.Content(), Is.EqualTo(PlayerCommandsHandler.NoSuchPlayer));
    }
}
