using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Stats;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// Who sees a season not announced yet (#435): commissioners and sysadmins do, players and signed-out callers never; and the
/// season a caller means when they name none, by what it's for.
/// </summary>
[TestFixture]
internal sealed class SeasonVisibilityUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly int[] PublicSeasons = [1, 2];
    private static readonly int[] AllSeasons = [1, 2, 3];
    private FakeTimeProvider _time = null!;

    [SetUp]
    public void SetUp() => _time = new FakeTimeProvider(Now);

    private SeasonService Seasons() => new(Context, new SeasonStatsService(Context, _time), _time);

    /// <summary>A season starting <paramref name="startsInDays"/> from now (negative: started), announced
    /// <paramref name="announcedInDays"/> from now (null: never).</summary>
    private async Task<Season> AddSeason(int id, string name, int startsInDays, int? announcedInDays, int lastsDays = 30)
    {
        var start = Now.AddDays(startsInDays);
        var season = Season.Seed(seasonName: name, seasonID: id);
        season.Schedule(start.AddDays(-30), start.AddDays(-20), start.AddDays(-10), start, start.AddDays(lastsDays));
        season.AnnounceAt(announcedInDays is int days ? Now.AddDays(days) : null);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
        return season;
    }

    [TestCase(new string[0], SeasonViewer.Public)]
    [TestCase(new[] { SiteRoles.Player }, SeasonViewer.Public)]
    [TestCase(new[] { SiteRoles.Commissioner }, SeasonViewer.Staff)]
    [TestCase(new[] { SiteRoles.Player, SiteRoles.Sysadmin }, SeasonViewer.Staff)]
    public void ACallersRoles_DecideTheirViewer(string[] roles, SeasonViewer expected)
        => Assert.That(SeasonVisibility.For(roles), Is.EqualTo(expected));

    [Test]
    public async Task ASeasonNotAnnouncedYet_IsHiddenFromThePublic_WithItsTeamsAndMatches_ButNotFromStaff()
    {
        var secret = await AddSeason(1, "Secret", startsInDays: 40, announcedInDays: 10);
        var team = Team.Seed("Team", seasonID: secret.SeasonID, teamID: 7);
        Context.Teams.Add(team);
        var match = SeasonMatch.Seed(secret.SeasonID, null, null);
        Context.SeasonMatches.Add(match);
        await Context.SaveChangesAsync();
        var visibility = new SeasonVisibility(Context, _time);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await visibility.IsSeasonHidden(secret.SeasonID, SeasonViewer.Public), Is.True);
            Assert.That(await visibility.IsTeamHidden(team.TeamID, SeasonViewer.Public), Is.True);
            Assert.That(await visibility.IsSeasonMatchHidden(match.SeasonMatchID, SeasonViewer.Public), Is.True);
            Assert.That(await visibility.IsSeasonHidden(secret.SeasonID, SeasonViewer.Staff), Is.False);
            Assert.That(await visibility.IsTeamHidden(team.TeamID, SeasonViewer.Staff), Is.False);
            Assert.That(await visibility.IsSeasonMatchHidden(match.SeasonMatchID, SeasonViewer.Staff), Is.False);
        }
    }

    [Test]
    public async Task AnAnnouncedOrStartedSeason_AndOneThatDoesntExist_AreNotHidden()
    {
        var announced = await AddSeason(1, "Announced", startsInDays: 40, announcedInDays: -1);
        var started = await AddSeason(2, "Started", startsInDays: -5, announcedInDays: null);
        var visibility = new SeasonVisibility(Context, _time);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await visibility.IsSeasonHidden(announced.SeasonID, SeasonViewer.Public), Is.False);
            Assert.That(await visibility.IsSeasonHidden(started.SeasonID, SeasonViewer.Public), Is.False);
            Assert.That(await visibility.IsSeasonHidden(999, SeasonViewer.Public), Is.False, "its own lookup says there's none");
        }
    }

    [TestCase(SeasonPurpose.Current, "Running")]
    [TestCase(SeasonPurpose.Active, "Running")]
    [TestCase(SeasonPurpose.Upcoming, "Next")]
    public async Task DuringASeason_EachPurposeMeans(SeasonPurpose purpose, string expected)
    {
        await AddSeason(1, "Ended", startsInDays: -100, announcedInDays: null);
        await AddSeason(2, "Running", startsInDays: -10, announcedInDays: null);
        await AddSeason(3, "Next", startsInDays: 60, announcedInDays: -1);

        var id = await Seasons().GetDefaultSeasonID(purpose, SeasonViewer.Public);

        Assert.That(Context.Seasons.Single(s => s.SeasonID == id).SeasonName, Is.EqualTo(expected));
    }

    [TestCase(SeasonPurpose.Current, "Ended")]
    [TestCase(SeasonPurpose.Active, "Next")]
    [TestCase(SeasonPurpose.Upcoming, "Next")]
    public async Task BetweenSeasons_EachPurposeMeans(SeasonPurpose purpose, string expected)
    {
        await AddSeason(1, "Ended", startsInDays: -40, announcedInDays: null);
        await AddSeason(2, "Next", startsInDays: 20, announcedInDays: -1);
        await AddSeason(3, "Later", startsInDays: 120, announcedInDays: -1);

        var id = await Seasons().GetDefaultSeasonID(purpose, SeasonViewer.Public);

        Assert.That(Context.Seasons.Single(s => s.SeasonID == id).SeasonName, Is.EqualTo(expected));
    }

    [TestCase(SeasonViewer.Public, "Ended")]
    [TestCase(SeasonViewer.Staff, "Secret")]
    public async Task ASeasonNotAnnouncedYet_IsTheUpcomingDefault_ForStaffAlone(SeasonViewer viewer, string expected)
    {
        await AddSeason(1, "Ended", startsInDays: -40, announcedInDays: null);
        await AddSeason(2, "Secret", startsInDays: 20, announcedInDays: 10);

        var id = await Seasons().GetDefaultSeasonID(SeasonPurpose.Upcoming, viewer);

        Assert.That(Context.Seasons.Single(s => s.SeasonID == id).SeasonName, Is.EqualTo(expected));
    }

    [Test]
    public async Task WithOnlyASeasonNotAnnouncedYet_ThePublicHasNoDefault()
    {
        await AddSeason(1, "Secret", startsInDays: 20, announcedInDays: 10);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await Seasons().GetDefaultSeasonID(SeasonPurpose.Current, SeasonViewer.Public), Is.Zero);
            Assert.That(await Seasons().GetDefaultSeasonID(SeasonPurpose.Current, SeasonViewer.Staff), Is.EqualTo(1));
        }
    }

    [Test]
    public async Task TheSeasonList_HasTheAnnouncedSeasons_AndStaffsUnannouncedOnesToo()
    {
        await AddSeason(1, "Started", startsInDays: -10, announcedInDays: null);
        await AddSeason(2, "Announced", startsInDays: 20, announcedInDays: -1);
        await AddSeason(3, "Secret", startsInDays: 40, announcedInDays: 10);

        var everyone = (await Seasons().GetSeasons(new SeasonListFilter())).Results.Select(s => s.SeasonID);
        var staff = (await Seasons().GetSeasons(new SeasonListFilter(), SeasonViewer.Staff)).Results.Select(s => s.SeasonID);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(everyone, Is.EquivalentTo(PublicSeasons));
            Assert.That(staff, Is.EquivalentTo(AllSeasons));
        }
    }
}
