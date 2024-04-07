using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Teams;

[Route("api/[controller]/[action]")]
[ApiController]
public class TeamsController : ControllerBase
{
    private readonly TeamService _teamService;

    public TeamsController(TeamService teamService)
    {
        _teamService = teamService;
    }

    [HttpGet("{seasonID:int}", Name = "GetTeams")]
    public Task<List<TeamResponseDto>> GetTeams([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.GetTeams(seasonID, ct);
    }

    [HttpGet("{seasonID:int}", Name = "GetPlayerPool")]
    public Task<List<PlayerDto>> GetPlayerPool([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.GetPlayerPool(seasonID, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpPost(Name = "AddCaptain")]
    public Task AddCaptain([FromBody] CaptainPlacementDto dto, CancellationToken ct)
    {
        return _teamService.AddCaptain(dto, resortOnly: false, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpPost(Name = "ResortCaptain")]
    public Task ResortCaptain([FromBody] CaptainPlacementDto dto, CancellationToken ct)
    {
        return _teamService.AddCaptain(dto, resortOnly: true, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpPost(Name = "RemoveCaptain")]
    public Task RemoveCaptain([FromBody] RemoveCaptainDto dto, CancellationToken ct)
    {
        return _teamService.RemoveCaptain(dto, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpPost(Name = "RemovePlayerFromTeam")]
    public Task RemovePlayerFromTeam([FromBody] RemovePlayerFromTeamRequestDto dto, CancellationToken ct)
    {
        return _teamService.RemovePlayerFromTeam(dto, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpPost(Name = "MovePlayerToTeam")]
    public Task MovePlayerToTeam([FromBody] MovePlayerToTeamRequestDto dto, CancellationToken ct)
    {
        return _teamService.MovePlayerToTeam(dto, ct);
    }

    [Authorize(Roles = "EventOrganizer,Player")]
    [HttpPost(Name = "AddPlayerToTeam")]
    public Task AddPlayerToTeam([FromBody] AddPlayerToTeamRequestDto dto, CancellationToken ct)
    {
        // Need check that player is captain
        return _teamService.AddPlayerToTeam(dto, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpGet("{seasonID:int}", Name = "LockCaptains")]
    public Task LockCaptains([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.LockCaptains(seasonID, @lock: true, ct);
    }

    [Authorize(Roles = "EventOrganizer")]
    [HttpGet("{seasonID:int}", Name = "UnlockCaptains")]
    public Task UnlockCaptains([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.LockCaptains(seasonID, @lock: false, ct);
    }

    [HttpGet("{seasonID:int}", Name = "AreCaptainsLocked")]
    public Task<bool> AreCaptainsLocked([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.AreCaptainsLocked(seasonID, ct);
    }
}
