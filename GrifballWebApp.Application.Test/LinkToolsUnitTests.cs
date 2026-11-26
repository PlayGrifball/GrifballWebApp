using GrifballWebApp.Application.Tools;

namespace GrifballWebApp.Application.Test;

[TestFixture]
public class LinkToolsUnitTests
{
    private static readonly Guid Game = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private static LinkTools Tools(bool relative = false)
        => new(ToolCaller.Anonymous with { RelativeLinks = relative }, new UrlService("https://grif.test"));

    [TestCase(SitePage.Seasons, null, null, null, null, null, null, null, "https://grif.test/seasons")]
    [TestCase(SitePage.Leaderboards, null, null, null, null, null, null, null, "https://grif.test/leaderboards")]
    [TestCase(SitePage.LateLeague, null, null, null, null, null, null, null, "https://grif.test/late-league")]
    [TestCase(SitePage.LateLeagueSeason, null, null, null, null, 4, null, null, "https://grif.test/late-league/4")]
    [TestCase(SitePage.Season, 12, null, null, null, null, null, null, "https://grif.test/season/12")]
    [TestCase(SitePage.SeasonSignups, 12, null, null, null, null, null, null, "https://grif.test/season/12/signups")]
    [TestCase(SitePage.SeasonDraft, 12, null, null, null, null, null, null, "https://grif.test/season/12/draft")]
    [TestCase(SitePage.PlayerGrades, 12, null, null, null, null, null, null, "https://grif.test/season/12/playergrades")]
    [TestCase(SitePage.Team, 12, 5, null, null, null, null, null, "https://grif.test/season/12/team/5")]
    [TestCase(SitePage.TeamHistory, null, 5, null, null, null, null, null, "https://grif.test/history/team/5")]
    [TestCase(SitePage.SeasonMatch, null, null, 8, null, null, null, null, "https://grif.test/seasonmatch/8")]
    [TestCase(SitePage.Profile, null, null, null, 3, null, null, null, "https://grif.test/profile/3")]
    [TestCase(SitePage.XboxAccount, null, null, null, null, null, null, 2533274790000001L, "https://grif.test/xbox/2533274790000001")]
    public void GetSiteLink_BuildsThePageFromUrlService_Absolute(SitePage page, int? seasonID, int? teamID, int? seasonMatchID, int? userID,
        int? matchmakingSeasonID, Guid? gameID, long? xboxUserID, string expected)
        => Assert.That(Tools().GetSiteLink(page, seasonID, teamID, seasonMatchID, userID, matchmakingSeasonID, gameID, xboxUserID), Is.EqualTo(expected));

    [Test]
    public void GetSiteLink_ForTheSitesOwnChat_IsAPathOnTheSite()
    {
        var tools = Tools(relative: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tools.GetSiteLink(SitePage.Season, seasonID: 12), Is.EqualTo("/season/12"));
            Assert.That(tools.GetSiteLink(SitePage.Team, seasonID: 12, teamID: 5), Is.EqualTo("/season/12/team/5"));
            Assert.That(tools.GetSiteLink(SitePage.Leaderboards), Is.EqualTo("/leaderboards"));
            Assert.That(tools.GetSiteLink(SitePage.Game, gameID: Game), Is.EqualTo($"/match/{Game}"));
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
        => Assert.That(() => Tools().GetSiteLink(page), Throws.TypeOf<ToolException>().With.Message.Contains(missing));

    [Test]
    public void TheSiteChat_GivesPathsOnTheSite_ButTheOtherHostsDoNot()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Chat.ChatHost.Site.RelativeLinks, Is.True);
            Assert.That(Chat.ChatHost.Discord.RelativeLinks, Is.False);
            Assert.That(Chat.ChatHost.DiscordVoice.RelativeLinks, Is.False);
        }
    }

    [Test]
    public void TheChatToolsIncludeGetSiteLink_AndTheSitePromptTellsTheModelToUseIt()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(GrifTools.Chat, Does.Contain("get_site_link"));
            Assert.That(Chat.ChatHost.Site.More, Does.Contain("get_site_link"));
        }
    }
}
