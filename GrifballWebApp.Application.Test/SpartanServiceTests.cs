using GrifballWebApp.Application.Spartan;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>A user's Spartan on their profile: their own choice (kept only when the render service's catalog allows it), the render it
/// asks for, and the files of that render; and the Spartan headshot the profile picture reads from those files.</summary>
[TestFixture]
internal sealed class SpartanServiceTests : ApplicationDbContextTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly SpartanCatalog Catalog = new(
        [new SpartanCatalogCore("Mark VII", [new SpartanCatalogRegion("REGION", "helmet", 100, [new(1, "Mark VII"), new(2, "Eaglestrike")])])],
        ["111"], ["Plain"], []);

    private sealed class FakeCatalogs(SpartanCatalog? catalog) : ISpartanCatalogSource
    {
        public Task<SpartanCatalog?> Get(CancellationToken ct = default) => Task.FromResult(catalog);
    }

    private readonly FakeTimeProvider _time = new(Start);
    private int _userId;

    private SpartanService Service(SpartanCatalog? catalog = null, bool catalogUp = true)
        => new(Context, new FakeCatalogs(catalogUp ? catalog ?? Catalog : null), _time);

    [SetUp]
    public async Task SeedUser()
    {
        var xbox = XboxUser.FromXbox(500, "Grifter");
        Context.XboxUsers.Add(xbox);
        var user = User.Seed(userName: "grif", xboxUser: xbox);
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        _userId = user.Id;
    }

    private static SpartanChoice Mk7(string piece = "Eaglestrike")
        => new("Mark VII", new Dictionary<string, string> { ["helmet"] = piece }, null, "111", "Plain");

    [Test]
    public async Task Get_IsNullForNoSuchUser() => Assert.That(await Service().Get(9999), Is.Null);

    [Test]
    public async Task Get_OfAUserWithNothing_HasNoChoiceNoEquippedSetAndNoRender()
    {
        var dto = await Service().Get(_userId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto!.Choice, Is.Null);
            Assert.That(dto.HasEquippedSet, Is.False);
            Assert.That((dto.RenderState, dto.RenderedAt, dto.RenderError), Is.EqualTo(((string?)null, (DateTime?)null, (string?)null)));
            Assert.That(dto.Files, Is.Empty);
        }
    }

    [TestCase(XboxUserArmorStatus.Found, "core", true)]
    [TestCase(XboxUserArmorStatus.Found, null, false)]
    [TestCase(XboxUserArmorStatus.NotFound, "core", false)]
    [TestCase(XboxUserArmorStatus.Failed, "core", false)]
    public async Task Get_HasEquippedSet_OnlyForAFoundArmorWithACore(XboxUserArmorStatus status, string? coreId, bool expected)
    {
        Context.XboxUserArmor.Add(new XboxUserArmor { XboxUserID = 500, Status = status, CoreId = coreId, CheckedAt = Start.UtcDateTime });
        await Context.SaveChangesAsync();

        var dto = await Service().Get(_userId);

        Assert.That(dto!.HasEquippedSet, Is.EqualTo(expected));
    }

    [Test]
    public async Task Get_HasNoEquippedSet_ForAUserWithNoXboxAccount()
    {
        var plain = User.Seed(userName: "plain");
        Context.Users.Add(plain);
        await Context.SaveChangesAsync();

        Assert.That((await Service().Get(plain.Id))!.HasEquippedSet, Is.False);
    }

    [Test]
    public async Task Get_GivesTheChoiceTheRenderAndItsKnownFilesInTheirOrder()
    {
        Assert.That((await Service().Set(_userId, Mk7())).Outcome, Is.EqualTo(SetSpartanOutcome.Saved));
        var render = await Context.UserSpartanRenders.SingleAsync();
        render.State = UserSpartanRenderState.Failed;
        render.Error = "boom";
        render.RenderedAt = Start.UtcDateTime;
        Context.UserSpartanFiles.AddRange(
            new UserSpartanFile { UserId = _userId, Name = "neutral-head", ContentType = "image/png", Content = [1] },
            new UserSpartanFile { UserId = _userId, Name = "stance-glb", ContentType = "model/gltf-binary", Content = [2] },
            new UserSpartanFile { UserId = _userId, Name = "mystery", ContentType = "image/png", Content = [3] });
        await Context.SaveChangesAsync();

        var dto = await Service().Get(_userId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(dto!.Choice!.Core, Is.EqualTo("Mark VII"));
            Assert.That(dto.Choice.Armor!["helmet"], Is.EqualTo("Eaglestrike"));
            Assert.That(dto.Choice.Coating, Is.EqualTo("111"));
            Assert.That(dto.RenderState, Is.EqualTo("Failed"));
            Assert.That(dto.RenderError, Is.EqualTo("boom"));
            Assert.That(dto.RenderedAt, Is.EqualTo(Start.UtcDateTime));
            Assert.That(dto.Files, Is.EqualTo(new List<string> { "stance-glb", "neutral-head" }));
        }
    }

    [Test]
    public async Task Catalog_IsWhatTheSourceGives()
    {
        var up = await Service().Catalog();
        var down = await Service(catalogUp: false).Catalog();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(up, Is.SameAs(Catalog));
            Assert.That(down, Is.Null);
        }
    }

    [Test]
    public async Task Set_SavesTheChoice_AndAsksForItsRender()
    {
        var result = await Service().Set(_userId, Mk7());

        var row = await Context.UserSpartans.AsNoTracking().SingleAsync();
        var render = await Context.UserSpartanRenders.AsNoTracking().SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(SetSpartanOutcome.Saved));
            Assert.That(result.Problems, Is.Empty);
            Assert.That(row.ChoiceJson, Is.EqualTo("""{"core":"Mark VII","armor":{"helmet":"Eaglestrike"},"parts":null,"coating":"111","visor":"Plain"}"""));
            Assert.That(row.UpdatedAt, Is.EqualTo(Start.UtcDateTime));
            Assert.That((render.State, render.RequestedAt, render.Error), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime, (string?)null)));
        }
    }

    [Test]
    public async Task Set_Again_ReplacesTheChoice_AndAsksAgainForTheRender_ClearingItsError()
    {
        await Service().Set(_userId, Mk7());
        var render = await Context.UserSpartanRenders.SingleAsync();
        render.State = UserSpartanRenderState.Failed;
        render.Error = "boom";
        await Context.SaveChangesAsync();
        _time.Advance(TimeSpan.FromHours(1));

        await Service().Set(_userId, Mk7("Mark VII"));

        Context.ChangeTracker.Clear();
        var row = await Context.UserSpartans.SingleAsync();
        var again = await Context.UserSpartanRenders.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(row.ChoiceJson, Does.Contain("\"helmet\":\"Mark VII\""));
            Assert.That(row.UpdatedAt, Is.EqualTo(Start.UtcDateTime.AddHours(1)));
            Assert.That((again.State, again.RequestedAt, again.Error), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddHours(1), (string?)null)));
        }
    }

    [Test]
    public async Task Set_RefusesWhatTheCatalogDoesNotListAndSavesNothing()
    {
        var result = await Service().Set(_userId, new SpartanChoice("Nope"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(SetSpartanOutcome.Invalid));
            Assert.That(result.Problems, Is.EqualTo(new List<string> { "There is no core 'Nope'" }));
            Assert.That(await Context.UserSpartans.AnyAsync(), Is.False);
            Assert.That(await Context.UserSpartanRenders.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task Set_RefusesAChoiceTooLongToKeep()
    {
        var piece = new string('x', UserSpartan.MaxChoiceLength);
        var catalog = new SpartanCatalog(
            [new SpartanCatalogCore("Mark VII", [new SpartanCatalogRegion("REGION", "helmet", 100, [new(1, piece)])])], [], [], []);

        var result = await Service(catalog).Set(_userId, new SpartanChoice("Mark VII", new Dictionary<string, string> { ["helmet"] = piece }));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(SetSpartanOutcome.Invalid));
            Assert.That(result.Problems, Is.EqualTo(new List<string> { "That Spartan is too long to keep" }));
            Assert.That(await Context.UserSpartans.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task Set_SaysTheCatalogIsUnavailable_WhenTheRenderServiceCannotBeReached()
    {
        var result = await Service(catalogUp: false).Set(_userId, Mk7());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(SetSpartanOutcome.CatalogUnavailable));
            Assert.That(await Context.UserSpartans.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task Set_SaysThereIsNoSuchUser_BeforeAskingForTheCatalog()
    {
        var result = await Service(catalogUp: false).Set(9999, Mk7());

        Assert.That(result.Outcome, Is.EqualTo(SetSpartanOutcome.NoSuchUser));
    }

    [Test]
    public async Task Clear_RemovesTheChoice_AndAsksAgainForTheRender_WhenOneExists()
    {
        await Service().Set(_userId, Mk7());
        var render = await Context.UserSpartanRenders.SingleAsync();
        render.State = UserSpartanRenderState.Done;
        await Context.SaveChangesAsync();
        _time.Advance(TimeSpan.FromMinutes(5));

        var cleared = await Service().Clear(_userId);

        Context.ChangeTracker.Clear();
        var again = await Context.UserSpartanRenders.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared, Is.True);
            Assert.That(await Context.UserSpartans.AnyAsync(), Is.False);
            Assert.That((again.State, again.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddMinutes(5))));
        }
    }

    [Test]
    public async Task Clear_WithNothingChosenAndNoRender_AsksForNoRender()
    {
        var cleared = await Service().Clear(_userId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cleared, Is.True);
            Assert.That(await Context.UserSpartanRenders.AnyAsync(), Is.False);
        }
    }

    [Test]
    public async Task Clear_IsFalseForNoSuchUser() => Assert.That(await Service().Clear(9999), Is.False);

    [Test]
    public async Task RequestRender_CreatesTheRequest_ThenRequestsItAgain()
    {
        var first = await Service().RequestRender(_userId);
        var render = await Context.UserSpartanRenders.SingleAsync();
        render.State = UserSpartanRenderState.Done;
        await Context.SaveChangesAsync();
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = await Service().RequestRender(_userId);

        Context.ChangeTracker.Clear();
        var after = await Context.UserSpartanRenders.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That((first, second), Is.EqualTo((true, true)));
            Assert.That((after.State, after.RequestedAt), Is.EqualTo((UserSpartanRenderState.Requested, Start.UtcDateTime.AddMinutes(1))));
        }
    }

    [Test]
    public async Task RequestRender_IsFalseForNoSuchUser() => Assert.That(await Service().RequestRender(9999), Is.False);

    private async Task StoreFileAsync(string name, byte[] content, DateTime? renderedAt = null)
    {
        if (await Context.UserSpartanRenders.FindAsync(_userId) is null)
            Context.UserSpartanRenders.Add(new UserSpartanRender { UserId = _userId, State = UserSpartanRenderState.Done, RequestedAt = Start.UtcDateTime, RenderedAt = renderedAt });
        Context.UserSpartanFiles.Add(new UserSpartanFile { UserId = _userId, Name = name, ContentType = "image/png", Content = content });
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task File_GivesTheNamedFileOfTheUser_OrNull()
    {
        await StoreFileAsync("neutral-body", [7, 8]);

        var file = await Service().File(_userId, "neutral-body");
        var other = await Service().File(_userId, "neutral-head");
        var wrongUser = await Service().File(_userId + 1, "neutral-body");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(file!.ContentType, Is.EqualTo("image/png"));
            Assert.That(file.Content, Is.EqualTo(new byte[] { 7, 8 }));
            Assert.That(other, Is.Null);
            Assert.That(wrongUser, Is.Null);
        }
    }

    [Test]
    public async Task TheSpartanHeadshot_IsTheNeutralHeadFileWithWhenItWasRendered()
    {
        var images = new XboxImageService(Context);
        var renderedAt = Start.UtcDateTime.AddMinutes(-3);
        await StoreFileAsync("neutral-body", [1], renderedAt);
        var beforeHead = await images.GetSpartanHeadshotVersionAsync(_userId);
        await StoreFileAsync("neutral-head", [9, 9], renderedAt);

        var version = await images.GetSpartanHeadshotVersionAsync(_userId);
        var content = await images.GetSpartanHeadshotAsync(_userId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(beforeHead, Is.Null, "another file is not the headshot");
            Assert.That(version, Is.EqualTo(renderedAt));
            Assert.That(content!.Content, Is.EqualTo(new byte[] { 9, 9 }));
            Assert.That((content.ContentType, content.CheckedAt), Is.EqualTo(("image/png", renderedAt)));
            Assert.That(await images.GetSpartanHeadshotAsync(_userId + 1), Is.Null);
            Assert.That(await images.GetSpartanHeadshotVersionAsync(_userId + 1), Is.Null);
        }
    }

    [Test]
    public async Task TheSpartanHeadshot_OfARenderWithNoRenderedAt_IsVersionedByTheEpoch()
    {
        await StoreFileAsync("neutral-head", [9]);

        var content = await new XboxImageService(Context).GetSpartanHeadshotAsync(_userId);

        Assert.That(content!.CheckedAt, Is.EqualTo(DateTime.UnixEpoch));
    }

    [Test]
    public async Task GetUserAndChoiceOfXbox_GivesTheUserTiedToTheAccountAndTheirChoice_DefaultingToSpartan()
    {
        var images = new XboxImageService(Context);

        var known = await images.GetUserAndChoiceOfXboxAsync(500);
        var unknown = await images.GetUserAndChoiceOfXboxAsync(501);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(known, Is.EqualTo((_userId, ProfilePictureChoice.Spartan)));
            Assert.That(unknown, Is.Null);
        }
    }

    [Test]
    public async Task GetUserAndChoiceOfXbox_FollowsTheUsersChoice()
    {
        var user = await Context.Users.SingleAsync(u => u.Id == _userId);
        user.ChooseProfilePicture(ProfilePictureChoice.GamerPicture);
        await Context.SaveChangesAsync();

        Assert.That(await new XboxImageService(Context).GetUserAndChoiceOfXboxAsync(500), Is.EqualTo((_userId, ProfilePictureChoice.GamerPicture)));
    }
}
