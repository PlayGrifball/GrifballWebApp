using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Application.Matchmaking;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Infrastructure.Preview;

namespace GrifballWebApp.Server.Matchmaking;

/// <summary>The matchmaking queue as the signed-in user (if any) sees it.</summary>
public class QueueStatusDto
{
    /// <summary>How many players are in the queue.</summary>
    public required int Count { get; set; }
    /// <summary>
    /// Matchmaking runs here, so joining is allowed: not in a preview without Discord, where the matchmaking tick is off
    /// and nobody who joined would ever be matched.
    /// </summary>
    public required bool Open { get; set; }
    public required bool SignedIn { get; set; }
    /// <summary>The user has a gamertag linked, which joining needs.</summary>
    public required bool HasGamertag { get; set; }
    public required bool InQueue { get; set; }
    public required bool InMatch { get; set; }
}

/// <summary>
/// The matchmaking queue from the site: its size for everyone, and joining and leaving for signed-in players, on the
/// Discord buttons' rules (<see cref="QueueMembership"/>). Matches are made by the worker's matchmaking tick, which
/// also updates the queue's Discord message, within 10 seconds of a change made here.
/// </summary>
[Route("[controller]/[action]")]
[ApiController]
public class QueueController : ControllerBase
{
    private readonly QueueMembership _membership;
    private readonly IQueueRepository _queue;
    private readonly bool _open;

    public QueueController(QueueMembership membership, IQueueRepository queue, IConfiguration configuration)
    {
        _membership = membership;
        _queue = queue;
        // The matchmaking tick (MatchmakingQueueJob) runs unless Discord is off
        _open = !PreviewMode.DisablesDiscord(configuration);
    }

    [HttpGet(Name = "GetQueueStatus")]
    public Task<QueueStatusDto> Status(CancellationToken ct) => StatusOf(JobUser.IdOf(User), ct);

    [Authorize]
    [HttpPost(Name = "JoinQueue")]
    public async Task<ActionResult<QueueStatusDto>> Join(CancellationToken ct)
    {
        if (JobUser.IdOf(User) is not int userId)
            return Unauthorized();
        if (!_open)
            return Conflict(QueueMessages.LateLeagueOff);
        var (result, count) = await _membership.Join(userId, ct);
        if (result is not QueueJoinResult.Joined)
            return Conflict(QueueMessages.For(result));
        return Joined(count);
    }

    /// <summary>Allowed even where matchmaking is off: nobody should be stuck in the queue.</summary>
    [Authorize]
    [HttpPost(Name = "LeaveQueue")]
    public async Task<ActionResult<QueueStatusDto>> Leave(CancellationToken ct)
    {
        if (JobUser.IdOf(User) is not int userId)
            return Unauthorized();
        var (result, _) = await _membership.Leave(userId, ct);
        if (result is not QueueLeaveResult.Left)
            return Conflict(QueueMessages.For(result));
        // Read again, not assumed: leaving needs no gamertag, so the player may have none
        return await StatusOf(userId, ct);
    }

    /// <summary>After a join: the player has a gamertag and is not in a match, or it would have been refused.</summary>
    private QueueStatusDto Joined(int count)
        => new() { Count = count, Open = _open, SignedIn = true, HasGamertag = true, InQueue = true, InMatch = false };

    /// <summary>The signed-in player's part is one query, with the size (<see cref="IQueueRepository.PlayerState"/>).</summary>
    private async Task<QueueStatusDto> StatusOf(int? userId, CancellationToken ct)
    {
        if (userId is int id && await _queue.PlayerState(id, ct) is { } player)
        {
            return new QueueStatusDto
            {
                Count = player.QueueCount,
                Open = _open,
                SignedIn = true,
                HasGamertag = player.HasGamertag,
                InQueue = player.InQueue,
                InMatch = player.InMatch,
            };
        }

        // Signed out, or signed in as a user that is gone
        return new QueueStatusDto { Count = await _queue.Count(ct), Open = _open, SignedIn = userId is not null, HasGamertag = false, InQueue = false, InMatch = false };
    }
}
