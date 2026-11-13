using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.Championships;
using GrifballWebApp.Application.Database;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.SeasonMatchPage;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Quartz;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// The jobs that pull players' games from Halo Infinite: a season match's (SeasonMatchPullJob, its recent games or
/// back to its season's start) and every player's in a season under way (ActiveSeasonPullJob), on a real (temp-file
/// SQLite) GrifballContext with the data pull substituted; each run's report says what was found and what's missing.
/// A fixed season, and a fake clock for what's under way.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SeasonMatchPullJobDbTests : SqliteContextFixture
{
    private static readonly DateTime SeasonStart = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);
    private static readonly long[] HomeXbox = [101, 102];
    private static readonly long[] AwayXbox = [201, 202];

    private readonly IDataPullService _dataPull = Substitute.For<IDataPullService>();
    private readonly IJobExecutionContext _run = Substitute.For<IJobExecutionContext>();
    private readonly MatchPullResult _found = new(4, [], 10, 4, 6, []);

    [SetUp]
    public void SetUp()
    {
        _dataPull.DownloadRecentMatchesForPlayers(default!).ReturnsForAnyArgs(_found);
        _dataPull.DownloadMatchesBetween(default!, default, default).ReturnsForAnyArgs(_found);
    }

    private SeasonMatchService SeasonMatches() => new((IApplicationDbContext)Context, _dataPull, Substitute.For<IBracketService>(), new ChampionshipService((IApplicationDbContext)Context));

    private JobRunReport Report() => (JobRunReport)_run.Result!;

    private static List<long> Everyone() => [.. HomeXbox, .. AwayXbox];

    /// <summary>A season from 1 to 31 October, two teams of two with Xbox accounts, and a match between them.</summary>
    private async Task<SeasonMatch> SeedAsync()
    {
        var season = Season.Seed(seasonName: "Pull Season").WithDates(seasonStart: SeasonStart, seasonEnd: SeasonEnd);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
        var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
        Context.Teams.AddRange(home, away);
        await Context.SaveChangesAsync();
        foreach (var id in HomeXbox)
            home.SeedPlayer(user: NewUser(id));
        foreach (var id in AwayXbox)
            away.SeedPlayer(user: NewUser(id));
        await Context.SaveChangesAsync();
        var match = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID, bestOf: 3);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return match;
    }

    private static User NewUser(long xboxId)
    {
        var user = User.Seed(userName: $"user{xboxId}", displayName: $"User {xboxId}");
        user.TieToXbox(XboxUser.FromXbox(xboxId, $"GT{xboxId}"));
        return user;
    }

    [Test]
    public async Task ASeasonMatchsPull_LooksThroughBothTeamsRecentGames()
    {
        var match = await SeedAsync();

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(_run, new SeasonMatchPull(match.SeasonMatchID));

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(Everyone().Order())), 0, 10, 25, Arg.Any<CancellationToken>());
        using (Assert.EnterMultipleScope())
        {
            Assert.That((Report().Result, Report().Summary), Is.EqualTo((JobRunResult.Succeeded, "4 players, 10 Grifball games found, 4 saved before, 6 new saved")));
            Assert.That(Report().Metrics!["matchesFetched"], Is.EqualTo(6));
        }
        _run.Received(1).ReportProgress(0, "Looking through 4 players' recent games");
    }

    [Test]
    public async Task ASeasonMatchsPull_BackToTheSeasonsStart_SearchesTheSeasonsWindow()
    {
        var match = await SeedAsync();
        var from = SeasonStart - SeasonMatchService.BeforeSeason;
        var to = SeasonEnd + SeasonMatchService.AfterSeason;

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(_run, new SeasonMatchPull(match.SeasonMatchID, BackToSeasonStart: true));

        await _dataPull.Received(1).DownloadMatchesBetween(Arg.Is<List<long>>(ids => ids.Count == 4), from, to, DataPullService.DefaultMaxCallsBack, Arg.Any<CancellationToken>());
        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
        Assert.That(Report().Summary, Is.EqualTo("2026-09-30 to 2026-11-07: 4 players, 10 Grifball games found, 4 saved before, 6 new saved"));
        _run.Received(1).ReportProgress(0, Arg.Is<string?>(m => m != null && m.StartsWith("Finding 4 players' games from ", StringComparison.Ordinal)));
    }

    [Test]
    public async Task APullMissingGames_IsAFailure_CountingWhatsMissing()
    {
        var match = await SeedAsync();
        var incomplete = new MatchPullResult(4, ["xuid(101)"], 10, 4, 4, [Guid.Empty, Guid.Empty], CappedHistories: ["xuid(201)"]);
        _dataPull.DownloadRecentMatchesForPlayers(default!).ReturnsForAnyArgs(incomplete);

        await new SeasonMatchPullJob(SeasonMatches(), _dataPull).Execute(_run, new SeasonMatchPull(match.SeasonMatchID));

        var metrics = Report().Metrics!;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Report().Result, Is.EqualTo(JobRunResult.Failed));
            Assert.That((metrics["players"], metrics["matchesFound"], metrics["matchesFailed"], metrics["historiesFailed"], metrics["historiesCapped"]),
                Is.EqualTo(((object?)4, (object?)10, (object?)2, (object?)1, (object?)1)));
        }
    }

    [Test]
    public async Task TheScheduledPull_TakesThePlayersOfSeasonsUnderWay_OnePageEach()
    {
        await SeedAsync();
        var ended = Season.Seed(seasonName: "Over").WithDates(seasonStart: SeasonStart.AddDays(-60), seasonEnd: SeasonStart.AddDays(-30));
        Context.Seasons.Add(ended);
        await Context.SaveChangesAsync();
        var oldTeam = Team.Seed(seasonID: ended.SeasonID, teamName: "Old Team");
        Context.Teams.Add(oldTeam);
        await Context.SaveChangesAsync();
        oldTeam.SeedPlayer(user: NewUser(999));
        await Context.SaveChangesAsync();
        var during = new FakeTimeProvider(new DateTimeOffset(SeasonStart.AddDays(10)));

        await new ActiveSeasonPullJob(Context, _dataPull, during).Execute(_run);

        await _dataPull.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(ids => ids.Order().SequenceEqual(Everyone().Order())), 0, 0, 25, Arg.Any<CancellationToken>());
        Assert.That(Report().Result, Is.EqualTo(JobRunResult.Succeeded));
    }

    [Test]
    public async Task TheScheduledPull_WithNoSeasonUnderWay_AsksHaloNothing()
    {
        await SeedAsync();
        var before = new FakeTimeProvider(new DateTimeOffset(SeasonStart.AddDays(-1)));

        await new ActiveSeasonPullJob(Context, _dataPull, before).Execute(_run);

        await _dataPull.DidNotReceiveWithAnyArgs().DownloadRecentMatchesForPlayers(default!);
        Assert.That((Report().Result, Report().Summary), Is.EqualTo((JobRunResult.Skipped, "No season under way")));
    }
}
