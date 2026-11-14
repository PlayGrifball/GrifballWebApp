using GrifballWebApp.Domain.League;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// Season.Announce (#435): a season still to start must be public by the time its signups open, or already public, since
/// players can't sign up for a season they can't see. A refusal changes nothing.
/// </summary>
[TestFixture]
public class SeasonAnnounceUnitTests
{
    private static readonly DateTime Now = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A season whose signups open <paramref name="signupsInDays"/> from now, starting 20 days after.</summary>
    private static Season Season(int signupsInDays)
        => GrifballWebApp.Domain.League.Season.Seed(seasonName: "S").WithDates(signupsOpen: Now.AddDays(signupsInDays), signupsClose: Now.AddDays(signupsInDays + 5),
            draftStart: Now.AddDays(signupsInDays + 10), seasonStart: Now.AddDays(signupsInDays + 20), seasonEnd: Now.AddDays(signupsInDays + 60));

    [TestCase(10, 10, Description = "public the moment signups open")]
    [TestCase(10, 3, Description = "public a week before")]
    [TestCase(-5, -1, Description = "announced after signups opened, but public already")]
    [TestCase(-5, 0, Description = "made public now, late")]
    public void ASeasonPublicByItsSignups_OrAlready_IsAnnounced(int signupsInDays, int publicInDays)
    {
        var season = Season(signupsInDays);

        var result = season.Announce(Now.AddDays(publicInDays), Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SeasonAnnounceResult.Announced));
            Assert.That(season.PublicAt, Is.EqualTo(Now.AddDays(publicInDays)));
        }
    }

    [TestCase(10, null, Description = "no public date")]
    [TestCase(10, 11, Description = "public a day after signups open")]
    [TestCase(-5, 2, Description = "signups open, public only in two days")]
    public void ASeasonStillToStart_NotPublicByItsSignups_IsRefused_AndKeepsItsDate(int signupsInDays, int? publicInDays)
    {
        var season = Season(signupsInDays);
        season.AnnounceAt(Now.AddDays(-30));

        var result = season.Announce(publicInDays is int days ? Now.AddDays(days) : null, Now);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(SeasonAnnounceResult.NotPublicBySignups));
            Assert.That(season.PublicAt, Is.EqualTo(Now.AddDays(-30)), "unchanged");
        }
    }

    [Test]
    public void ASeasonThatStarted_TakesAnyPublicDate()
    {
        var season = Season(-60);

        Assert.That(season.Announce(null, Now), Is.EqualTo(SeasonAnnounceResult.Announced));
    }
}
