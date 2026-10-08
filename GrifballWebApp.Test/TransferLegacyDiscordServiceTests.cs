using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Availability;
using GrifballWebApp.Server.EventOrganizer;
using GrifballWebApp.Server.Profile;
using GrifballWebApp.Server.Seasons;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using AvailabilityTimeslotDto = GrifballWebApp.Server.Availability.TimeslotDto;

namespace GrifballWebApp.Test;

[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public class TransferLegacyDiscordServiceTests
{
    private GrifballContext _context;
    private const string NameClaim = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    [SetUp]
    public async Task Setup() => _context = await SetUpFixture.NewGrifballContext();

    [TearDown]
    public async Task TearDown() => await _context.DropDatabaseAndDispose();

    private async Task<User> UserWithLogin(string name, string provider, string key, string? claimName)
    {
        var user = new User { UserName = name };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        _context.UserLogins.Add(new UserLogin { UserId = user.Id, LoginProvider = provider, ProviderKey = key, ProviderDisplayName = provider });
        if (claimName is not null)
            _context.UserClaims.Add(new UserClaim { UserId = user.Id, ClaimType = NameClaim, ClaimValue = claimName });
        await _context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task TransferAllAsync_CreatesDiscordUsersAndLinks()
    {
        var withClaim = await UserWithLogin("a", "Discord", "1001", "discordA");
        var noClaim = await UserWithLogin("b", "Discord", "1002", null);
        var google = await UserWithLogin("c", "Google", "1003", "googleC");
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 1004, DiscordUsername = "existing" });
        await _context.SaveChangesAsync();
        var existingDiscord = await UserWithLogin("d", "Discord", "1004", null);

        await new TransferLegacyDiscordService(_context).TransferAllAsync();

        _context.ChangeTracker.Clear();
        var users = await _context.Users.ToDictionaryAsync(u => u.UserName!);
        Assert.Multiple(() =>
        {
            Assert.That(users["a"].DiscordUserID, Is.EqualTo(1001));
            Assert.That(users["b"].DiscordUserID, Is.Null, "no name claim -> skipped");
            Assert.That(users["c"].DiscordUserID, Is.Null, "non-Discord provider ignored");
            Assert.That(users["d"].DiscordUserID, Is.EqualTo(1004), "existing discord user linked");
        });
        var discordUsers = await _context.DiscordUsers.ToDictionaryAsync(d => d.DiscordUserID);
        Assert.That(discordUsers[1001].DiscordUsername, Is.EqualTo("discordA"));
        Assert.That(discordUsers[1004].DiscordUsername, Is.EqualTo("existing"));
        Assert.That(discordUsers.ContainsKey(1002), Is.False);
    }

    [Test]
    public async Task TransferAllAsync_AlreadyLinkedUsersAreSkipped()
    {
        _context.DiscordUsers.Add(new DiscordUser { DiscordUserID = 2001, DiscordUsername = "orig" });
        await _context.SaveChangesAsync();
        var user = await UserWithLogin("linked", "Discord", "2002", "other");
        user.DiscordUserID = 2001;
        await _context.SaveChangesAsync();

        await new TransferLegacyDiscordService(_context).TransferAllAsync();

        _context.ChangeTracker.Clear();
        Assert.That((await _context.Users.SingleAsync(u => u.Id == user.Id)).DiscordUserID, Is.EqualTo(2001));
        Assert.That(await _context.DiscordUsers.AnyAsync(d => d.DiscordUserID == 2002), Is.False);
    }
}
