using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>UserThemeService against the fake IApplicationDbContext: a player saving the theme they made, and every
/// device reading it back.</summary>
[TestFixture]
internal sealed class UserThemeServiceUnitTests : ApplicationDbContextTests
{
    private UserThemeService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new UserThemeService(Context);
    }

    private static UserThemeDto Theme(string primary = "#E8922D") => new()
    {
        Era = "2008",
        Font = "Roboto",
        Primary = primary,
        Secondary = "#2C3344",
        Tertiary = "#e6c",
        Neutral = "#6B768F",
        NeutralVariant = "#5C667D",
        Error = "#E5484D",
    };

    private async Task AddUser()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task Get_IsNull_UntilTheUserSavedATheme_AndForNoSuchUser()
    {
        await AddUser();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _service.Get(1), Is.Null);
            Assert.That(await _service.Get(99), Is.Null);
        }
    }

    [Test]
    public async Task Set_SavesTheTheme_ThatGetReturnsAsItWas()
    {
        await AddUser();

        var result = await _service.Set(1, Theme());

        var saved = await _service.Get(1);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SetThemeResult.Set));
            Assert.That(saved, Is.Not.Null);
            Assert.That((saved!.Era, saved.Font, saved.Primary, saved.Tertiary, saved.NeutralVariant), Is.EqualTo(("2008", "Roboto", "#E8922D", "#e6c", "#5C667D")));
        }
    }

    [Test]
    public async Task Set_RefusesAThemeTheEditorCannotMake_AndKeepsTheOldOne()
    {
        await AddUser();
        await _service.Set(1, Theme());

        var badColour = await _service.Set(1, Theme("red; background:url(x)"));
        var badFont = await _service.Set(1, new UserThemeDto { Era = "", Font = "Comic Sans", Primary = "#fff", Secondary = "#fff", Tertiary = "#fff", Neutral = "#fff", NeutralVariant = "#fff", Error = "#fff" });
        var badEra = await _service.Set(1, new UserThemeDto { Era = "<script>", Font = "Roboto", Primary = "#fff", Secondary = "#fff", Tertiary = "#fff", Neutral = "#fff", NeutralVariant = "#fff", Error = "#fff" });

        using (Assert.EnterMultipleScope())
        {
            Assert.That(new[] { badColour, badFont, badEra }, Is.All.EqualTo(SetThemeResult.Invalid));
            Assert.That((await _service.Get(1))!.Primary, Is.EqualTo("#E8922D"));
        }
    }

    [Test]
    public async Task Set_RefusesAThemeWithAColourMissing_AndTakesOneWithNoEra()
    {
        await AddUser();

        var noColour = await _service.Set(1, new UserThemeDto { Era = null!, Font = "Roboto", Primary = null!, Secondary = "#fff", Tertiary = "#fff", Neutral = "#fff", NeutralVariant = "#fff", Error = "#fff" });
        var noEra = await _service.Set(1, new UserThemeDto { Era = null!, Font = "Roboto", Primary = "#fff", Secondary = "#fff", Tertiary = "#fff", Neutral = "#fff", NeutralVariant = "#fff", Error = "#fff" });
        var noColourOnly = await _service.Set(1, new UserThemeDto { Era = "", Font = "Roboto", Primary = null!, Secondary = "#fff", Tertiary = "#fff", Neutral = "#fff", NeutralVariant = "#fff", Error = "#fff" });
        var nothing = await _service.Set(1, null!);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(noColour, Is.EqualTo(SetThemeResult.Invalid));
            Assert.That(noColourOnly, Is.EqualTo(SetThemeResult.Invalid));
            Assert.That(noEra, Is.EqualTo(SetThemeResult.Set));
            Assert.That(nothing, Is.EqualTo(SetThemeResult.Invalid));
            Assert.That(UserThemeService.IsValid(null), Is.False);
        }
    }

    [Test]
    public async Task Set_SaysThereIsNoSuchUser()
    {
        Assert.That(await _service.Set(99, Theme()), Is.EqualTo(SetThemeResult.NoSuchUser));
    }

    [Test]
    public async Task Get_IsNull_ForStoredJsonNull()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();
        var user = await Context.Users.SingleAsync(u => u.Id == 1);
        user.ChooseTheme("null");
        await Context.SaveChangesAsync();

        Assert.That(await _service.Get(1), Is.Null);
    }
    [Test]
    public async Task Get_IsNull_ForStoredTextThatIsNoTheme()
    {
        Context.Users.Add(User.Seed(userName: "alice", id: 1));
        await Context.SaveChangesAsync();
        var user = await Context.Users.SingleAsync(u => u.Id == 1);
        user.ChooseTheme("{\"primary\":\"javascript:alert(1)\"}");
        await Context.SaveChangesAsync();

        Assert.That(await _service.Get(1), Is.Null);
    }
}
