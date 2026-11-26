using System.ComponentModel;

namespace GrifballWebApp.Application.Tools;

/// <summary>The pages of the site a chat can link to, each with the IDs <see cref="LinkTools.GetSiteLink"/> takes for it.</summary>
public enum SitePage
{
    Seasons,
    Leaderboards,
    LateLeague,
    LateLeagueSeason,
    Season,
    SeasonSignups,
    SeasonDraft,
    PlayerGrades,
    Team,
    TeamHistory,
    SeasonMatch,
    Game,
    Profile,
    XboxAccount,
}

/// <summary>The site's own pages, as links: the route table is <see cref="UrlService"/>'s, so a chat never invents a path.</summary>
public class LinkTools
{
    private readonly ToolCaller _caller;
    private readonly UrlService _urls;

    public LinkTools(ToolCaller caller, UrlService urls)
    {
        _caller = caller;
        _urls = urls;
    }

    [Description("The link to a page of the league's site, from the IDs the other tools give: a season's page (and its signups, draft and player grades), a team's page in a season "
        + "or its history, a season match, a recorded game, a player's profile, an Xbox account, Late League, the seasons list or the leaderboards. "
        + "Give the link in your reply exactly as this returns it; never write a site link of your own.")]
    public string GetSiteLink([Description("Which page.")] SitePage page,
        [Description("The season's ID: for Season, SeasonSignups, SeasonDraft, PlayerGrades and Team.")] int? seasonID = null,
        [Description("The team's ID: for Team and TeamHistory.")] int? teamID = null,
        [Description("The season match's ID: for SeasonMatch.")] int? seasonMatchID = null,
        [Description("The player's user ID: for Profile.")] int? userID = null,
        [Description("The Late League season's ID: for LateLeagueSeason.")] int? matchmakingSeasonID = null,
        [Description("The game's match ID, a GUID: for Game.")] Guid? gameID = null,
        [Description("The Xbox account's ID: for XboxAccount.")] long? xboxUserID = null)
    {
        // The site's own chat gets paths it turns into links on the page; anywhere else (Discord, MCP) a whole address
        var urls = _caller.RelativeLinks ? UrlService.Relative : _urls;
        return page switch
        {
            SitePage.Seasons => urls.Page("seasons"),
            SitePage.Leaderboards => urls.Page("leaderboards"),
            SitePage.LateLeague => urls.Page("late-league"),
            SitePage.LateLeagueSeason => urls.Page($"late-league/{Need(matchmakingSeasonID, nameof(matchmakingSeasonID))}"),
            SitePage.Season => urls.Season(Need(seasonID, nameof(seasonID))),
            SitePage.SeasonSignups => urls.ViewSignups(Need(seasonID, nameof(seasonID))),
            SitePage.SeasonDraft => urls.Draft(Need(seasonID, nameof(seasonID))),
            SitePage.PlayerGrades => urls.Grades(Need(seasonID, nameof(seasonID))),
            SitePage.Team => urls.Team(Need(seasonID, nameof(seasonID)), Need(teamID, nameof(teamID))),
            SitePage.TeamHistory => urls.TeamHistory(Need(teamID, nameof(teamID))),
            SitePage.SeasonMatch => urls.SeasonMatch(Need(seasonMatchID, nameof(seasonMatchID))),
            SitePage.Game => urls.Match(Need(gameID, nameof(gameID))),
            SitePage.Profile => urls.Profile(Need(userID, nameof(userID))),
            SitePage.XboxAccount => urls.XboxAccount(Need(xboxUserID, nameof(xboxUserID))),
            _ => throw new ToolException($"There is no page {page}."),
        };
    }

    private static T Need<T>(T? id, string name) where T : struct
        => id ?? throw new ToolException($"That page needs {name}: the other tools give it.");
}
