using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class SetGamertagServiceTests
{
    private GrifballContext _context;
    private IGetsertXboxUserService _getsertXboxUserService;
    private IUserMergeService _userMergeService;
    private SetGamertagService _setGamertagService;

    [SetUp]
    public async Task Setup()
    {
        _context = await SetUpFixture.NewGrifballContext();

        _getsertXboxUserService = Substitute.For<IGetsertXboxUserService>();

        // We'll test user merge service too
        _userMergeService = new UserMergeService(_context);

        _setGamertagService = new SetGamertagService(
            _context,
            _getsertXboxUserService,
            _userMergeService,
            new GrifballWebApp.Server.Historical.GamertagMatching(_context)
        );
    }

    [TearDown]
    public async Task Teardown()
    {
        await _context.DropDatabaseAndDispose();
    }

    [Test]
    public async Task CanSetGamertag()
    {
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var user = new User
        {
            Id = 1
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre",
                User = null // Simulating a new gamertag not associated with any user
            }, null));
        await transaction.CommitAsync();

        // Act  
        var result = await _setGamertagService.SetGamertag(1, "Grunt Padre");
        var userAfter = await _context.Users
            .Include(x => x.XboxUser)
            .Where(x => x.Id == 1)
            .FirstOrDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(userAfter, Is.Not.Null);
            Assert.That(userAfter?.XboxUser, Is.Not.Null);
            Assert.That(userAfter?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"));
        });
    }

    [Test]
    public async Task CannotStealGamertag()
    {
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            }
        };
        _context.Users.Add(user);
        var user2 = new User
        {
            Id = 2,
        };
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user.XboxUser, null));
        await transaction.CommitAsync();

        // Act  
        var result = await _setGamertagService.SetGamertag(2, "Grunt Padre");

        Assert.That(result, Is.EqualTo("That gamertag is already attached to a user. Contact sysadmin if you believe this is incorrect"));
        Assert.That(_context.Database.CurrentTransaction, Is.Null);
    }

    [Test]
    public async Task CannotSetIfUserDoesNotExist()
    {
        // Act  
        var result = await _setGamertagService.SetGamertag(1, "Grunt Padre");

        Assert.That(result, Is.EqualTo("User does not exist"));
        Assert.That(_context.Database.CurrentTransaction, Is.Null);
    }

    [Test]
    public async Task CannotSetAlreadySetGamertag()
    {
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            }
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        // Act  
        var result = await _setGamertagService.SetGamertag(1, "New Example Tag - Doesn't matter for test");

        Assert.That(result, Is.EqualTo("Gamertag is already set for this user. Contact sysadmin if it needs to be changed"));
        Assert.That(_context.Database.CurrentTransaction, Is.Null);
    }

    [Test]
    public async Task HandleGetsertError()
    {
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var user = new User
        {
            Id = 1,
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((null, "foobar"));
        await transaction.CommitAsync();

        // Act  
        var result = await _setGamertagService.SetGamertag(1, "New Example Tag - Doesn't matter for test");

        Assert.That(result, Is.EqualTo("Failed to set gamertag, reason: foobar"));
        Assert.That(_context.Database.CurrentTransaction, Is.Null);
    }

    [Test]
    public async Task AllowDummyTakeover()
    {
        // Arrange
        using var transaction = await _context.Database.BeginTransactionAsync();
        await _context.DisableContraints("[Auth].[Users]");
        var season = new Season()
        {
            SeasonName = "Test Season",
        };
        var teamplayer = new TeamPlayer
        {
            Team = new Team()
            {
                Season = season,
            },
        };
        _context.TeamPlayers.Add(teamplayer);
        var seasonSignup = new SeasonSignup
        {
            Season = season,
        };
        _context.SeasonSignups.Add(seasonSignup);
        var user = new User
        {
            Id = 1,
            XboxUser = new XboxUser
            {
                XboxUserID = 1234567890,
                Gamertag = "Grunt Padre"
            },
            DiscordUser = new DiscordUser
            {
                DiscordUserID = 1337,
                DiscordUsername = "discordusernamegohere",
            },
            TeamPlayers = [
                teamplayer,
                ],
            // PersonExperiences
            SeasonSignups = [
                seasonSignup,
                ],
            IsDummyUser = true // Simulating a dummy user
        };
        _context.Users.Add(user);
        var user2 = new User
        {
            Id = 2,
        };
        _context.Users.Add(user2);
        await _context.SaveChangesAsync();
        await _context.EnableContraints("[Auth].[Users]");
        await transaction.CommitAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((user.XboxUser, null));

        // Act  
        var result = await _setGamertagService.SetGamertag(2, "Grunt Padre");
        var user1After = await _context.Users
            .Where(x => x.Id == 1)
            .FirstOrDefaultAsync();
        var user2After = await _context.Users
            .Include(x => x.XboxUser)
            .Include(x => x.DiscordUser)
            .Include(x => x.TeamPlayers).ThenInclude(x => x.Team.Season)
            .Include(x => x.SeasonSignups).ThenInclude(x => x.Season)
            .Where(x => x.Id == 2)
            .FirstOrDefaultAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(user1After, Is.Null); // Should have been deleted (dummy user was merged into new real account)
            Assert.That(user2After, Is.Not.Null);
            // Now from this line on lets make sure that user2 owns everything that user1 did own prior
            Assert.That(user2After?.XboxUser, Is.Not.Null);
            Assert.That(user2After?.XboxUser?.XboxUserID, Is.EqualTo(1234567890));
            Assert.That(user2After?.XboxUser?.Gamertag, Is.EqualTo("Grunt Padre"));
            Assert.That(user2After?.DiscordUser, Is.Not.Null);
            Assert.That(user2After?.DiscordUser?.DiscordUserID, Is.EqualTo(1337));
            Assert.That(user2After?.DiscordUser?.DiscordUsername, Is.EqualTo("discordusernamegohere"));
            Assert.That(user2After?.TeamPlayers, Is.Not.Null);
            Assert.That(user2After?.TeamPlayers.Count, Is.EqualTo(1));
            Assert.That(user2After?.TeamPlayers.ElementAtOrDefault(0)?.Team.Season.SeasonName, Is.EqualTo("Test Season"));
            Assert.That(user2After?.SeasonSignups, Is.Not.Null);
            Assert.That(user2After?.SeasonSignups.Count, Is.EqualTo(1));
            Assert.That(user2After?.SeasonSignups.ElementAtOrDefault(0)?.Season.SeasonName, Is.EqualTo("Test Season"));
        });
    }

    [Test]
    public async Task DummyTakeover_WhenBothHaveExperiencesAndSignupsForTheSameSeason_Merges()
    {
        // The automatic merge used to fail (500) on UserExperience's key and the (UserID, SeasonID) signup index.
        var season = await _context.AddSeason();
        var dummy = await _context.AddUser("dummy", gamertag: "Placeholder GT");
        dummy.IsDummyUser = true;
        var real = await _context.AddUser("real");
        _context.UserExperiences.AddRange(
            new UserExperience { UserID = dummy.Id, GameVersionID = 1 },
            new UserExperience { UserID = real.Id, GameVersionID = 1 });
        await _context.SaveChangesAsync();
        await _context.AddSignup(season, dummy);
        await _context.AddSignup(season, real);
        var xbox = await _context.XboxUsers.Include(x => x.User).SingleAsync(x => x.XboxUserID == dummy.XboxUserID);
        _getsertXboxUserService.GetsertXboxUserByGamertag("Placeholder GT", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Placeholder GT");

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.Null);
            Assert.That(await fresh.Users.AnyAsync(x => x.Id == dummy.Id), Is.False);
            Assert.That((await fresh.Users.SingleAsync(x => x.Id == real.Id)).XboxUserID, Is.EqualTo(xbox.XboxUserID));
            Assert.That(await fresh.SeasonSignups.CountAsync(), Is.EqualTo(1));
            Assert.That(await fresh.UserExperiences.Select(x => x.UserID).ToListAsync(), Is.EqualTo(new[] { real.Id }));
        });
    }

    [Test]
    public async Task DummyTakeover_WhenOnDifferentTeamsInOneSeason_SaysWhy()
    {
        var season = await _context.AddSeason("S1");
        var dummy = await _context.AddUser("dummy", gamertag: "Placeholder GT");
        dummy.IsDummyUser = true;
        await _context.SaveChangesAsync();
        var real = await _context.AddUser("real");
        await _context.AddTeam(season, "A", dummy, 1);
        await _context.AddTeam(season, "B", real, 2);
        var xbox = await _context.XboxUsers.Include(x => x.User).SingleAsync(x => x.XboxUserID == dummy.XboxUserID);
        _getsertXboxUserService.GetsertXboxUserByGamertag("Placeholder GT", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Placeholder GT");

        Assert.That(result, Does.Contain("on different teams in season S1"));
        await using var fresh = _context.NewContextLike();
        Assert.That((await fresh.Users.SingleAsync(x => x.Id == real.Id)).XboxUserID, Is.Null, "Nothing changed");
    }

    /// <summary>A placeholder only sharing the gamertag (someone may have used it years ago) is not merged: a commissioner decides.</summary>
    [Test]
    public async Task DummyNamesake_NotTiedToTheXboxAccount_IsNotMerged_ButClaimedForACommissioner()
    {
        var dummy = new User
        {
            UserName = "historical:Ghost Spartan",
            NormalizedUserName = "HISTORICAL:GHOST SPARTAN",
            DisplayName = "Ghost Spartan",
            IsDummyUser = true
        };
        var real = await _context.AddUser("real_ghost");
        _context.Users.Add(dummy);
        await _context.SaveChangesAsync();

        var season = await _context.AddSeason("Historical S1");
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat
        {
            UserID = dummy.Id,
            SeasonID = season.SeasonID,
            GameVersionID = 1,
            Gamertag = "Ghost Spartan",
            GamesPlayed = 10,
            Wins = 8,
            Losses = 2,
            Goals = 12,
            Kills = 100,
            Deaths = 50,
            StintIndex = 1,
            IsPlayoffs = false
        });
        await _context.SaveChangesAsync();

        var newXboxUser = new XboxUser { XboxUserID = 998877, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.Add(newXboxUser);
        await _context.SaveChangesAsync();

        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>())
            .Returns((newXboxUser, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.Null, "The gamertag is set");
            Assert.That(await fresh.Users.AnyAsync(x => x.Id == dummy.Id), Is.True, "The placeholder is not merged");
            Assert.That((await fresh.Users.SingleAsync(x => x.Id == real.Id)).XboxUserID, Is.EqualTo(998877));
            Assert.That(await fresh.PlayerSeasonStats.Where(s => s.UserID == dummy.Id).CountAsync(), Is.EqualTo(1), "Its stats stay with it");
            var claim = await fresh.GamertagClaims.SingleAsync();
            Assert.That((claim.Kind, claim.Status, claim.XboxUserID, claim.PlaceholderUserID, claim.Gamertag), Is.EqualTo((GamertagClaimKind.SetGamertag, GamertagClaimStatus.Pending, 998877L, (int?)dummy.Id, "Ghost Spartan")));
        });
    }

    /// <summary>A placeholder of a gamertag the Xbox account is known to have had (its history) is the player's: merged.</summary>
    [Test]
    public async Task DummyOfAPastGamertagOfTheXboxAccount_IsMerged()
    {
        var dummy = new User { UserName = "historical:Old Ghost", NormalizedUserName = "HISTORICAL:OLD GHOST", DisplayName = "Old Ghost", IsDummyUser = true };
        var real = await _context.AddUser("real_ghost");
        _context.Users.Add(dummy);
        await _context.SaveChangesAsync();
        var season = await _context.AddSeason("Historical S1");
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = dummy.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = "Old Ghost", Kills = 100 });
        var xbox = new XboxUser { XboxUserID = 998877, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.Add(xbox);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 998877, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.Null);
            Assert.That(await fresh.Users.AnyAsync(x => x.Id == dummy.Id), Is.False, "Merged and deleted");
            Assert.That(await fresh.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(real.Id));
            Assert.That(await fresh.GamertagClaims.AnyAsync(), Is.False);
        });
    }

    [Test]
    public async Task DummyOfAPastGamertag_ThatCannotBeMerged_RefusesWithTheReason()
    {
        var dummy = new User { UserName = "historical:Old Ghost", NormalizedUserName = "HISTORICAL:OLD GHOST", DisplayName = "Old Ghost", IsDummyUser = true };
        var real = await _context.AddUser("real_ghost");
        _context.Users.Add(dummy);
        await _context.SaveChangesAsync();
        var season = await _context.AddSeason("Historical S1");
        _context.PlayerSeasonStats.AddRange(
            new PlayerSeasonStat { UserID = dummy.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = "Old Ghost" },
            new PlayerSeasonStat { UserID = real.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = "Real" });
        var xbox = new XboxUser { XboxUserID = 998877, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.Add(xbox);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 998877, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", Source = GamertagHistorySource.Manual });
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        Assert.That(result, Does.Contain("could not be merged"));
    }

    private async Task<User> PlaceholderWithARow(string gamertag, int seasonYear, string seasonName)
    {
        var placeholder = new User { UserName = "historical:" + gamertag, NormalizedUserName = ("historical:" + gamertag).ToUpperInvariant(), DisplayName = gamertag, IsDummyUser = true };
        _context.Users.Add(placeholder);
        var season = new Season { SeasonName = seasonName, SeasonStart = new DateTime(seasonYear, 1, 1, 0, 0, 0, DateTimeKind.Utc), SeasonEnd = new DateTime(seasonYear, 12, 31, 0, 0, 0, DateTimeKind.Utc) };
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = placeholder.Id, SeasonID = season.SeasonID, GameVersionID = 1, Gamertag = gamertag });
        await _context.SaveChangesAsync();
        return placeholder;
    }

    [Test]
    public async Task DummyOfAPastGamertag_FromSeasonsAfterTheAccountLetGoOfIt_IsClaimedNotMerged()
    {
        var real = await _context.AddUser("real_ghost");
        var placeholder = await PlaceholderWithARow("Old Ghost", 2024, "Season 2024");
        var xbox = new XboxUser { XboxUserID = 998877, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.Add(xbox);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 998877, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", LastSeen = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        await using var fresh = _context.NewContextLike();
        Assert.That(result, Is.Null);
        Assert.That(await fresh.Users.AnyAsync(u => u.Id == placeholder.Id), "Someone else's 2024 Old Ghost, as far as is known");
        Assert.That(await fresh.GamertagClaims.Select(c => c.PlaceholderUserID).SingleAsync(), Is.EqualTo(placeholder.Id));
    }

    [Test]
    public async Task DummyOfAPastGamertag_AnotherAccountAlsoHadThen_IsClaimedNotMerged()
    {
        var real = await _context.AddUser("real_ghost");
        var placeholder = await PlaceholderWithARow("Old Ghost", 2019, "Season 2019");
        var xbox = new XboxUser { XboxUserID = 998877, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.AddRange(xbox, new XboxUser { XboxUserID = 112233, Gamertag = "Someone Else" });
        _context.XboxGamertagHistory.AddRange(
            new XboxGamertagHistory { XboxUserID = 998877, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", Source = GamertagHistorySource.RenameDetected },
            new XboxGamertagHistory { XboxUserID = 112233, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", LastSeen = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc), Source = GamertagHistorySource.Manual });
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>()).Returns((xbox, null));

        await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        await using var fresh = _context.NewContextLike();
        Assert.That(await fresh.Users.AnyAsync(u => u.Id == placeholder.Id));
        Assert.That(await fresh.GamertagClaims.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task DummiesOfPastGamertags_AreMergedAllOrNothing()
    {
        var real = await _context.AddUser("real_ghost");
        var first = await PlaceholderWithARow("Old Ghost", 2019, "Season 2019");
        var second = await PlaceholderWithARow("Older Ghost", 2018, "Season 2018");
        // The second can't be merged: both it and the real user have stats for 2018
        var season2018 = await _context.Seasons.SingleAsync(s => s.SeasonName == "Season 2018");
        _context.PlayerSeasonStats.Add(new PlayerSeasonStat { UserID = real.Id, SeasonID = season2018.SeasonID, GameVersionID = 1, Gamertag = "Real" });
        var xbox = new XboxUser { XboxUserID = 998877, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.Add(xbox);
        _context.XboxGamertagHistory.AddRange(
            new XboxGamertagHistory { XboxUserID = 998877, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", Source = GamertagHistorySource.RenameDetected },
            new XboxGamertagHistory { XboxUserID = 998877, Gamertag = "Older Ghost", NormalizedGamertag = "OLDER GHOST", Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Does.Contain("could not be merged"));
            Assert.That(await fresh.Users.CountAsync(u => u.Id == first.Id || u.Id == second.Id), Is.EqualTo(2), "Neither merged");
            Assert.That((await fresh.Users.SingleAsync(u => u.Id == real.Id)).XboxUserID, Is.Null, "Nothing changed");
        });
    }

    [Test]
    public async Task TheXuidHeldByAnImportPlaceholder_AndARenamedPastGamertag_AreBothMerged()
    {
        var real = await _context.AddUser("real_ghost");
        var xbox = new XboxUser { XboxUserID = 778899, Gamertag = "Ghost Spartan" };
        _context.XboxUsers.Add(xbox);
        await _context.SaveChangesAsync();
        // The import tied a placeholder to the XUID (its Waypoint games), and the sheets of an older season have the old name
        var holding = new User { UserName = "historical:Ghost Spartan", NormalizedUserName = "HISTORICAL:GHOST SPARTAN", DisplayName = "Ghost Spartan", IsDummyUser = true, XboxUserID = 778899 };
        var older = await PlaceholderWithARow("Old Ghost", 2019, "Winter League 2019");
        _context.Users.Add(holding);
        _context.XboxGamertagHistory.Add(new XboxGamertagHistory { XboxUserID = 778899, Gamertag = "Old Ghost", NormalizedGamertag = "OLD GHOST", Source = GamertagHistorySource.RenameDetected });
        await _context.SaveChangesAsync();
        _getsertXboxUserService.GetsertXboxUserByGamertag("Ghost Spartan", Arg.Any<CancellationToken>()).Returns((xbox, null));

        var result = await _setGamertagService.SetGamertag(real.Id, "Ghost Spartan");

        await using var fresh = _context.NewContextLike();
        Assert.Multiple(async () =>
        {
            Assert.That(result, Is.Null);
            Assert.That((await fresh.Users.SingleAsync(u => u.Id == real.Id)).XboxUserID, Is.EqualTo(778899L));
            Assert.That(await fresh.Users.AnyAsync(u => u.Id == holding.Id || u.Id == older.Id), Is.False, "Both placeholders merged");
            Assert.That(await fresh.PlayerSeasonStats.Select(s => s.UserID).SingleAsync(), Is.EqualTo(real.Id));
        });
    }
}
