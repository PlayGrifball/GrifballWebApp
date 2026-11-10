using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Leaderboards;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using GrifballWebApp.Application.Brackets;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.TeamStandings;
using GrifballWebApp.Infrastructure.Historical;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Waypoint subs and opponents who never signed up have an Xbox account and no user: each gets a placeholder (the
/// import's kind), so their games count in stats and leaderboards, and their sign-up merges it (audited).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxPlaceholderTests
{
    private GrifballContext _context;
    private GamertagMatching _matching;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();
        _matching = new GamertagMatching(_context);
    }

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    [Test]
    public async Task EveryXboxAccountWithNoUser_GetsAPlaceholder_Once()
    {
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "Sub One"), XboxUser.FromXbox(2, "Sub Two"));
        _context.Users.Add(User.Seed(userName: "taken", xboxUser: XboxUser.FromXbox(3, "Has A User")));
        await _context.SaveChangesAsync();

        var first = await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);
        var second = await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        var placeholders = await _context.Users.Where(u => u.IsDummyUser).OrderBy(u => u.XboxUserID).ToListAsync();
        Assert.Multiple(() =>
        {
            Assert.That((first, second), Is.EqualTo((2, 0)));
            Assert.That(placeholders.Select(p => (p.UserName, p.DisplayName, p.XboxUserID)), Is.EqualTo(new[]
            {
                ("historical:Sub One", "Sub One", (long?)1),
                ("historical:Sub Two", "Sub Two", (long?)2),
            }));
        });
    }

    [Test]
    public async Task OnlyTheGivenAccounts_AndNoneForAnEmptyList()
    {
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "Sub One"), XboxUser.FromXbox(2, "Sub Two"));
        await _context.SaveChangesAsync();

        Assert.That(await _matching.EnsureXboxPlaceholdersAsync([], CancellationToken.None), Is.Zero);
        Assert.That(await _matching.EnsureXboxPlaceholdersAsync([2], CancellationToken.None), Is.EqualTo(1));
        Assert.That(await _context.Users.Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task AnImportPlaceholderOfTheSameGamertag_IsLeftAlone_TheAccountGetsItsOwn()
    {
        // Someone used the gamertag years ago: the live account isn't assumed to be them
        _context.Users.Add(User.Seed(userName: "historical:Sub One", normalizedUserName: "HISTORICAL:SUB ONE", isDummyUser: true));
        _context.XboxUsers.Add(XboxUser.FromXbox(1, "Sub One"));
        await _context.SaveChangesAsync();

        await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        var names = await _context.Users.OrderBy(u => u.Id).Select(u => new { u.UserName, u.XboxUserID }).ToListAsync();
        Assert.That(names.Select(n => (n.UserName, n.XboxUserID)), Is.EqualTo(new[] { ("historical:Sub One", (long?)null), ("historical:Sub One:1", (long?)1) }));
    }

    [Test]
    public async Task TheJob_IsIdempotent()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(1, "Sub One"));
        await _context.SaveChangesAsync();
        var job = new XboxPlaceholderJob(new GamertagMatching(_context), NullLogger<XboxPlaceholderJob>.Instance);

        await job.Execute(Substitute.For<Quartz.IJobExecutionContext>());
        await job.Execute(Substitute.For<Quartz.IJobExecutionContext>());

        Assert.That(await _context.Users.CountAsync(u => u.IsDummyUser && u.XboxUserID == 1), Is.EqualTo(1));
    }

    [Test]
    public async Task ALiveGameWithASub_CountsTheSubInSeasonStatsAndLeaderboards()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        // 301 has an Xbox account and no user: a sub for the away team
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddMinutes(-20),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, [201, 202, 203, 301], Outcomes.Lost);
        _context.ChangeTracker.Clear();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context), new GrifballWebApp.Application.Championships.ChampionshipService(_context));
        await new SeasonMatchService(_context, Substitute.For<IDataPullService>(), bracketService, new GrifballWebApp.Application.Championships.ChampionshipService(_context)).ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);
        _context.ChangeTracker.Clear();

        await new GamertagMatching(_context).EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        var sub = await _context.Users.SingleAsync(u => u.XboxUserID == 301);
        var stats = await new SeasonStatsService(_context).GetAsync(seasonID: seeded.Season.SeasonID);
        var boards = await new LeaderboardsController(new GrifballWebApp.Application.Leaderboards.LeaderboardsService(_context, new SeasonStatsService(_context), new GrifballWebApp.Application.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())), Microsoft.Extensions.Logging.Abstractions.NullLogger<GrifballWebApp.Application.Leaderboards.LeaderboardsService>.Instance)).GetLeaderboards(null, CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(sub.IsDummyUser);
            Assert.That(stats.Where(s => s.UserID == sub.Id).Select(s => s.GamesPlayed), Is.EqualTo(new[] { 1 }));
            Assert.That(boards.KillLeaders.Select(k => k.UserID), Does.Contain(sub.Id));
        });
    }

    [Test]
    public async Task TheSubsSignUp_MergesTheirPlaceholder_WithAnAuditRecord()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(301, "GT301"));
        var person = User.Seed(userName: "person");
        _context.Users.Add(person);
        await _context.SaveChangesAsync();
        await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);
        var placeholder = await _context.Users.SingleAsync(u => u.XboxUserID == 301);
        var getsert = Substitute.For<IGetsertXboxUserService>();
        getsert.GetsertXboxUserByGamertag("GT301", Arg.Any<CancellationToken>())
            .Returns(async _ => ((XboxUser?)await _context.XboxUsers.Include(x => x.User).FirstAsync(x => x.XboxUserID == 301), (string?)null));
        var service = new SetGamertagService(_context, getsert, new UserMergeService(_context), _matching);

        var error = await service.SetGamertag(person.Id, "GT301");

        _context.ChangeTracker.Clear();
        var merge = await _context.UserMerges.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That(error, Is.Null);
            Assert.That(await _context.Users.Where(u => u.Id == person.Id).Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(301));
            Assert.That(await _context.Users.AnyAsync(u => u.Id == placeholder.Id), Is.False);
            Assert.That((merge.Trigger, merge.SourceUserID, merge.TargetUserID, merge.TriggeredByUserID), Is.EqualTo((MergeTrigger.SetGamertag, placeholder.Id, person.Id, (int?)person.Id)));
        });
    }

    [Test]
    public async Task ApprovingAClaimOnAnAccountWithItsOwnPlaceholder_MergesIntoIt_AsAnXboxPlaceholderMerge()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(50, "Now Tag"));
        var historical = User.Seed(userName: "historical:Old Tag", normalizedUserName: "HISTORICAL:OLD TAG", displayName: "Old Tag", isDummyUser: true);
        _context.Users.Add(historical);
        await _context.SaveChangesAsync();
        await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);
        var claim = GamertagClaim.FromXuidLookup(50, "Old Tag", "OLD TAG", historical.Id, "Now Tag", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _context.GamertagClaims.Add(claim);
        await _context.SaveChangesAsync();

        await new GamertagClaimService(_context, new UserMergeService(_context)).Approve(claim.GamertagClaimID);

        _context.ChangeTracker.Clear();
        var merge = await _context.UserMerges.SingleAsync();
        Assert.Multiple(async () =>
        {
            Assert.That((merge.Trigger, merge.Reason, merge.SourceUserID), Is.EqualTo((MergeTrigger.XboxPlaceholder, $"claim {claim.GamertagClaimID}", historical.Id)));
            Assert.That(await _context.Users.CountAsync(), Is.EqualTo(1));
        });
    }
}
