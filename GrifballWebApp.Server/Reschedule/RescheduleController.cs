using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GrifballWebApp.Server.Reschedule;

[Route("[controller]/[action]")]
[ApiController]
[Authorize]
public class RescheduleController : ControllerBase
{
    private readonly RescheduleService _rescheduleService;

    public RescheduleController(RescheduleService rescheduleService)
    {
        _rescheduleService = rescheduleService;
    }

    [HttpPost(Name = "RequestReschedule")]
    public async Task<IActionResult> RequestReschedule([FromBody] RescheduleRequestDto dto, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        try
        {
            var reschedule = await _rescheduleService.RequestRescheduleAsync(dto, userId, ct);
            return Ok(new { RescheduleId = reschedule.MatchRescheduleID, Message = "Reschedule request submitted successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{rescheduleId:int}", Name = "ProcessReschedule")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    public async Task<IActionResult> ProcessReschedule(int rescheduleId, [FromBody] ProcessRescheduleDto dto, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var reschedule = await _rescheduleService.ProcessRescheduleAsync(rescheduleId, dto, userId, ct);
            return Ok(new { Message = $"Reschedule request {(dto.Approved ? "approved" : "rejected")} successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet(Name = "GetPendingReschedules")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    public async Task<IActionResult> GetPendingReschedules(CancellationToken ct)
    {
        var reschedules = await _rescheduleService.GetPendingReschedulesAsync(ct);
        return Ok(reschedules);
    }

    [HttpPost("{rescheduleId:int}/thread", Name = "CreateDiscordThread")]
    [Authorize(Roles = "Commissioner,Sysadmin")]
    public async Task<IActionResult> CreateDiscordThread(int rescheduleId, [FromBody] CreateThreadDto dto, CancellationToken ct)
    {
        try
        {
            var reschedule = await _rescheduleService.CreateDiscordThreadAsync(rescheduleId, dto.ChannelId, ct);
            if (reschedule?.DiscordThreadID.HasValue == true)
            {
                return Ok(new { ThreadId = reschedule.DiscordThreadID.Value });
            }
            return BadRequest("Failed to create Discord thread");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating Discord thread: {ex.Message}");
        }
    }
}