using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.MatchDetails;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Application.Historical;
using GrifballWebApp.Infrastructure.Historical;
using GrifballWebApp.Server.XboxAccounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// Xbox accounts that are no league players, on each database: their own page (<see cref="XboxAccountService"/>), the game
/// page's link to it, and the cleanup of the placeholders made for them before (<see cref="XboxPlaceholderCleanup"/>).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxAccountTests
{
    private static readonly string[] KeptUsers = ["historical:Rostered", "historical:Sub", "signedup"];

    private GrifballContext _context = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = await TestDatabase.NewGrifballContext();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _context.DropDatabaseAndDispose();
    }

    private async Task<Match> Game(DateTime? start, long xuid, int score, Outcomes outcome, long? opponent = null)
    {
        var match = Match.Seed(Guid.NewGuid(), startTime: start);
        match.AddTeam(0, score, outcome);
        match.AddParticipant(0, xuid, new MatchParticipantStats { Kills = 7, Deaths = 3, Assists = 2 });
        if (opponent is long other)
        {
            match.AddTeam(1, 1, outcome == Outcomes.Won ? Outcomes.Lost : Outcomes.Won);
            match.AddParticipant(1, other, new MatchParticipantStats());
        }
        _context.Matches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    [Test]
    public async Task AnAccountNobodyHolds_ShowsItsGamesNewestFirst_WithNoAccount()
    {
        _context.XboxUsers.AddRange(XboxUser.FromXbox(1, "Pickup"), XboxUser.FromXbox(2, "Them"));
        await _context.SaveChangesAsync();
        var older = await Game(new DateTime(2026, 10, 1, 20, 0, 0, DateTimeKind.Utc), 1, 5, Outcomes.Won, opponent: 2);
        var newer = await Game(new DateTime(2026, 10, 2, 20, 0, 0, DateTimeKind.Utc), 1, 2, Outcomes.Lost, opponent: 2);
        var undated = await Game(null, 1, 4, Outcomes.Won);

        var account = (await new XboxAccountService(_context).GetAsync(1))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(account.Gamertag, Is.EqualTo("Pickup"));
            Assert.That(account.UserID, Is.Null, "no account was created for it");
            Assert.That(account.GameCount, Is.EqualTo(3));
            Assert.That(account.Games.Select(g => g.MatchID), Is.EqualTo(new[] { newer.MatchID, older.MatchID, undated.MatchID }));
            Assert.That((account.Games[1].Score, account.Games[1].OtherScore, account.Games[1].Outcome), Is.EqualTo((5, (int?)1, Outcomes.Won)));
            Assert.That((account.Games[1].Kills, account.Games[1].Deaths, account.Games[1].Assists), Is.EqualTo((7, 3, 2)));
            Assert.That(account.Games[2].OtherScore, Is.Null, "a game with one team");
        }
    }

    [Test]
    public async Task AnAccountAUserHolds_NamesTheirProfile_AndAnUnknownOneIsNotFound()
    {
        var user = User.Seed(userName: "holder", xboxUser: XboxUser.FromXbox(3, "Held"));
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var account = await new XboxAccountService(_context).GetAsync(3);
        var controller = new XboxAccountsController(new XboxAccountService(_context));
        var missing = await controller.GetXboxAccount(999, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(account!.UserID, Is.EqualTo(user.Id));
            Assert.That(account.Games, Is.Empty);
            Assert.That(missing.Result, Is.InstanceOf<ObjectResult>().With.Property(nameof(ObjectResult.StatusCode)).EqualTo(404));
        }
    }

    [Test]
    public async Task TheGamesPage_ComesInPages()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(4, "Busy"));
        await _context.SaveChangesAsync();
        for (var i = 0; i < XboxAccountService.PageSize + 2; i++)
            await Game(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(i), 4, 1, Outcomes.Won);

        var second = (await new XboxAccountService(_context).GetAsync(4, page: 2))!;
        // A page past the end from the query string: empty, not an overflow the database refuses
        var far = (await new XboxAccountService(_context).GetAsync(4, page: int.MaxValue))!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(second.GameCount, Is.EqualTo(XboxAccountService.PageSize + 2));
            Assert.That(second.Games, Has.Count.EqualTo(2));
            Assert.That(second.Page, Is.EqualTo(2));
            Assert.That(far.Games, Is.Empty);
        }
    }

    [Test]
    public async Task TheGamePage_GivesEachPlayersXboxAccount()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(5, "No Account"));
        await _context.SaveChangesAsync();
        var game = await Game(DateTime.UtcNow, 5, 3, Outcomes.Won);

        var details = (await new MatchDetailsService(_context).GetAsync(game.MatchID))!;

        var player = details.Teams.Single().Players.Single();
        Assert.That((player.UserID, player.XboxUserID), Is.EqualTo(((int?)null, 5L)));
    }

    [Test]
    public async Task TheCleanup_RemovesAPickupOnlyPlaceholder_AndKeepsEveryOneTheLeagueNames()
    {
        // 10: only in a pickup game, named by nothing: removed. 11: in a league game. 12: on a team. 13: a real user.
        _context.XboxUsers.AddRange(XboxUser.FromXbox(10, "Pickup"), XboxUser.FromXbox(11, "Sub"), XboxUser.FromXbox(12, "Rostered"), XboxUser.FromXbox(13, "Signed Up"));
        await _context.SaveChangesAsync();
        var pickup = User.Seed(userName: "historical:Pickup", isDummyUser: true, xboxUserID: 10);
        var sub = User.Seed(userName: "historical:Sub", isDummyUser: true, xboxUserID: 11);
        var rostered = User.Seed(userName: "historical:Rostered", isDummyUser: true, xboxUserID: 12);
        var real = User.Seed(userName: "signedup", xboxUserID: 13);
        _context.Users.AddRange(pickup, sub, rostered, real);
        await _context.SaveChangesAsync();
        await SeasonMatchTestData.InAPickupGameAsync(_context, 10, 12, 13);
        await SeasonMatchTestData.InALeagueGameAsync(_context, 11);
        var season = Season.Seed(seasonName: "Roster");
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var team = Team.Seed(seasonID: season.SeasonID, teamName: "Team");
        team.SeedPlayer(user: rostered);
        _context.Teams.Add(team);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var removed = await new XboxPlaceholderCleanup(_context).RemoveAsync();

        _context.ChangeTracker.Clear();
        var left = await _context.Users.Select(u => u.UserName).OrderBy(n => n).ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(removed, Is.EqualTo(new[] { new RemovedPlaceholder(pickup.Id, "Pickup") }));
            Assert.That(left, Is.EqualTo(KeptUsers));
            Assert.That(await _context.XboxUsers.AnyAsync(x => x.XboxUserID == 10), Is.True, "the account and its games stay");
            Assert.That(await new XboxPlaceholderCleanup(_context).RemoveAsync(), Is.Empty, "nothing left to remove");
        }
    }

    [Test]
    public async Task TheCleanup_KeepsAPlaceholderAnythingElseNames_OrThatCarriesMoreThanItsXboxAccount()
    {
        // Each only in pickup games, named by no foreign key: kept for what a key-less column or the row itself holds
        long[] xuids = [40, 41, 42, 43, 44, 45, 46, 47];
        foreach (var xuid in xuids)
            _context.XboxUsers.Add(XboxUser.FromXbox(xuid, $"GT{xuid}"));
        await _context.SaveChangesAsync();
        // As EnsureXboxPlaceholdersAsync makes one, and the case of its prefix doesn't matter on any database
        var plain = User.Seed(userName: "HISTORICAL:GT40", displayName: "GT40", isDummyUser: true, xboxUserID: 40);
        var merged = User.Seed(userName: "historical:GT41", isDummyUser: true, xboxUserID: 41);
        var dismissed = User.Seed(userName: "historical:GT42", isDummyUser: true, xboxUserID: 42);
        var discord = User.Seed(userName: "historical:GT43", isDummyUser: true, xboxUserID: 43, discordUser: DiscordUser.FromDiscord(9_300_043, "linked"));
        var signedIn = User.Seed(userName: "historical:GT44", isDummyUser: true, xboxUserID: 44, email: "someone@example.com", passwordHash: "hash");
        var region = User.Seed(userName: "historical:GT45", isDummyUser: true, xboxUserID: 45, regionID: 1, regionSource: GrifballWebApp.Domain.Shared.RegionSource.Sysadmin);
        var lateLeague = User.Seed(userName: "historical:GT46", isDummyUser: true, xboxUserID: 46);
        // A sysadmin renamed it on user management: no longer as it was made
        var renamed = User.Seed(userName: "historical:GT47", displayName: "Renamed By A Sysadmin", isDummyUser: true, xboxUserID: 47);
        _context.Users.AddRange(plain, merged, dismissed, discord, signedIn, region, lateLeague, renamed);
        await _context.SaveChangesAsync();
        await SeasonMatchTestData.InAPickupGameAsync(_context, xuids);
        await SeasonMatchTestData.InALateLeagueGameAsync(_context, 46);
        _context.UserMerges.Add(UserMerge.Record(DateTime.UtcNow, MergeTrigger.SetGamertag, null, null, 999, "gone", merged.Id, "historical:GT41", "{}", 0, 0, 0));
        _context.DuplicateDismissals.Add(DuplicateDismissal.Dismiss(dismissed.Id, discord.Id, DateTime.UtcNow, null));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var removed = await new XboxPlaceholderCleanup(_context).RemoveAsync();

        Assert.That(removed.Select(r => r.UserID), Is.EqualTo(new[] { plain.Id }));
    }

    [Test]
    public async Task TheCleanupJob_LogsWhatItRemoved()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(20, "Gone"));
        await _context.SaveChangesAsync();
        _context.Users.Add(User.Seed(userName: "historical:Gone", isDummyUser: true, xboxUserID: 20));
        await _context.SaveChangesAsync();

        await new XboxPlaceholderCleanupJob(new XboxPlaceholderCleanup(_context), NullLogger<XboxPlaceholderCleanupJob>.Instance)
            .Execute(Substitute.For<Quartz.IJobExecutionContext>());

        Assert.That(await _context.Users.AnyAsync(), Is.False);
    }

    [Test]
    public async Task ThePullButton_QueuesTheAccountsPull_AsTheUser_ForPlayersAndStaff()
    {
        _context.XboxUsers.Add(XboxUser.FromXbox(30, "Pull Me"));
        await _context.SaveChangesAsync();
        var pulls = Substitute.For<IXboxAccountPulls>();
        var queued = new OnDemandJobStatusDto { State = OnDemandJobState.Queued };
        pulls.Queue(30, 21, Arg.Any<CancellationToken>()).Returns(queued);
        var controller = new XboxAccountsController(new XboxAccountService(_context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "21")], "test")),
                },
            },
        };

        var pulled = await controller.PullXboxAccount(30, pulls, CancellationToken.None);
        var unknown = await controller.PullXboxAccount(31, pulls, CancellationToken.None);
        var roles = typeof(XboxAccountsController).GetMethod(nameof(XboxAccountsController.PullXboxAccount))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false).Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Roles;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(pulled.Result, Is.InstanceOf<AcceptedResult>().With.Property(nameof(AcceptedResult.Value)).SameAs(queued));
            Assert.That(unknown.Result, Is.InstanceOf<ObjectResult>().With.Property(nameof(ObjectResult.StatusCode)).EqualTo(404));
            Assert.That(roles, Is.EqualTo("Player,Commissioner,Sysadmin"));
        }
        await pulls.Received(1).Queue(30, 21, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ThePullJob_PullsTheAccountsRecentGames_AndSaysHowItWent()
    {
        var dataPull = Substitute.For<GrifballWebApp.Application.HaloInfinite.IDataPullService>();
        dataPull.DownloadRecentMatchesForPlayers(Arg.Any<List<long>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new GrifballWebApp.Application.HaloInfinite.MatchPullResult(1, [], 3, 1, 2, []));
        var context = Substitute.For<Quartz.IJobExecutionContext>();

        await new GrifballWebApp.Infrastructure.HaloInfinite.XboxAccountPullJob(dataPull).Execute(context, new GrifballWebApp.Infrastructure.HaloInfinite.XboxAccountPull(30));

        await dataPull.Received(1).DownloadRecentMatchesForPlayers(Arg.Is<List<long>>(ids => ids.Count == 1 && ids[0] == 30), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        Assert.That(((Quartz.JobRunReport)context.Result!).Result, Is.EqualTo(Quartz.JobRunResult.Succeeded));
    }
}
