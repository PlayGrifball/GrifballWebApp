using System.Linq.Expressions;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Server.TeamPage;

/// <summary>
/// A team's player as the database reads it (<see cref="Of"/>), and the roster every page of a team shows from it
/// (<see cref="ToPlayers"/>): the team page and the franchise's history alike.
/// </summary>
public class RosterPlayer
{
    public int TeamPlayerID { get; set; }
    public int UserID { get; set; }
    public string? Gamertag { get; set; }
    public string? Discord { get; set; }
    public string? DisplayName { get; set; }
    public string? UserName { get; set; }
    public int? DraftCaptainOrder { get; set; }
    public int? DraftRound { get; set; }
    public bool IsCaptain { get; set; }
    public string? Region { get; set; }

    /// <summary>Use on a team's players as <c>t.TeamPlayers.AsQueryable().Select(RosterPlayer.Of)</c>.</summary>
    public static readonly Expression<Func<TeamPlayer, RosterPlayer>> Of = p => new RosterPlayer
    {
        TeamPlayerID = p.TeamPlayerID,
        UserID = p.UserID,
        Gamertag = p.User.XboxUser != null ? p.User.XboxUser.Gamertag : null,
        Discord = p.User.DiscordUser != null ? p.User.DiscordUser.DiscordUsername : null,
        DisplayName = p.User.DisplayName,
        UserName = p.User.UserName,
        DraftCaptainOrder = p.DraftCaptainOrder,
        DraftRound = p.DraftRound,
        IsCaptain = p.CaptainTeam != null || p.DraftCaptainOrder != null,
        Region = p.User.Region != null ? p.User.Region.RegionName : null,
    };

    /// <summary>The roster: captain first, then by draft round, then by name.</summary>
    public static PlayerDto[] ToPlayers(IEnumerable<RosterPlayer> players) => players.Select(p => new PlayerDto
    {
        TeamPlayerID = p.TeamPlayerID,
        UserID = p.UserID,
        // As the app names a user everywhere: a placeholder of an imported season has no Xbox account
        Gamertag = UserNames.Pick(p.Gamertag, p.Discord, p.DisplayName, p.UserName),
        DraftCaptainOrder = p.DraftCaptainOrder,
        DraftRound = p.DraftRound,
        IsCaptain = p.IsCaptain,
        Region = p.Region,
    }).OrderBy(x => x.IsCaptain ? 0 : 1).ThenBy(x => x.DraftRound).ThenBy(x => x.Gamertag).ToArray();
}
