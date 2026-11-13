using GrifballWebApp.Application.Historical;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// A user's claims of their own past gamertags: the preview (what the claim would merge, or why it can't be made), the
/// submission (its refusals, the daily limit, the pending claim it makes) and the user's list. The refusals' Xbox-account
/// checks compare gamertags with their spaces taken out in SQL (REPLACE, which on SQLite loses the column's NOCASE
/// collation), so the seeded gamertags here are spelt in the case the claim gives.
/// </summary>
[TestFixture]
internal sealed class UserGamertagClaimServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private FakeTimeProvider _time = null!;
    private UserGamertagClaimService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = Service(5);
    }

    private UserGamertagClaimService Service(int submissionsPerDay)
        => new(Context, new GamertagMatching(Context), _time, Options.Create(new UserClaimOptions { SubmissionsPerDay = submissionsPerDay }));

    /// <summary>The signed-up user (ID 1) and the untied placeholder of "OldName".</summary>
    private async Task<(User Claimant, User Placeholder)> ClaimantAndPlaceholder()
    {
        var claimant = User.Seed(userName: "claimant", displayName: "Claimant", id: 1);
        Context.Users.Add(claimant);
        await Context.SaveChangesAsync();
        var placeholder = User.ImportPlaceholder("historical:OldName", "OldName", null);
        Context.Users.Add(placeholder);
        await Context.SaveChangesAsync();
        return (claimant, placeholder);
    }

    // ---------- Preview ----------

    [Test]
    public async Task Preview_NoGamertag_IsRefused()
    {
        var preview = await _service.Preview(1, "   ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Gamertag, Is.Empty);
            Assert.That(preview.Refusal, Is.EqualTo("Give the gamertag"));
            Assert.That(preview.PlaceholderUserID, Is.Null);
        }
    }

    [Test]
    public async Task Preview_APlaceholdersGamertag_ShowsItAndItsSeasons()
    {
        var (_, placeholder) = await ClaimantAndPlaceholder();
        var season = Season.Seed(seasonName: "Winter League 2019").WithDates(
            seasonStart: new DateTime(2019, 1, 5, 0, 0, 0, DateTimeKind.Utc), seasonEnd: new DateTime(2019, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        Context.PlayerSeasonStats.Add(PlayerSeasonStat.Seed(new StatSheetTotals { Gamertag = "OldName" }, seasonID: season.SeasonID, userID: placeholder.Id, gameVersionID: 6));
        await Context.SaveChangesAsync();

        var preview = await _service.Preview(1, " OldName ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Gamertag, Is.EqualTo("OldName"));
            Assert.That(preview.PlaceholderUserID, Is.EqualTo(placeholder.Id));
            Assert.That(preview.PlaceholderName, Is.EqualTo("OldName"));
            Assert.That(preview.Seasons.Select(s => (s.SeasonID, s.SeasonName, s.Year)), Is.EqualTo(new[] { (season.SeasonID, "Winter League 2019", 2019) }));
            Assert.That(preview.Refusal, Is.Null);
        }
    }

    [Test]
    public async Task Preview_NoPlaceholder_SaysThereIsNothingToClaim()
    {
        await ClaimantAndPlaceholder();

        var preview = await _service.Preview(1, "NeverPlayed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.PlaceholderUserID, Is.Null);
            Assert.That(preview.Seasons, Is.Empty);
            Assert.That(preview.Refusal, Is.EqualTo("No past records use that gamertag"));
        }
    }

    [Test]
    public async Task Preview_ByAPlaceholderOrNobody_IsRefused()
    {
        var (_, placeholder) = await ClaimantAndPlaceholder();

        var byPlaceholder = await _service.Preview(placeholder.Id, "OldName");
        var byNobody = await _service.Preview(999, "OldName");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(byPlaceholder.Refusal, Is.EqualTo("Only a signed-up user can claim a gamertag"));
            Assert.That(byNobody.Refusal, Is.EqualTo("Only a signed-up user can claim a gamertag"));
        }
    }

    [Test]
    public async Task Preview_AGamertagAnotherUsersAccountHasNow_IsRefused()
    {
        await ClaimantAndPlaceholder();
        Context.Users.Add(User.Seed(userName: "other", xboxUser: XboxUser.FromXbox(9, "OldName")));
        await Context.SaveChangesAsync();

        var preview = await _service.Preview(1, "OldName");

        Assert.That(preview.Refusal, Does.StartWith("That gamertag belongs to another user's Xbox account"));
    }

    [Test]
    public async Task Preview_AGamertagTheClaimantsOwnAccountHas_IsNotRefusedForIt()
    {
        var xbox = XboxUser.FromXbox(9, "OldName");
        var claimant = User.Seed(userName: "claimant", xboxUser: xbox, id: 1);
        Context.Users.Add(claimant);
        await Context.SaveChangesAsync();
        Context.Users.Add(User.ImportPlaceholder("historical:OldName", "OldName", null));
        await Context.SaveChangesAsync();

        var preview = await _service.Preview(1, "OldName");

        Assert.That(preview.Refusal, Is.Null);
    }

    [Test]
    public async Task Preview_AGamertagAnotherUsersAccountHad_IsRefused()
    {
        await ClaimantAndPlaceholder();
        Context.Users.Add(User.Seed(userName: "other", xboxUser: XboxUser.FromXbox(9, "NewName")));
        Context.Set<XboxGamertagHistory>().Add(new XboxGamertagHistory(9, "OldName"));
        await Context.SaveChangesAsync();

        var preview = await _service.Preview(1, "OldName");

        Assert.That(preview.Refusal, Does.StartWith("That gamertag was another user's Xbox account's"));
    }

    // ---------- Submit ----------

    [Test]
    public async Task Submit_NoGamertag_Throws()
    {
        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(1, new SubmitGamertagClaimRequest { Gamertag = " " }));

        Assert.That(ex!.Message, Is.EqualTo("Give the gamertag"));
    }

    [Test]
    public async Task Submit_APeriodThatEndsBeforeItStarts_Throws()
    {
        var request = new SubmitGamertagClaimRequest
        {
            Gamertag = "OldName",
            FirstSeen = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(1, request));

        Assert.That(ex!.Message, Is.EqualTo("The start of the period is after its end"));
    }

    [Test]
    public async Task Submit_PastTheDailyLimit_Throws()
    {
        var (claimant, placeholder) = await ClaimantAndPlaceholder();
        var earlier = GamertagClaim.ClaimedByUser(claimant.Id, null, "Other Tag", "OTHER TAG", placeholder.Id, null, null);
        Context.GamertagClaims.Add(earlier);
        Context.Entry(earlier).Property(c => c.CreatedAt).CurrentValue = Now.AddHours(-2);
        await Context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => Service(1).Submit(1, new SubmitGamertagClaimRequest { Gamertag = "OldName" }));

        Assert.That(ex!.Message, Is.EqualTo("You can make 1 claims a day; try again tomorrow"));
    }

    [Test]
    public async Task Submit_AClaimFromMoreThanADayAgo_DoesNotCountTowardsTheLimit()
    {
        var (claimant, placeholder) = await ClaimantAndPlaceholder();
        var earlier = GamertagClaim.ClaimedByUser(claimant.Id, null, "Other Tag", "OTHER TAG", placeholder.Id, null, null);
        Context.GamertagClaims.Add(earlier);
        Context.Entry(earlier).Property(c => c.CreatedAt).CurrentValue = Now.AddDays(-2);
        await Context.SaveChangesAsync();

        var claim = await Service(1).Submit(1, new SubmitGamertagClaimRequest { Gamertag = "OldName" });

        Assert.That(claim.Gamertag, Is.EqualTo("OldName"));
    }

    [Test]
    public async Task Submit_MakesAPendingUserClaim_OfThePlaceholder()
    {
        var (_, placeholder) = await ClaimantAndPlaceholder();
        var request = new SubmitGamertagClaimRequest
        {
            Gamertag = " OldName ",
            FirstSeen = new DateTime(2018, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastSeen = new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var dto = await _service.Submit(1, request);

        var row = await Context.GamertagClaims.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto.GamertagClaimID, Is.EqualTo(row.GamertagClaimID));
            Assert.That(dto.Kind, Is.EqualTo(nameof(GamertagClaimKind.UserClaim)));
            Assert.That(dto.Status, Is.EqualTo(nameof(GamertagClaimStatus.Pending)));
            Assert.That(dto.ClaimantName, Is.EqualTo("Claimant"));
            Assert.That(row.ClaimantUserID, Is.EqualTo(1));
            Assert.That(row.Gamertag, Is.EqualTo("OldName"));
            Assert.That(row.NormalizedGamertag, Is.EqualTo("OLDNAME"));
            Assert.That(row.PlaceholderUserID, Is.EqualTo(placeholder.Id));
            Assert.That((row.FirstSeen, row.LastSeen), Is.EqualTo((request.FirstSeen, request.LastSeen)));
            Assert.That(row.CreatedAt, Is.EqualTo(Now), "Stamped by the service's clock, so the daily limit counts it");
        }
    }

    [Test]
    public async Task Submit_TheSameGamertagAgain_IsRefused()
    {
        await ClaimantAndPlaceholder();
        await _service.Submit(1, new SubmitGamertagClaimRequest { Gamertag = "OldName" });

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(1, new SubmitGamertagClaimRequest { Gamertag = "oldname" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("You have claimed that gamertag already"));
            Assert.That(await Context.GamertagClaims.CountAsync(), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task Submit_NoPlaceholder_IsRefused()
    {
        await ClaimantAndPlaceholder();

        var ex = await Assert.ThrowsAsync<GamertagClaimException>(() => _service.Submit(1, new SubmitGamertagClaimRequest { Gamertag = "NeverPlayed" }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Is.EqualTo("No past records use that gamertag"));
            Assert.That(await Context.GamertagClaims.CountAsync(), Is.Zero);
        }
    }

    // ---------- ClaimsOf ----------

    [Test]
    public async Task ClaimsOf_OnlyTheUsersOwnClaims_NewestFirst()
    {
        var (claimant, placeholder) = await ClaimantAndPlaceholder();
        var someone = User.Seed(userName: "someone");
        Context.Users.Add(someone);
        await Context.SaveChangesAsync();
        var older = GamertagClaim.ClaimedByUser(claimant.Id, null, "First Tag", "FIRST TAG", placeholder.Id, null, null);
        var newer = GamertagClaim.ClaimedByUser(claimant.Id, null, "Second Tag", "SECOND TAG", placeholder.Id, null, null);
        var someoneElses = GamertagClaim.ClaimedByUser(someone.Id, null, "Third Tag", "THIRD TAG", placeholder.Id, null, null);
        Context.GamertagClaims.Add(older);
        await Context.SaveChangesAsync();
        Context.GamertagClaims.AddRange(newer, someoneElses);
        await Context.SaveChangesAsync();

        var claims = await _service.ClaimsOf(claimant.Id);

        Assert.That(claims.Select(c => c.Gamertag), Is.EqualTo(SecondThenFirst));
    }

    private static readonly string[] SecondThenFirst = ["Second Tag", "First Tag"];
}
