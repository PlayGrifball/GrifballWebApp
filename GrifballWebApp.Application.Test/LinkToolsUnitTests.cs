using GrifballWebApp.Application.Chat;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Application.Tools;
using GrifballWebApp.Domain.League;
using Microsoft.Extensions.Time.Testing;

namespace GrifballWebApp.Application.Test;

/// <summary>The links a chat gives to the site's pages (<see cref="LinkTools"/>): UrlService's routes, only to pages the caller may see.</summary>
[TestFixture]
internal sealed class LinkToolsUnitTests : ApplicationDbContextTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Game = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private FakeTimeProvider _time = null!;
    private int _seasonMatchID;
    private int _hiddenSeasonMatchID;

    [SetUp]
    public async Task SetUp()
    {
        _time = new FakeTimeProvider(Now);
        await AddSeason(12, startsInDays: -5, announcedInDays: null);
        await AddSeason(13, startsInDays: 40, announcedInDays: 10);
        Context.Teams.Add(Team.Seed("Open", seasonID: 12, teamID: 5));
        Context.Teams.Add(Team.Seed("Secret", seasonID: 13, teamID: 6));
        var open = SeasonMatch.Seed(12, null, null);
        var hidden = SeasonMatch.Seed(13, null, null);
        Context.SeasonMatches.AddRange(open, hidden);
        await Context.SaveChangesAsync();
        _seasonMatchID = open.SeasonMatchID;
        _hiddenSeasonMatchID = hidden.SeasonMatchID;
    }

    private async Task AddSeason(int id, int startsInDays, int? announcedInDays)
    {
        var start = Now.AddDays(startsInDays);
        var season = Season.Seed(seasonName: $"Season {id}", seasonID: id);
        season.Schedule(start.AddDays(-30), start.AddDays(-20), start.AddDays(-10), start, start.AddDays(30));
        season.AnnounceAt(announcedInDays is int days ? Now.AddDays(days) : null);
        Context.Seasons.Add(season);
        await Context.SaveChangesAsync();
    }

    private LinkTools Tools(bool relative = false, params string[] roles)
        => new(new ToolCaller(null, roles) { RelativeLinks = relative }, new UrlService("https://grif.test"), new SeasonVisibility(Context, _time));

    [TestCase(SitePage.Seasons, "https://grif.test/seasons")]
    [TestCase(SitePage.Leaderboards, "https://grif.test/leaderboards")]
    [TestCase(SitePage.LateLeague, "https://grif.test/late-league")]
    public async Task GetSiteLink_ForAPageWithNoID_IsItsAddress(SitePage page, string expected)
        => Assert.That(await Tools().GetSiteLink(page), Is.EqualTo(expected));

    [Test]
    public async Task GetSiteLink_BuildsEachPageFromUrlService_Absolute()
    {
        var tools = Tools();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await tools.GetSiteLink(SitePage.LateLeagueSeason, matchmakingSeasonID: 4), Is.EqualTo("https://grif.test/late-league/4"));
            Assert.That(await tools.GetSiteLink(SitePage.Season, seasonID: 12), Is.EqualTo("https://grif.test/season/12"));
            Assert.That(await tools.GetSiteLink(SitePage.SeasonSignups, seasonID: 12), Is.EqualTo("https://grif.test/season/12/signups"));
            Assert.That(await tools.GetSiteLink(SitePage.SeasonDraft, seasonID: 12), Is.EqualTo("https://grif.test/season/12/draft"));
            Assert.That(await tools.GetSiteLink(SitePage.PlayerGrades, seasonID: 12), Is.EqualTo("https://grif.test/season/12/playergrades"));
            Assert.That(await tools.GetSiteLink(SitePage.Team, seasonID: 12, teamID: 5), Is.EqualTo("https://grif.test/season/12/team/5"));
            Assert.That(await tools.GetSiteLink(SitePage.TeamHistory, teamID: 5), Is.EqualTo("https://grif.test/history/team/5"));
            Assert.That(await tools.GetSiteLink(SitePage.SeasonMatch, seasonMatchID: _seasonMatchID), Is.EqualTo($"https://grif.test/seasonmatch/{_seasonMatchID}"));
            Assert.That(await tools.GetSiteLink(SitePage.Game, gameID: Game), Is.EqualTo($"https://grif.test/match/{Game}"));
            Assert.That(await tools.GetSiteLink(SitePage.Profile, userID: 3), Is.EqualTo("https://grif.test/profile/3"));
            Assert.That(await tools.GetSiteLink(SitePage.XboxAccount, xboxUserID: 2533274790000001L), Is.EqualTo("https://grif.test/xbox/2533274790000001"));
        }
    }

    [Test]
    public async Task GetSiteLink_ForTheSitesOwnChat_IsAPathOnTheSite()
    {
        var tools = Tools(relative: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await tools.GetSiteLink(SitePage.Season, seasonID: 12), Is.EqualTo("/season/12"));
            Assert.That(await tools.GetSiteLink(SitePage.Team, seasonID: 12, teamID: 5), Is.EqualTo("/season/12/team/5"));
            Assert.That(await tools.GetSiteLink(SitePage.Leaderboards), Is.EqualTo("/leaderboards"));
            Assert.That(await tools.GetSiteLink(SitePage.Game, gameID: Game), Is.EqualTo($"/match/{Game}"));
        }
    }

    [TestCase(SitePage.Season, "seasonID")]
    [TestCase(SitePage.Team, "seasonID")]
    [TestCase(SitePage.TeamHistory, "teamID")]
    [TestCase(SitePage.SeasonMatch, "seasonMatchID")]
    [TestCase(SitePage.Game, "gameID")]
    [TestCase(SitePage.Profile, "userID")]
    [TestCase(SitePage.XboxAccount, "xboxUserID")]
    [TestCase(SitePage.LateLeagueSeason, "matchmakingSeasonID")]
    public void GetSiteLink_WithoutAnIDThePageNeeds_SaysWhichOne(SitePage page, string missing)
        => Assert.That(async () => await Tools().GetSiteLink(page), Throws.TypeOf<ToolException>().With.Message.Contains(missing));

    [Test]
    public void GetSiteLink_ForASeasonTeamOrMatchThatDoesNotExist_Refuses()
    {
        var tools = Tools();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(async () => await tools.GetSiteLink(SitePage.Season, seasonID: 99), Throws.TypeOf<ToolException>());
            Assert.That(async () => await tools.GetSiteLink(SitePage.Team, seasonID: 12, teamID: 99), Throws.TypeOf<ToolException>());
            Assert.That(async () => await tools.GetSiteLink(SitePage.Team, seasonID: 12, teamID: 6), Throws.TypeOf<ToolException>(), "a team of another season, hidden here");
            Assert.That(async () => await tools.GetSiteLink(SitePage.SeasonMatch, seasonMatchID: 9999), Throws.TypeOf<ToolException>());
        }
    }

    [Test]
    public async Task GetSiteLink_ForASeasonNotAnnouncedYet_IsStaffsAlone()
    {
        var link = async (LinkTools tools) => new[]
        {
            await Try(() => tools.GetSiteLink(SitePage.Season, seasonID: 13)),
            await Try(() => tools.GetSiteLink(SitePage.SeasonDraft, seasonID: 13)),
            await Try(() => tools.GetSiteLink(SitePage.Team, seasonID: 13, teamID: 6)),
            await Try(() => tools.GetSiteLink(SitePage.TeamHistory, teamID: 6)),
            await Try(() => tools.GetSiteLink(SitePage.SeasonMatch, seasonMatchID: _hiddenSeasonMatchID)),
        };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await link(Tools()), Is.All.Null, "the public");
            Assert.That(await link(Tools(roles: SiteRoles.Player)), Is.All.Null, "a player");
            Assert.That(await link(Tools(roles: SiteRoles.Commissioner)), Is.All.Not.Null, "staff");
        }
    }

    private static async Task<string?> Try(Func<Task<string>> link)
    {
        try
        {
            return await link();
        }
        catch (ToolException)
        {
            return null;
        }
    }

    [Test]
    public void GetSiteLink_ForAPageTheSiteDoesNotHave_Refuses()
        => Assert.That(async () => await Tools().GetSiteLink((SitePage)999), Throws.TypeOf<ToolException>().With.Message.Contains("no page"));

    [Test]
    public void TheSiteChat_GivesPathsOnTheSite_ButTheOtherHostsDoNot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ChatHost.Site.RelativeLinks, Is.True);
            Assert.That(ChatHost.Discord.RelativeLinks, Is.False);
            Assert.That(ChatHost.DiscordVoice.RelativeLinks, Is.False);
        }
    }

    [Test]
    public void TheChatToolsIncludeGetSiteLink_AndTheSitePromptTellsTheModelToUseIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifTools.Chat, Does.Contain("get_site_link"));
            Assert.That(ChatHost.Site.More, Does.Contain("get_site_link"));
        }
    }
}
