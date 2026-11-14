using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// GamertagMatching against the fake SQLite context: placeholders found by name (and by a "#1234" suffix either way),
/// made once (one of its own for an Xbox account another placeholder's name is taken by), made for orphan Xbox accounts,
/// fitted to an account's history, claims made once, and the signed-up user a gamertag is.
/// </summary>
[TestFixture]
internal sealed class GamertagMatchingUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime LookedUpAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private GamertagMatching _matching = null!;

    [SetUp]
    public void SetUp()
    {
        _matching = new GamertagMatching(Context);
    }

    private async Task<User> Placeholder(string gamertag, long? xboxUserID = null)
    {
        var user = User.ImportPlaceholder(HistoricalKeys.DummyUserName(gamertag), gamertag, xboxUserID);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        return user;
    }

    private async Task<XboxUser> Xbox(long xuid, string gamertag)
    {
        var xbox = XboxUser.FromXbox(xuid, gamertag);
        Context.XboxUsers.Add(xbox);
        await Context.SaveChangesAsync();
        return xbox;
    }

    // ---------- PlaceholderAsync ----------

    [Test]
    public async Task PlaceholderAsync_FindsTheGamertagsOwn()
    {
        var own = await Placeholder("OldName");

        var found = await _matching.PlaceholderAsync("OldName", untiedOnly: false, CancellationToken.None);

        Assert.That(found?.Id, Is.EqualTo(own.Id));
    }

    [Test]
    public async Task PlaceholderAsync_UntiedOnly_SkipsOneTiedToAnAccount()
    {
        await Xbox(1, "OldName");
        await Placeholder("OldName", xboxUserID: 1);

        var untied = await _matching.PlaceholderAsync("OldName", untiedOnly: true, CancellationToken.None);
        var any = await _matching.PlaceholderAsync("OldName", untiedOnly: false, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(untied, Is.Null);
            Assert.That(any?.XboxUserID, Is.EqualTo(1L));
        }
    }

    [Test]
    public async Task PlaceholderAsync_ASuffixedGamertag_FindsTheUntiedPlaceholderOfItsName()
    {
        var plain = await Placeholder("Name");

        var found = await _matching.PlaceholderAsync("Name#1234", untiedOnly: false, CancellationToken.None);

        Assert.That(found?.Id, Is.EqualTo(plain.Id));
    }

    [Test]
    public async Task PlaceholderAsync_ASuffixedGamertag_NeverTakesATiedPlaceholderOfItsName()
    {
        await Xbox(1, "Name");
        await Placeholder("Name", xboxUserID: 1);

        var found = await _matching.PlaceholderAsync("Name#1234", untiedOnly: false, CancellationToken.None);

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task PlaceholderAsync_APlainGamertag_FindsTheOnlySuffixedPlaceholder()
    {
        var suffixed = await Placeholder("Name#1234");

        var found = await _matching.PlaceholderAsync("Name", untiedOnly: false, CancellationToken.None);

        Assert.That(found?.Id, Is.EqualTo(suffixed.Id));
    }

    [Test]
    public async Task PlaceholderAsync_APlainGamertag_WithTwoSuffixedPlaceholders_FindsNone()
    {
        await Placeholder("Name#1234");
        await Placeholder("Name#5678");

        var found = await _matching.PlaceholderAsync("Name", untiedOnly: false, CancellationToken.None);

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task PlaceholderAsync_APlainGamertag_IgnoresANameWhoseRestIsNotASuffix()
    {
        await Placeholder("Name#12ab");

        var found = await _matching.PlaceholderAsync("Name", untiedOnly: false, CancellationToken.None);

        Assert.That(found, Is.Null);
    }

    // ---------- CreatePlaceholderAsync ----------

    [Test]
    public async Task CreatePlaceholderAsync_NoneYet_MakesOne_TiedToAnUnheldAccount()
    {
        var xbox = await Xbox(1, "Fresh");

        var (user, created) = await _matching.CreatePlaceholderAsync(" Fresh ", xbox, CancellationToken.None);

        var reread = await Context.Users.AsNoTracking().SingleAsync(u => u.Id == user.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.True);
            Assert.That(reread.UserName, Is.EqualTo("historical:Fresh"));
            Assert.That(reread.DisplayName, Is.EqualTo("Fresh"));
            Assert.That(reread.IsDummyUser, Is.True);
            Assert.That(reread.XboxUserID, Is.EqualTo(1L));
        }
    }

    [Test]
    public async Task CreatePlaceholderAsync_AnAccountSomeoneHolds_IsNotTied()
    {
        var xbox = await Xbox(1, "Fresh");
        Context.Users.Add(User.Seed(userName: "holder", xboxUserID: 1));
        await Context.SaveChangesAsync();

        var (user, created) = await _matching.CreatePlaceholderAsync("Fresh", xbox, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.True);
            Assert.That(user.XboxUserID, Is.Null);
        }
    }

    [Test]
    public async Task CreatePlaceholderAsync_ExistingOne_WithoutAnAccount_IsReturned()
    {
        var existing = await Placeholder("OldName");

        var (user, created) = await _matching.CreatePlaceholderAsync("OldName", null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.False);
            Assert.That(user.Id, Is.EqualTo(existing.Id));
            Assert.That(await Context.Users.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task CreatePlaceholderAsync_ExistingOne_OfTheSameAccount_IsReturned()
    {
        var xbox = await Xbox(1, "OldName");
        var existing = await Placeholder("OldName", xboxUserID: 1);

        var (user, created) = await _matching.CreatePlaceholderAsync("OldName", xbox, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.False);
            Assert.That(user.Id, Is.EqualTo(existing.Id));
        }
    }

    [Test]
    public async Task CreatePlaceholderAsync_TheNameIsAnotherAccounts_MakesOneNamedAfterTheXuid_OnceOnly()
    {
        await Xbox(1, "OldName");
        var other = await Xbox(2, "Renamed");
        await Placeholder("OldName", xboxUserID: 1);

        var (first, firstCreated) = await _matching.CreatePlaceholderAsync("OldName", other, CancellationToken.None);
        var (second, secondCreated) = await _matching.CreatePlaceholderAsync("OldName", other, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(firstCreated, Is.True);
            Assert.That(first.UserName, Is.EqualTo("historical:OldName:2"));
            Assert.That(first.XboxUserID, Is.EqualTo(2L));
            Assert.That(secondCreated, Is.False, "Its own placeholder is found again");
            Assert.That(second.Id, Is.EqualTo(first.Id));
        }
    }

    [Test]
    public async Task CreatePlaceholderAsync_FoundByItsSuffixlessName_ForAnotherAccount_MakesTheSuffixedOne()
    {
        var plain = await Placeholder("Name");
        var xbox = await Xbox(5, "Name#1234");

        var (user, created) = await _matching.CreatePlaceholderAsync("Name#1234", xbox, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(created, Is.True);
            Assert.That(user.Id, Is.Not.EqualTo(plain.Id));
            Assert.That(user.UserName, Is.EqualTo("historical:Name#1234"));
            Assert.That(user.XboxUserID, Is.EqualTo(5L));
        }
    }

    // ---------- EnsureXboxPlaceholdersAsync ----------

    /// <summary>The Xbox accounts (saved already) in a game: reported for a season match of its own (a league game), or not (a pickup).</summary>
    private async Task InAGame(bool league, params long[] xuids)
    {
        var match = GrifballWebApp.Domain.Halo.Match.Seed(Guid.NewGuid(), startTime: DateTime.UtcNow);
        match.AddTeam(0, 5, GrifballWebApp.Domain.Halo.Outcomes.Won);
        foreach (var xuid in xuids)
            match.AddParticipant(0, xuid, new GrifballWebApp.Domain.Halo.MatchParticipantStats());
        Context.Matches.Add(match);
        if (league)
        {
            var season = Season.Seed(seasonName: $"League {Guid.NewGuid():N}");
            Context.Seasons.Add(season);
            await Context.SaveChangesAsync();
            var home = Team.Seed(seasonID: season.SeasonID, teamName: "Home");
            var away = Team.Seed(seasonID: season.SeasonID, teamName: "Away");
            Context.Teams.AddRange(home, away);
            await Context.SaveChangesAsync();
            var seasonMatch = SeasonMatch.Seed(season.SeasonID, home.TeamID, away.TeamID);
            Context.SeasonMatches.Add(seasonMatch);
            seasonMatch.AddGame(match, 1);
        }
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task EnsureXboxPlaceholdersAsync_NoXuids_MakesNothing()
    {
        await Xbox(1, "Orphan");

        var made = await _matching.EnsureXboxPlaceholdersAsync(Array.Empty<long>(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.Zero);
            Assert.That(await Context.Users.CountAsync(), Is.Zero);
        }
    }

    [Test]
    public async Task EnsureXboxPlaceholdersAsync_GivenXuids_OnlyThoseOrphans()
    {
        await Xbox(1, "Orphan One");
        await Xbox(2, "Orphan Two");
        await InAGame(league: true, 1, 2);

        var made = await _matching.EnsureXboxPlaceholdersAsync(XuidOne, CancellationToken.None);

        var users = await Context.Users.AsNoTracking().ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.EqualTo(1));
            Assert.That(users.Select(u => (u.UserName, u.XboxUserID)), Is.EqualTo(OrphanOneOnly));
        }
    }

    private static readonly (string?, long?)[] OrphanOneOnly = [("historical:Orphan One", 1L)];
    private static readonly long[] XuidOne = [1L];

    [Test]
    public async Task EnsureXboxPlaceholdersAsync_AllOrphans_SkipsHeldAccounts_AndIsIdempotent()
    {
        await Xbox(1, "Orphan One");
        await Xbox(2, "Held");
        Context.Users.Add(User.Seed(userName: "holder", xboxUserID: 2));
        await Context.SaveChangesAsync();
        await InAGame(league: true, 1, 2);

        var made = await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);
        var again = await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.EqualTo(1));
            Assert.That(again, Is.Zero);
            Assert.That(await Context.Users.CountAsync(u => u.IsDummyUser), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task EnsureXboxPlaceholdersAsync_AnAccountOnlyInPickupGames_GetsNone()
    {
        // No league player: their account and games are kept, with no user
        await Xbox(1, "Pickup Only");
        await InAGame(league: false, 1);

        var made = await _matching.EnsureXboxPlaceholdersAsync(null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(made, Is.Zero);
            Assert.That(await Context.Users.AnyAsync(), Is.False);
        }
    }

    // ---------- FitsTheHistoryAsync ----------

    /// <summary>A placeholder with a stat row in a season starting 2019-03-01.</summary>
    private async Task<User> PlaceholderWhoPlayedIn2019()
    {
        var placeholder = await Placeholder("OldName");
        var season = Season.Seed(seasonName: "Spring 2019").WithDates(
            seasonStart: new DateTime(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "OldName" }, seasonID: season.SeasonID, userID: placeholder.Id, gameVersionID: 6));
        await Context.SaveChangesAsync();
        return placeholder;
    }

    [Test]
    public async Task FitsTheHistoryAsync_TheAccountHeldItWhenTheSeasonBegan_Fits()
    {
        var placeholder = await PlaceholderWhoPlayedIn2019();
        await Xbox(1, "NewName");
        var past = new XboxGamertagHistory(1, "OldName", lastSeen: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(await _matching.FitsTheHistoryAsync(placeholder.Id, past, CancellationToken.None), Is.True);
    }

    [Test]
    public async Task FitsTheHistoryAsync_TheAccountLetGoBeforeTheSeason_DoesNotFit()
    {
        var placeholder = await PlaceholderWhoPlayedIn2019();
        var past = new XboxGamertagHistory(1, "OldName", lastSeen: new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(await _matching.FitsTheHistoryAsync(placeholder.Id, past, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task FitsTheHistoryAsync_AnotherAccountHadItThen_DoesNotFit()
    {
        var placeholder = await PlaceholderWhoPlayedIn2019();
        await Xbox(1, "NewName");
        await Xbox(2, "Other Now");
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(2, "OldName", lastSeen: new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await Context.SaveChangesAsync();
        var past = new XboxGamertagHistory(1, "OldName");

        Assert.That(await _matching.FitsTheHistoryAsync(placeholder.Id, past, CancellationToken.None), Is.False);
    }

    [Test]
    public async Task FitsTheHistoryAsync_NoRows_OnlyAnotherAccountsHistoryDecides()
    {
        var placeholder = await Placeholder("OldName");
        await Xbox(1, "NewName");
        var past = new XboxGamertagHistory(1, "OldName");

        var alone = await _matching.FitsTheHistoryAsync(placeholder.Id, past, CancellationToken.None);
        await Xbox(2, "Other Now");
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(2, "OldName", lastSeen: new DateTime(2010, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        await Context.SaveChangesAsync();
        var shared = await _matching.FitsTheHistoryAsync(placeholder.Id, past, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(alone, Is.True);
            Assert.That(shared, Is.False, "No row dates the placeholder: any other account's history with the gamertag stops it");
        }
    }

    // ---------- ClaimAsync / ClaimForUserAsync ----------

    [Test]
    public async Task ClaimAsync_AnXuidLookupWithoutWhatXboxFound_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _matching.ClaimAsync(GamertagClaimKind.XuidLookup, 1, "OldName", 100, null, null, evenIfRejected: false, CancellationToken.None));
    }

    [Test]
    public async Task ClaimAsync_AnXuidLookup_SaysWhatXboxFound()
    {
        var claim = await _matching.ClaimAsync(GamertagClaimKind.XuidLookup, 1, " Old  Name ", 100, null, null, evenIfRejected: false, CancellationToken.None,
            lookedUp: ("NewName", LookedUpAt));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(claim?.Kind, Is.EqualTo(GamertagClaimKind.XuidLookup));
            Assert.That(claim?.Gamertag, Is.EqualTo("Old Name"));
            Assert.That(claim?.NormalizedGamertag, Is.EqualTo("OLD NAME"));
            Assert.That(claim?.CurrentGamertag, Is.EqualTo("NewName"));
            Assert.That(claim?.LookedUpAt, Is.EqualTo(LookedUpAt));
            Assert.That(Context.GamertagClaims.Local, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task ClaimAsync_TheSameOneAddedAndNotSaved_IsNotMadeAgain()
    {
        var first = await _matching.ClaimAsync(GamertagClaimKind.RosterMatch, 1, "OldName", 100, 3, 4, evenIfRejected: false, CancellationToken.None);
        var second = await _matching.ClaimAsync(GamertagClaimKind.RosterMatch, 1, "oldname", 100, 3, 4, evenIfRejected: false, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first?.SeasonID, Is.EqualTo(3));
            Assert.That(first?.TeamID, Is.EqualTo(4));
            Assert.That(second, Is.Null);
            Assert.That(Context.GamertagClaims.Local, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task ClaimAsync_ARejectedOne_StopsItUnlessEvenIfRejected()
    {
        await Xbox(1, "NewName");
        var placeholder = await Placeholder("OldName");
        var rejected = GamertagClaim.Suggest(GamertagClaimKind.RosterMatch, 1, "OldName", "OLDNAME", placeholder.Id);
        rejected.Reject("no", null, LookedUpAt);
        Context.GamertagClaims.Add(rejected);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var stopped = await _matching.ClaimAsync(GamertagClaimKind.RosterMatch, 1, "OldName", placeholder.Id, null, null, evenIfRejected: false, CancellationToken.None);
        var again = await _matching.ClaimAsync(GamertagClaimKind.RosterMatch, 1, "OldName", placeholder.Id, null, null, evenIfRejected: true, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stopped, Is.Null);
            Assert.That(again?.Status, Is.EqualTo(GamertagClaimStatus.Pending));
        }
    }

    [Test]
    public async Task ClaimForUserAsync_AnyKindButADiscordMatch_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _matching.ClaimForUserAsync(GamertagClaimKind.RosterMatch, 5, null, "OldName", 100, evenIfRejected: false, CancellationToken.None));
    }

    [Test]
    public async Task ClaimForUserAsync_ADiscordMatch_IsMadeOnce()
    {
        var first = await _matching.ClaimForUserAsync(GamertagClaimKind.DiscordMatch, 5, 7, " OldName ", 100, evenIfRejected: false, CancellationToken.None);
        var second = await _matching.ClaimForUserAsync(GamertagClaimKind.DiscordMatch, 5, 7, "OLDNAME", 100, evenIfRejected: false, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first?.Kind, Is.EqualTo(GamertagClaimKind.DiscordMatch));
            Assert.That(first?.ClaimantUserID, Is.EqualTo(5));
            Assert.That(first?.XboxUserID, Is.EqualTo(7L));
            Assert.That(first?.Gamertag, Is.EqualTo("OldName"));
            Assert.That(second, Is.Null);
        }
    }

    // ---------- RealUserOfAsync ----------

    [Test]
    public async Task RealUserOfAsync_TheOneWhoseAccountHasIt_ComparedLoosely()
    {
        var holder = User.Seed(userName: "noah", xboxUser: XboxUser.FromXbox(1, "Shadow_S16#1234"));
        Context.Users.Add(holder);
        await Context.SaveChangesAsync();

        var found = await _matching.RealUserOfAsync("shadow s16", CancellationToken.None);

        Assert.That(found?.Id, Is.EqualTo(holder.Id));
    }

    [Test]
    public async Task RealUserOfAsync_TwoAccountsHaveIt_IsNobody()
    {
        Context.Users.AddRange(
            User.Seed(userName: "a", xboxUser: XboxUser.FromXbox(1, "Shadow S16")),
            User.Seed(userName: "b", xboxUser: XboxUser.FromXbox(2, "Shadow_S16")));
        await Context.SaveChangesAsync();

        Assert.That(await _matching.RealUserOfAsync("Shadow S16", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task RealUserOfAsync_OnlyAnAccountsHistoryHasIt_IsThatAccountsUser()
    {
        var holder = User.Seed(userName: "pie", xboxUser: XboxUser.FromXbox(2, "Pie Now"));
        Context.Users.Add(holder);
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(2, "x0 pie x0"));
        await Context.SaveChangesAsync();

        var found = await _matching.RealUserOfAsync("X0 PIE X0", CancellationToken.None);

        Assert.That(found?.Id, Is.EqualTo(holder.Id));
    }

    [Test]
    public async Task RealUserOfAsync_NoAccountHasOrHadIt_IsNobody()
    {
        Context.Users.Add(User.Seed(userName: "pie", xboxUser: XboxUser.FromXbox(2, "Pie Now")));
        await Placeholder("Ghost");

        Assert.That(await _matching.RealUserOfAsync("Ghost", CancellationToken.None), Is.Null);
    }
}
