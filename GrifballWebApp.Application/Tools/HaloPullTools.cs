using System.ComponentModel;
using GrifballWebApp.Application.Identity;
using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.SeasonMatchPage;
using GrifballWebApp.Application.XboxAccounts;

namespace GrifballWebApp.Application.Tools;

/// <summary>
/// Pulls from Halo, as the season match page's and the Xbox account page's buttons start them: queued as background jobs, one
/// per match or account at a time. For the roles the pages let pull. The slash commands (/match pull, /player pull) call these
/// too.
/// </summary>
[ToolRoles(SiteRoles.Player, SiteRoles.Commissioner, SiteRoles.Sysadmin)]
public class HaloPullTools
{
    private readonly ToolCaller _caller;
    private readonly SeasonMatchService _seasonMatches;
    private readonly ISeasonMatchPulls _pulls;
    private readonly XboxAccountService _accounts;
    private readonly IXboxAccountPulls _accountPulls;

    public HaloPullTools(ToolCaller caller, SeasonMatchService seasonMatches, ISeasonMatchPulls pulls, XboxAccountService accounts, IXboxAccountPulls accountPulls)
    {
        _caller = caller;
        _seasonMatches = seasonMatches;
        _pulls = pulls;
        _accounts = accounts;
        _accountPulls = accountPulls;
    }

    [Description("Queues a pull of a season match's players' recent Grifball games from Halo, as the match page's button does, so the games "
        + "can be reported (and get_recent_games shows them). It runs in the background, one pull per match at a time: the answer says whether "
        + "it is queued or running, and its last run. Players, commissioners and sysadmins may.")]
    public async Task<OnDemandJobStatusDto> PullSeasonMatchGames(
        [Description("The season match's ID (schedules and team matches list them).")] int seasonMatchID,
        [Description("Back through each player's history to the season's start, for an old match whose games are past their recent ones.")] bool backToSeasonStart = false,
        CancellationToken ct = default)
    {
        // The roles its [ToolRoles] names: a host's filtering isn't the only guard
        _caller.Require(GrifTools.RolesByName["pull_season_match_games"]);
        if (!await _seasonMatches.Exists(seasonMatchID, ct))
            throw new ToolException($"There is no season match {seasonMatchID}.");
        return await _pulls.Queue(seasonMatchID, _caller.UserID, backToSeasonStart, ct);
    }

    [Description("Queues a pull of an Xbox account's recent Grifball games from Halo (up to 11 pages of 25), as the account's page button does, "
        + "so get_recent_games shows them. It runs in the background, one pull per account at a time: the answer says whether it is queued or "
        + "running, and its last run. The site must know the account (search). Players, commissioners and sysadmins may.")]
    public async Task<PulledAccountDto> PullXboxAccountGames(
        [Description("The account's Xbox gamertag, current or past.")] string gamertag,
        CancellationToken ct = default)
    {
        _caller.Require(GrifTools.RolesByName["pull_xbox_account_games"]);
        var account = await _accounts.FindByGamertagAsync(gamertag, ct)
            ?? throw new ToolException($"The site knows no Xbox account with the gamertag {gamertag.Trim()}: search finds the players it knows.");
        return await PullXboxAccountGamesOf(account.XboxUserID, account.Gamertag, ct);
    }

    /// <summary>pull_xbox_account_games for an account already found (by its XUID: /player pull, whose option picks one).</summary>
    public async Task<PulledAccountDto> PullXboxAccountGamesOf(long xuid, string gamertag, CancellationToken ct = default)
    {
        _caller.Require(GrifTools.RolesByName["pull_xbox_account_games"]);
        if (!await _accounts.Exists(xuid, ct))
            throw new ToolException($"The site knows no Xbox account {xuid}: search finds the players it knows.");
        return new PulledAccountDto(xuid, gamertag, await _accountPulls.Queue(xuid, _caller.UserID, ct));
    }
}

/// <summary>The account a pull was queued for (its XUID and gamertag), and where the pull is.</summary>
public sealed record PulledAccountDto(long XboxUserID, string Gamertag, OnDemandJobStatusDto Pull);
