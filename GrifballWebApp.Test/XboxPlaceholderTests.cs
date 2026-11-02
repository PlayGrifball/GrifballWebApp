using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Brackets;
using GrifballWebApp.Server.Historical;
using GrifballWebApp.Server.Leaderboards;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.SeasonMatchPage;
using GrifballWebApp.Server.Services;
using GrifballWebApp.Server.Stats;
using GrifballWebApp.Server.TeamStandings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GrifballWebApp.Test;

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
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 1, Gamertag = "Sub One" }, new XboxUser { XboxUserID = 2, Gamertag = "Sub Two" });
        _context.Users.Add(new User { UserName = "taken", XboxUser = new XboxUser { XboxUserID = 3, Gamertag = "Has A User" } });
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
        _context.XboxUsers.AddRange(new XboxUser { XboxUserID = 1, Gamertag = "Sub One" }, new XboxUser { XboxUserID = 2, Gamertag = "Sub Two" });
        await _context.SaveChangesAsync();

        Assert.That(await _matching.EnsureXboxPlaceholdersAsync([], CancellationToken.None), Is.Zero);
        Assert.That(await _matching.EnsureXboxPlaceholdersAsync([2], CancellationToken.None), Is.EqualTo(1));
        Assert.That(await _context.Users.Select(u => u.XboxUserID).SingleAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task AnImportPlaceholderOfTheSameGamertag_IsLeftAlone_TheAccountGetsItsOwn()
    {
        // Someone used the gamertag years ago: the live account isn't assumed to be them
        _context.Users.Add(new User { UserName = "historical:Sub One", NormalizedUserName = "HISTORICAL:SUB ONE", IsDummyUser = true });
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 1, Gamertag = "Sub One" });
        await _context.SaveChangesAsync();

        await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        var names = await _context.Users.OrderBy(u => u.Id).Select(u => new { u.UserName, u.XboxUserID }).ToListAsync();
        Assert.That(names.Select(n => (n.UserName, n.XboxUserID)), Is.EqualTo(new[] { ("historical:Sub One", (long?)null), ("historical:Sub One:1", (long?)1) }));
    }

    [Test]
    public async Task TheBackfillWorker_IsIdempotent()
    {
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 1, Gamertag = "Sub One" });
        await _context.SaveChangesAsync();
        var services = new ServiceCollection()
            .AddSingleton(_context)
            .AddTransient<GamertagMatching>()
            .BuildServiceProvider();
        var worker = new XboxPlaceholderBackfillWorker(services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new XboxPlaceholderOptions()), TimeProvider.System, NullLogger<XboxPlaceholderBackfillWorker>.Instance);

        Assert.That(await worker.RunOnceAsync(CancellationToken.None), Is.EqualTo(1));
        Assert.That(await worker.RunOnceAsync(CancellationToken.None), Is.Zero);
    }

    [Test]
    public async Task ALiveGameWithASub_CountsTheSubInSeasonStatsAndLeaderboards()
    {
        var seeded = await SeasonMatchTestData.SeedAsync(_context, bestOf: 1);
        // 301 has an Xbox account and no user: a sub for the away team
        var match = await SeasonMatchTestData.AddMatchAsync(_context, DateTime.UtcNow.AddMinutes(-20),
            SeasonMatchTestData.HomeXbox, Outcomes.Won, [201, 202, 203, 301], Outcomes.Lost);
        _context.ChangeTracker.Clear();
        var bracketService = new BracketService(_context, new TeamStandingsService(_context), new GrifballWebApp.Server.Championships.ChampionshipService(_context));
        await new SeasonMatchService(_context, Substitute.For<IDataPullService>(), bracketService, new GrifballWebApp.Server.Championships.ChampionshipService(_context)).ReportMatch(seeded.SeasonMatch.SeasonMatchID, match.MatchID);
        _context.ChangeTracker.Clear();

        await new GamertagMatching(_context).EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        var sub = await _context.Users.SingleAsync(u => u.XboxUserID == 301);
        var stats = await new SeasonStatsService(_context).GetAsync(seasonID: seeded.Season.SeasonID);
        var boards = await new LeaderboardsController(new GrifballWebApp.Server.Leaderboards.LeaderboardsService(_context, new SeasonStatsService(_context), new GrifballWebApp.Server.LateLeague.MatchmakingStatsService(_context), new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()))).GetLeaderboards(null, CancellationToken.None);
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
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 301, Gamertag = "GT301" });
        var person = new User { UserName = "person" };
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
        _context.XboxUsers.Add(new XboxUser { XboxUserID = 50, Gamertag = "Now Tag" });
        var historical = new User { UserName = "historical:Old Tag", NormalizedUserName = "HISTORICAL:OLD TAG", DisplayName = "Old Tag", IsDummyUser = true };
        _context.Users.Add(historical);
        await _context.SaveChangesAsync();
        await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);
        var claim = new GamertagClaim { Kind = GamertagClaimKind.XuidLookup, XboxUserID = 50, Gamertag = "Old Tag", NormalizedGamertag = "OLD TAG", PlaceholderUserID = historical.Id };
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
