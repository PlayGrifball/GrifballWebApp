using GrifballWebApp.Application.Profile;
using GrifballWebApp.Server.Profile;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Test;

/// <summary>MyThemeController's unit tests: the signed-in gate, saving and reading back.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class MyThemeControllerUnitTests : SqliteGrifballContextFixture
{
    private MyThemeController Controller() => new(new UserThemeService(Db));

    private static UserThemeDto Theme() => new()
    {
        Era = "",
        Font = "Rajdhani",
        Primary = "#E8922D",
        Secondary = "#2C3344",
        Tertiary = "#E6C15A",
        Neutral = "#6B768F",
        NeutralVariant = "#5C667D",
        Error = "#E5484D",
    };

    [Test]
    public async Task Actions_ReplyUnauthorized_ForAnUnknownCaller()
    {
        var controller = Controller().WithAnonymousUser();

        var get = await controller.Get(CancellationToken.None);
        var set = await controller.Set(Theme(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            get.Result.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
            set.AssertProblem(StatusCodes.Status401Unauthorized, "You must be signed in");
        }
    }

    [Test]
    public async Task Set_SavesTheCallersTheme_AndGet_ReadsItBack()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var controller = Controller().WithUser(user.Id.ToString());

        var none = await controller.Get(CancellationToken.None);
        var saved = await controller.Set(Theme(), CancellationToken.None);
        var read = await controller.Get(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ControllerTestHelpers.StatusOf(none.Result!), Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(ControllerTestHelpers.StatusOf(saved), Is.EqualTo(StatusCodes.Status204NoContent));
            Assert.That(read.Value?.Primary, Is.EqualTo("#E8922D"));
            Assert.That(await Db.Users.Where(u => u.Id == user.Id).Select(u => u.Theme).SingleAsync(), Is.Not.Null);
        }
    }

    [Test]
    public async Task Set_RepliesBadRequest_ForAThemeTheEditorCannotMake()
    {
        var user = await AddUser(discordUserId: 1, gamertag: "Grifter1");
        var theme = Theme();
        theme.Primary = "url(x)";

        var result = await Controller().WithUser(user.Id.ToString()).Set(theme, CancellationToken.None);

        result.AssertProblem(StatusCodes.Status400BadRequest, "That is not a theme the editor can make");
    }

    [Test]
    public async Task Set_RepliesNotFound_ForASignedInUserWhoIsGone()
    {
        var controller = Controller().WithUser("987654");

        var set = await controller.Set(Theme(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            set.AssertProblem(StatusCodes.Status404NotFound, "There is no such user");
        }
    }
}
