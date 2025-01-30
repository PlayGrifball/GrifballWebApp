using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace GrifballWebApp.Server.Teams;

public class TeamsHub : Hub<ITeamsHubClient>, ITeamsHubServer
{
    private readonly TeamService _teamService;
    //private static ConcurrentDictionary<string, int?> _connections = new ConcurrentDictionary<string, int?>();

    public TeamsHub(TeamService teamService)
    {
        _teamService = teamService;
    }

    //private int? GetPersonID()
    //{
    //    var personIDStr = Context.User?.Claims?.FirstOrDefault(x => x.Type is "PersonID")?.Value;
    //    if (personIDStr is null)
    //        return null;
    //    else
    //        return Convert.ToInt32(personIDStr);
    //}

    //private IEnumerable<string> GetConnections(int? personID)
    //{
    //    return _connections.Where(x => x.Value == personID).Select(x => x.Key);
    //}

    public override Task OnConnectedAsync()
    {
        //var isAuth = Context.User?.Identity?.IsAuthenticated ?? false;
        //var personID = GetPersonID();
        //var added = _connections.TryAdd(Context.ConnectionId, personID);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        //var isAuth = Context.User?.Identity?.IsAuthenticated ?? false;
        //var removed = _connections.Remove(Context.ConnectionId, out var personID);
        return base.OnDisconnectedAsync(exception);
    }

    public Task<List<TeamResponseDto>> GetTeams(int seasonID)
    {
        return _teamService.GetTeams(seasonID);
    }

    public Task<List<PlayerDto>> GetPlayerPool(int seasonID)
    {
        return _teamService.GetPlayerPool(seasonID);
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
        return _teamService.AddPlayerToTeam(dto, Context.ConnectionId);
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

    public Task<bool> AreCaptainsLocked(int seasonID)
    {
        return _teamService.AreCaptainsLocked(seasonID);
    }
}

/// <summary>
/// Methods that can be invoked on front end client
/// </summary>
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
/// Methods that can be invoked on back end server
/// </summary>
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
