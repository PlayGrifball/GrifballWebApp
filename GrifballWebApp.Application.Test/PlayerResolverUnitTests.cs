using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// PlayerResolver on its own: a Waypoint row's XUID (its user, or an untied placeholder of its gamertag tied to it), a
/// gamertag history one account or several claim for the season, and an earlier matchmaking season's rows.
/// </summary>
[TestFixture]
internal sealed class PlayerResolverUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime SeasonStart = new(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private PlayerResolver Resolver(int seasonId = 1, int? matchmakingSeasonId = null) => new(Context, seasonId, SeasonStart, matchmakingSeasonId);

    private async Task<User> Placeholder(string gamertag)
    {
        var user = User.ImportPlaceholder(HistoricalKeys.DummyUserName(gamertag), gamertag, null);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task Resolve_AnXuidWithAUser_IsThatUser()
    {
        var user = User.Seed(userName: "noah", xboxUser: XboxUser.FromXbox(1, "Shadow S16"));
        Context.Users.Add(user);
        await Context.SaveChangesAsync();

        var resolution = await Resolver().Resolve("Anything", 1, CancellationToken.None);

        Assert.That(resolution, Is.EqualTo(new Resolution(user.Id, ResolvedBy.Xuid)));
    }

    [Test]
    public async Task Resolve_AnUnheldXuid_TiesTheUntiedPlaceholderOfItsGamertag()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "Shadow S16"));
        var placeholder = await Placeholder("Shadow S16");

        var resolver = Resolver();
        var resolution = await resolver.Resolve("Shadow S16", 1, CancellationToken.None);

        var reread = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == placeholder.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resolution, Is.EqualTo(new Resolution(placeholder.Id, ResolvedBy.Placeholder)));
            Assert.That(reread.XboxUserID, Is.EqualTo(1L), "Tied to the account and saved");
            Assert.That(resolver.DummiesCreated, Is.Zero);
        }
    }

    [Test]
    public async Task Resolve_AGamertagTwoAccountsHistoriesHaveForTheSeason_IsAmbiguous_AndGetsItsOwnPlaceholder()
    {
        Context.XboxUsers.AddRange(XboxUser.FromXbox(1, "First Now"), XboxUser.FromXbox(2, "Second Now"));
        Context.Set<XboxGamertagHistory>().AddRange(new XboxGamertagHistory(1, "Shared Tag"), new XboxGamertagHistory(2, "Shared Tag"));
        await Context.SaveChangesAsync();

        var resolver = Resolver();
        var resolution = await resolver.Resolve("Shared Tag", null, CancellationToken.None);

        var placeholder = await Context.Users.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resolver.Ambiguous, Is.EqualTo(SharedTagOnly));
            Assert.That(resolution, Is.EqualTo(new Resolution(placeholder.Id, ResolvedBy.NewPlaceholder)));
            Assert.That(placeholder.XboxUserID, Is.Null, "Neither account's");
            Assert.That(resolver.DummiesCreated, Is.EqualTo(1));
        }
    }

    private static readonly string[] SharedTagOnly = ["Shared Tag"];

    [Test]
    public async Task Resolve_AnUnheldAccountsHistory_TiesTheGamertagsUntiedPlaceholder()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "New Name"));
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(1, "Old Name"));
        var placeholder = await Placeholder("Old Name");

        var resolution = await Resolver().Resolve("Old Name", null, CancellationToken.None);

        var reread = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == placeholder.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resolution, Is.EqualTo(new Resolution(placeholder.Id, ResolvedBy.GamertagHistory)));
            Assert.That(reread.XboxUserID, Is.EqualTo(1L));
        }
    }

    [Test]
    public async Task Resolve_AnUnheldAccountsHistory_WithoutAPlaceholder_MakesOneTiedToTheAccount()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "New Name"));
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(1, "Old Name", lastSeen: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await Context.SaveChangesAsync();

        var resolver = Resolver();
        var resolution = await resolver.Resolve("Old Name", null, CancellationToken.None);

        var made = await Context.Users.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resolution, Is.EqualTo(new Resolution(made.Id, ResolvedBy.GamertagHistory)));
            Assert.That((made.UserName, made.XboxUserID), Is.EqualTo(("historical:Old Name", (long?)1)));
            Assert.That(resolver.DummiesCreated, Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Resolve_AnotherMatchmakingSeasonsRow_IsTheSamePlayer_AndIsCached()
    {
        var player = User.Seed(userName: "late");
        var earlier = MatchmakingSeason.Seed("Late League Season 1", new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2016, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Late Player" }, gameVersionID: 6, matchmakingSeason: earlier, user: player));
        await Context.SaveChangesAsync();
        var current = MatchmakingSeason.Seed("Late League Season 2", SeasonStart, SeasonStart.AddMonths(2));
        Context.MatchmakingSeasons.Add(current);
        await Context.SaveChangesAsync();

        var resolver = Resolver(seasonId: 0, matchmakingSeasonId: current.MatchmakingSeasonID);
        var first = await resolver.Resolve("Late Player", null, CancellationToken.None);
        Context.MatchmakingPlayerStats.RemoveRange(await Context.MatchmakingPlayerStats.ToListAsync());
        await Context.SaveChangesAsync();
        var second = await resolver.Resolve("late player", null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(new Resolution(player.Id, ResolvedBy.EarlierRows)));
            Assert.That(second, Is.EqualTo(first), "The same gamertag is resolved once a run");
            Assert.That(resolver.DummiesCreated, Is.Zero);
        }
    }

    [Test]
    public async Task Resolve_ThisMatchmakingSeasonsRow_IsThatPlayer()
    {
        var player = User.Seed(userName: "late");
        var current = MatchmakingSeason.Seed("Late League Season 2", SeasonStart, SeasonStart.AddMonths(2));
        Context.MatchmakingPlayerStats.Add(MatchmakingPlayerStat.Seed(new StatSheetTotals { Gamertag = "Late Player" }, gameVersionID: 6, matchmakingSeason: current, user: player));
        await Context.SaveChangesAsync();

        var resolution = await Resolver(seasonId: 0, matchmakingSeasonId: current.MatchmakingSeasonID).Resolve("Late Player", null, CancellationToken.None);

        Assert.That(resolution, Is.EqualTo(new Resolution(player.Id, ResolvedBy.EarlierRows)));
    }
}
