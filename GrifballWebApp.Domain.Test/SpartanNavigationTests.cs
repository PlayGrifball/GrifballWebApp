using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>The navigation properties EF fills in for the Spartan rows point back at the row's owner.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SpartanNavigationTests
{
    [Test]
    public void ASpartanRow_PointsBackAtItsUser()
    {
        var user = User.Seed();
        var choice = new UserSpartan { User = user };
        var render = new UserSpartanRender { User = user };
        var file = new UserSpartanFile { Render = render };
        render.Files.Add(file);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.User, Is.SameAs(user));
            Assert.That(render.User, Is.SameAs(user));
            Assert.That(file.Render, Is.SameAs(render));
        }
    }

    [Test]
    public void TheEquippedArmor_PointsBackAtItsXboxUser()
    {
        var account = XboxUser.FromXbox(1, "Gamer");
        var armor = new XboxUserArmor { XboxUser = account };

        Assert.That(armor.XboxUser, Is.SameAs(account));
    }
}
