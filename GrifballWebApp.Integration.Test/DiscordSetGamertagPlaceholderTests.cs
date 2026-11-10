using DiscordInterface.Generated;
using GrifballWebApp.Infrastructure;
using GrifballWebApp.Server.Matchmaking;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Role = GrifballWebApp.Domain.Players.Role;
using User = GrifballWebApp.Domain.Players.User;
using DiscordUser = GrifballWebApp.Domain.Players.DiscordUser;
using GrifballWebApp.Application.Profile;

namespace GrifballWebApp.Integration.Test;

/// <summary>
/// The archive's players lists can link a Discord account to a placeholder: setting a gamertag through the bot then
/// gives the person their own account and merges the placeholder into it (audited), instead of playing as the placeholder.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class DiscordSetGamertagPlaceholderTests
{
    private const ulong DiscordId = 386902644670595072;

    private GrifballContext _context;
    private ServiceProvider _provider;
    private IServiceScope _scope;
    private UserManager<User> _userManager;
    private ISetGamertagService _setGamertag;
    private DiscordSetGamertag _sut;
    private IDiscordInteractionContext _interaction;
    private User _placeholder;

    [SetUp]
    public async Task SetUp()
    {
        _context = await SetUpFixture.NewGrifballContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => _context);
        services.AddIdentity<User, Role>().AddEntityFrameworkStores<GrifballContext>();
        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _userManager = _scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        _setGamertag = Substitute.For<ISetGamertagService>();
        _setGamertag.SetGamertag(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        _sut = new DiscordSetGamertag(_context, Substitute.For<ILogger<DiscordSetGamertag>>(), _userManager, _setGamertag, new UserMergeService(_context));
        _interaction = Substitute.For<IDiscordInteractionContext>();
        _interaction.Interaction.User.Id.Returns(DiscordId);
        _interaction.Interaction.User.Username.Returns("activehate");

        _context.DiscordUsers.Add(DiscordUser.FromDiscord((long)DiscordId, "activehate"));
        _placeholder = User.Seed(userName: "historical:ActiveHate", normalizedUserName: "HISTORICAL:ACTIVEHATE", isDummyUser: true, discordUserID: (long)DiscordId);
        _context.Users.Add(_placeholder);
        await _context.SaveChangesAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        var connectionString = _context.Database.GetConnectionString()!;
        _scope.Dispose();
        await _provider.DisposeAsync();
        _ = Task.Run(() => TestDatabase.DropDatabase(connectionString));
    }

    [Test]
    public async Task APlaceholdersDiscord_GetsAnAccountOfItsOwn_AndThePlaceholderIsMergedIn()
    {
        await _sut.SetGamertag(_interaction, "ActiveHate");

        await using var fresh = _context.NewContextLike();
        var users = await fresh.Users.AsNoTracking().ToListAsync();
        var merge = await fresh.UserMerges.SingleAsync();
        var person = users.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((person.UserName, person.IsDummyUser, person.DiscordUserID), Is.EqualTo(("activehate", false, (long?)DiscordId)));
            Assert.That((merge.Trigger, merge.Reason, merge.SourceUserID, merge.TargetUserID), Is.EqualTo((MergeTrigger.DiscordMatch, "Discord set gamertag", _placeholder.Id, person.Id)));
        }
        await _setGamertag.Received(1).SetGamertag(person.Id, "ActiveHate", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task APlaceholdersDiscord_WithASiteAccountSignedInWithIt_IsMergedIntoThatAccount()
    {
        var site = User.Seed(userName: "site-account");
        await _userManager.CreateAsync(site);
        await _userManager.AddLoginAsync(site, new UserLoginInfo("Discord", DiscordId.ToString(), "Discord"));

        await _sut.SetGamertag(_interaction, "ActiveHate");

        await using var fresh = _context.NewContextLike();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await fresh.Users.Select(u => u.Id).ToListAsync(), Is.EqualTo(new[] { site.Id }));
            Assert.That((await fresh.UserMerges.SingleAsync()).TargetUserID, Is.EqualTo(site.Id));
        }
        await _setGamertag.Received(1).SetGamertag(site.Id, "ActiveHate", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task APlaceholderThatCantBeMerged_KeepsItsDiscord_AndSaysWhy()
    {
        var site = User.Seed(userName: "site-account");
        await _userManager.CreateAsync(site);
        await _userManager.AddLoginAsync(site, new UserLoginInfo("Discord", DiscordId.ToString(), "Discord"));
        var season = Season.Seed(seasonName: "S1").WithDates(seasonStart: DateTime.UtcNow, seasonEnd: DateTime.UtcNow.AddDays(1));
        _context.Seasons.Add(season);
        await _context.SaveChangesAsync();
        var a = Team.Seed(seasonID: season.SeasonID, teamName: "A");
        var b = Team.Seed(seasonID: season.SeasonID, teamName: "B");
        _context.Teams.AddRange(a, b);
        await _context.SaveChangesAsync();
        _context.Set<TeamPlayer>().AddRange(TeamPlayer.Seed(teamID: a.TeamID, userID: site.Id), TeamPlayer.Seed(teamID: b.TeamID, userID: _placeholder.Id));
        await _context.SaveChangesAsync();

        await _sut.SetGamertag(_interaction, "ActiveHate");

        await using var fresh = _context.NewContextLike();
        Assert.That(await fresh.UserMerges.AnyAsync(), Is.False);
        await _setGamertag.DidNotReceiveWithAnyArgs().SetGamertag(default, default!, default);
    }
}
