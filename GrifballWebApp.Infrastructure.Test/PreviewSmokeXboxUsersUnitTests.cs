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
        var first = PreviewSmokeXboxUsers.Xuid("smoke-run1-p1");
        var again = PreviewSmokeXboxUsers.Xuid(string.Concat("smoke-run1-", "p1"));
        var other = PreviewSmokeXboxUsers.Xuid("smoke-run1-p2");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.Negative);
            Assert.That(again, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first));
        }
    }
}
