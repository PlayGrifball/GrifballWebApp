using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using GrifballWebApp.Application.Seasons;
using GrifballWebApp.Application.Teams;
using TypedSignalR.Client;

namespace GrifballWebApp.Server.Teams;

public class TeamsHub : Hub<ITeamsHubClient>, ITeamsHubServer
{
    /// <summary>Staff's connections (commissioners, sysadmins): the drafts of seasons not announced yet go to them alone (#435).</summary>
    public const string StaffGroup = "season-staff";

    private readonly TeamService _teamService;
    private readonly SeasonVisibility _visibility;
    //private static ConcurrentDictionary<string, int?> _connections = new ConcurrentDictionary<string, int?>();

    public TeamsHub(TeamService teamService, SeasonVisibility visibility)
    {
        _teamService = teamService;
        _visibility = visibility;
    }

    /// <summary>The draft of a season the caller may not see (not announced yet, to anyone but staff): refused as no such season (#435).</summary>
    private async Task RequireVisible(int seasonID)
    {
        if (!await _visibility.CanSeeSeason(seasonID, SeasonVisibility.For(Context?.User)))
            throw new HubException(GrifballWebApp.Server.Seasons.SeasonVisibilityFilter.NoSuchSeason);
    }

    //private IEnumerable<string> GetConnections(int? personID)
    //{
    //    return _connections.Where(x => x.Value == personID).Select(x => x.Key);
    //}

    public override async Task OnConnectedAsync()
    {
        if (SeasonVisibility.For(Context.User) is SeasonViewer.Staff)
            await Groups.AddToGroupAsync(Context.ConnectionId, StaffGroup);
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        //var isAuth = Context.User?.Identity?.IsAuthenticated ?? false;
        //var removed = _connections.Remove(Context.ConnectionId, out var personID);
        return base.OnDisconnectedAsync(exception);
    }

    public async Task<List<TeamResponseDto>> GetTeams(int seasonID)
    {
        await RequireVisible(seasonID);
        return await _teamService.GetTeams(seasonID);
    }

    public async Task<List<PlayerDto>> GetPlayerPool(int seasonID)
    {
        await RequireVisible(seasonID);
        return await _teamService.GetPlayerPool(seasonID);
    }

    [Authorize(Roles = "Commissioner")]
    public Task AddCaptain(CaptainPlacementDto dto)
    {
        return _teamService.AddCaptain(dto, resortOnly: false, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner")]
    public Task ResortCaptain(CaptainPlacementDto dto)
    {
        return _teamService.AddCaptain(dto, resortOnly: true, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner")]
    public Task RemoveCaptain(RemoveCaptainDto dto)
    {
        return _teamService.RemoveCaptain(dto, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner")]
    public Task RemovePlayerFromTeam(RemovePlayerFromTeamRequestDto dto)
    {
        return _teamService.RemovePlayerFromTeam(dto, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner")]
    public Task MovePlayerToTeam(MovePlayerToTeamRequestDto dto)
    {
        return _teamService.MovePlayerToTeam(dto, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner,Player")]
    public Task AddPlayerToTeam(AddPlayerToTeamRequestDto dto)
    {
        var userID = GetUserID();
        if (userID is null)
            throw new Exception("You must be logged in to add a player to a team");

        return _teamService.AddPlayerToTeam(dto, userID.Value, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner")]
    public Task LockCaptains(int seasonID)
    {
        return _teamService.LockCaptains(seasonID, @lock: true, Context.ConnectionId);
    }

    [Authorize(Roles = "Commissioner")]
    public Task UnlockCaptains(int seasonID)
    {
        return _teamService.LockCaptains(seasonID, @lock: false, Context.ConnectionId);
    }

    public async Task<bool> AreCaptainsLocked(int seasonID)
    {
        await RequireVisible(seasonID);
        return await _teamService.AreCaptainsLocked(seasonID);
    }

    private int? GetUserID()
    {
        var userIDStr = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIDStr == null)
            return null;
        var parsed = int.TryParse(userIDStr, out var userID);
        return parsed ? userID : null;
    }
}

/// <summary>
/// Methods that can be invoked on front end client (the typed client's receiver: grifballwebapp.client/src/generated/signalr)
/// </summary>
[Receiver]
public interface ITeamsHubClient
{
    Task AddCaptain(CaptainAddedDto dto);
    Task ResortCaptain(CaptainPlacementDto dto);
    Task RemoveCaptain(RemoveCaptainDto dto);
    Task RemovePlayerFromTeam(RemovePlayerFromTeamRequestDto dto);
    Task MovePlayerToTeam(MovePlayerToTeamRequestDto dto);
    Task AddPlayerToTeam(AddPlayerToTeamRequestDto dto);
    Task LockCaptains(int seasonID);
    Task UnlockCaptains(int seasonID);
}

/// <summary>
/// Methods that can be invoked on back end server: the draft's changes over SignalR, as over REST (the typed client's hub
/// proxy)
/// </summary>
[Hub]
public interface ITeamsHubServer
{
    Task<List<TeamResponseDto>> GetTeams(int seasonID);
    Task<List<PlayerDto>> GetPlayerPool(int seasonID);
    Task AddCaptain(CaptainPlacementDto dto);
    Task ResortCaptain(CaptainPlacementDto dto);
    Task RemoveCaptain(RemoveCaptainDto dto);
    Task RemovePlayerFromTeam(RemovePlayerFromTeamRequestDto dto);
    Task MovePlayerToTeam(MovePlayerToTeamRequestDto dto);
    Task AddPlayerToTeam(AddPlayerToTeamRequestDto dto);
    Task LockCaptains(int seasonID);
    Task UnlockCaptains(int seasonID);
    Task<bool> AreCaptainsLocked(int seasonID);
}
