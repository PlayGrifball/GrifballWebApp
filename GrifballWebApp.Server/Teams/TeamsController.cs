﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GrifballWebApp.Server.Teams;

[Route("[controller]/[action]")]
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

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "AddCaptain")]
    public Task AddCaptain([FromBody] CaptainPlacementDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.AddCaptain(dto, resortOnly: false, signalRConnectionID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "ResortCaptain")]
    public Task ResortCaptain([FromBody] CaptainPlacementDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.AddCaptain(dto, resortOnly: true, signalRConnectionID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "RemoveCaptain")]
    public Task RemoveCaptain([FromBody] RemoveCaptainDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.RemoveCaptain(dto, signalRConnectionID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "RemovePlayerFromTeam")]
    public Task RemovePlayerFromTeam([FromBody] RemovePlayerFromTeamRequestDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.RemovePlayerFromTeam(dto, signalRConnectionID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpPost(Name = "MovePlayerToTeam")]
    public Task MovePlayerToTeam([FromBody] MovePlayerToTeamRequestDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.MovePlayerToTeam(dto, signalRConnectionID, ct);
    }

    [Authorize(Roles = "Commissioner,Player")]
    [HttpPost(Name = "AddPlayerToTeam")]
    public async Task<IActionResult> AddPlayerToTeam([FromBody] AddPlayerToTeamRequestDto dto, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        var userID = GetUserID();
        if (userID == 0)
            return BadRequest("You must be logged in to add a player to a team");

        await _teamService.AddPlayerToTeam(dto, userID, signalRConnectionID, ct);

        return Ok();
    }

    [Authorize(Roles = "Commissioner")]
    [HttpGet("{seasonID:int}", Name = "LockCaptains")]
    public Task LockCaptains([FromRoute] int seasonID, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.LockCaptains(seasonID, @lock: true, signalRConnectionID, ct);
    }

    [Authorize(Roles = "Commissioner")]
    [HttpGet("{seasonID:int}", Name = "UnlockCaptains")]
    public Task UnlockCaptains([FromRoute] int seasonID, [FromHeader] string? signalRConnectionID, CancellationToken ct)
    {
        return _teamService.LockCaptains(seasonID, @lock: false, signalRConnectionID, ct);
    }

    [HttpGet("{seasonID:int}", Name = "AreCaptainsLocked")]
    public Task<bool> AreCaptainsLocked([FromRoute] int seasonID, CancellationToken ct)
    {
        return _teamService.AreCaptainsLocked(seasonID, ct);
    }

    private int GetUserID()
    {
        var userIDStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIDStr == null)
            return 0;
        int.TryParse(userIDStr, out var userID);
        return userID;
    }
}
