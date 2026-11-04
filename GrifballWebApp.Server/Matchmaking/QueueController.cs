using GrifballWebApp.Server.Preview;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    public Task<QueueStatusDto> Status(CancellationToken ct) => StatusOf(GetUserID(), ct);

    [Authorize]
    [HttpPost(Name = "JoinQueue")]
    public async Task<ActionResult<QueueStatusDto>> Join(CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return Unauthorized();
        if (!_open)
            return Conflict(QueueMessages.MatchmakingOff);
        var (result, count) = await _membership.Join(userId, ct);
        if (result is not QueueJoinResult.Joined)
            return Conflict(QueueMessages.For(result));
        return Player(count, inQueue: true);
    }

    /// <summary>Allowed even where matchmaking is off: nobody should be stuck in the queue.</summary>
    [Authorize]
    [HttpPost(Name = "LeaveQueue")]
    public async Task<ActionResult<QueueStatusDto>> Leave(CancellationToken ct)
    {
        if (GetUserID() is not int userId)
            return Unauthorized();
        var (result, count) = await _membership.Leave(userId, ct);
        if (result is not QueueLeaveResult.Left)
            return Conflict(QueueMessages.For(result));
        return Player(count, inQueue: false);
    }

    /// <summary>After a join or leave: the player has a gamertag and is not in a match, or it would have been refused.</summary>
    private QueueStatusDto Player(int count, bool inQueue)
        => new() { Count = count, Open = _open, SignedIn = true, HasGamertag = true, InQueue = inQueue, InMatch = false };

    private async Task<QueueStatusDto> StatusOf(int? userId, CancellationToken ct)
    {
        var count = await _queue.Count(ct);
        if (userId is not int id)
            return new QueueStatusDto { Count = count, Open = _open, SignedIn = false, HasGamertag = false, InQueue = false, InMatch = false };

        return new QueueStatusDto
        {
            Count = count,
            Open = _open,
            SignedIn = true,
            HasGamertag = await _membership.HasGamertag(id, ct),
            InQueue = await _queue.GetQueuePlayer(id, ct) is not null,
            InMatch = await _queue.IsInMatch(id, ct),
        };
    }

    private int? GetUserID()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
