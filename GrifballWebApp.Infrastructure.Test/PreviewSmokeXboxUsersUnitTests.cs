using GrifballWebApp.Infrastructure.Preview;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>PreviewSmokeXboxUsers's made-up XUIDs. Its lookups are tested in GrifballWebApp.Integration.Test.</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PreviewSmokeXboxUsersUnitTests
{
    [Test]
    public void A_xuid_is_negative_and_the_same_for_the_same_gamertag()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(PreviewSmokeXboxUsers.Xuid("smoke-run1-p1"), Is.Negative);
            Assert.That(PreviewSmokeXboxUsers.Xuid("smoke-run1-p1"), Is.EqualTo(PreviewSmokeXboxUsers.Xuid("smoke-run1-p1")));
            Assert.That(PreviewSmokeXboxUsers.Xuid("smoke-run1-p1"), Is.Not.EqualTo(PreviewSmokeXboxUsers.Xuid("smoke-run1-p2")));
        }
    }
}
