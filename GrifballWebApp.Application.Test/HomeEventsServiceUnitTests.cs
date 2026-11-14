using GrifballWebApp.Application.Home;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

[TestFixture]
internal sealed class HomeEventsServiceUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private FakeTimeProvider _time = null!;
    private HomeEventsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(Now);
        _service = new HomeEventsService(new GrifballWebApp.Application.Seasons.SeasonVisibility(Context, _time), _time);
    }

    [Test]
    public async Task CurrentAndFuture_NoSeasons_ReturnsEmpty()
    {
        var events = await _service.CurrentAndFuture();

        Assert.That(events, Is.Empty);
    }

    [Test]
    public async Task CurrentAndFuture_OneSeasonCoveringEveryPhase_ReturnsThreeEventsSoonestFirst()
    {
        var season = Season.Seed(seasonName: "S1", seasonID: 1);
        // Signups still open, draft not started, season not ended: one season gives all three event kinds.
        season.Schedule(
            signupsOpen: Now.AddDays(-5), signupsClose: Now.AddDays(5),
            draftStart: Now.AddDays(10), seasonStart: Now.AddDays(20), seasonEnd: Now.AddDays(60));
        season.AnnounceAt(Now.AddDays(-6));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();

        var events = await _service.CurrentAndFuture();

        Assert.That(events, Has.Length.EqualTo(3));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(events[0].EventType, Is.EqualTo(EventType.Signup));
            Assert.That(events[0].Start, Is.EqualTo(season.SignupsOpen));
            Assert.That(events[1].EventType, Is.EqualTo(EventType.Draft));
            Assert.That(events[1].Start, Is.EqualTo(season.DraftStart));
            Assert.That(events[2].EventType, Is.EqualTo(EventType.Season));
            Assert.That(events[2].Start, Is.EqualTo(season.SeasonStart));
        }
    }

    [Test]
    public async Task CurrentAndFuture_ASeasonNotAnnouncedYet_IsStaffsAlone()
    {
        var season = Season.Seed(seasonName: "Secret", seasonID: 1);
        season.Schedule(
            signupsOpen: Now.AddDays(5), signupsClose: Now.AddDays(10),
            draftStart: Now.AddDays(12), seasonStart: Now.AddDays(20), seasonEnd: Now.AddDays(60));
        season.AnnounceAt(Now.AddDays(3));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();

        var everyone = await _service.CurrentAndFuture();
        var staff = await _service.CurrentAndFuture(GrifballWebApp.Application.Seasons.SeasonViewer.Staff);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(everyone, Is.Empty, "announced in 3 days");
            Assert.That(staff, Has.Length.EqualTo(3));
        }
    }

    [Test]
    public async Task CurrentAndFuture_EndedSeason_IsExcluded()
    {
        var season = Season.Seed(seasonName: "Old", seasonID: 1);
        season.Schedule(
            signupsOpen: Now.AddDays(-90), signupsClose: Now.AddDays(-80),
            draftStart: Now.AddDays(-70), seasonStart: Now.AddDays(-60), seasonEnd: Now.AddDays(-1));
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();

        var events = await _service.CurrentAndFuture();

        Assert.That(events, Is.Empty);
    }
}
