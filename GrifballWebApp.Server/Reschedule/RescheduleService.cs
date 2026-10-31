using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GrifballWebApp.Server.Reschedule;

public class RescheduleService
{
    private readonly GrifballContext _context;
    private readonly IOutbox _outbox;
    private readonly ILogger<RescheduleService> _logger;
    private readonly IOptions<DiscordOptions> _options;

    public RescheduleService(GrifballContext context, IOutbox outbox, ILogger<RescheduleService> logger, IOptions<DiscordOptions> options)
    {
        _context = context;
        _outbox = outbox;
        _logger = logger;
        _options = options;
    }

    public async Task<MatchReschedule> RequestRescheduleAsync(RescheduleRequestDto dto, int requestedByUserId, CancellationToken ct = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Get the season match
            var seasonMatch = await _context.SeasonMatches
                .Include(sm => sm.HomeTeam)
                    .ThenInclude(t => t.Captain.User.XboxUser)
                .Include(sm => sm.AwayTeam)
                    .ThenInclude(t => t.Captain.User.XboxUser)
                .FirstOrDefaultAsync(sm => sm.SeasonMatchID == dto.SeasonMatchID, ct);

            if (seasonMatch == null)
                throw new ArgumentException("Season match not found");

            // Only an undecided match between two teams, and only by a player on either team or a commissioner
            if (RescheduleRules.WhyNotReschedulable(seasonMatch) is { } reason)
                throw new RescheduleNotAllowedException(reason);
            if (!await RescheduleRules.MayRequestAsync(_context, seasonMatch, requestedByUserId, ct))
                throw new RescheduleForbiddenException();

            // Prevent new request if there is already an active one
            if (seasonMatch.ActiveRescheduleRequestId.HasValue)
                throw new RescheduleAlreadyActiveException();

            // Create the reschedule request
            var reschedule = new MatchReschedule
            {
                SeasonMatchID = dto.SeasonMatchID,
                OriginalScheduledTime = seasonMatch.ScheduledTime,
                NewScheduledTime = dto.NewScheduledTime,
                Reason = dto.Reason,
                RequestedByUserID = requestedByUserId,
                Status = RescheduleStatus.Pending
            };

            _context.MatchReschedules.Add(reschedule);
            await _context.SaveChangesAsync(ct);

            // Set the active request ID on the match
            seasonMatch.ActiveRescheduleRequestId = reschedule.MatchRescheduleID;
            await _context.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return reschedule;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<MatchReschedule> ProcessRescheduleAsync(int rescheduleId, ProcessRescheduleDto dto, int commissionerUserId, CancellationToken ct = default)
    {
        // The decision and the message telling its thread are committed together; the outbox sends the message.
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            var reschedule = await _context.MatchReschedules
                .Include(mr => mr.SeasonMatch)
                .FirstOrDefaultAsync(mr => mr.MatchRescheduleID == rescheduleId, ct);

            if (reschedule == null)
                throw new ArgumentException("Reschedule request not found");

            if (reschedule.Status != RescheduleStatus.Pending)
                throw new RescheduleAlreadyProcessedException();

            reschedule.Status = dto.Approved ? RescheduleStatus.Approved : RescheduleStatus.Rejected;
            reschedule.ApprovedByUserID = commissionerUserId;
            reschedule.ProcessedAt = DateTime.UtcNow;
            reschedule.CommissionerNotes = dto.CommissionerNotes;

            // If approved, update the season match scheduled time
            if (dto.Approved && reschedule.NewScheduledTime.HasValue)
            {
                reschedule.SeasonMatch.ScheduledTime = reschedule.NewScheduledTime;
            }

            // Clear the active request if processed
            reschedule.SeasonMatch.ActiveRescheduleRequestId = null;

            // Told in its thread, if it has one or one is on its way.
            if (reschedule.DiscordThreadID is not null || await ThreadQueuedAsync(rescheduleId, ct))
            {
                var approver = await ApproverNameAsync(commissionerUserId, ct);
                await _outbox.EnqueueDecisionAsync(rescheduleId, $"Your request has been {reschedule.Status} by {approver}", ct);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return reschedule;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// The approver as the thread should see them: a mention when their Discord account is linked (as the other Discord
    /// notifications name users), otherwise their gamertag, Discord name or display name.
    /// </summary>
    private async Task<string> ApproverNameAsync(int userID, CancellationToken ct)
    {
        var user = await _context.Users
            .Include(u => u.XboxUser)
            .Include(u => u.DiscordUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userID, ct);

        if (user is null)
            return "a commissioner";
        if (user.DiscordUserID is { } discordUserID)
            return $"<@{discordUserID}>";
        return user.ToDisplayName() ?? user.UserName ?? "a commissioner";
    }

    public async Task<List<RescheduleDto>> GetPendingReschedulesAsync(CancellationToken ct = default)
    {
        var pendingReschedules = await _context.MatchReschedules
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.HomeTeam)
                    .ThenInclude(t => t.Captain.User.XboxUser)
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.AwayTeam)
                    .ThenInclude(t => t.Captain.User.XboxUser)
            .Include(mr => mr.RequestedByUser.XboxUser)
            .Where(mr => mr.Status == RescheduleStatus.Pending)
            .OrderBy(mr => mr.RequestedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return pendingReschedules.Select(mr => new RescheduleDto
        {
            MatchRescheduleID = mr.MatchRescheduleID,
            SeasonMatchID = mr.SeasonMatchID,
            HomeCaptain = mr.SeasonMatch.HomeTeam?.Captain?.User.ToDisplayName() ?? "UNKNOWN",
            AwayCaptain = mr.SeasonMatch.AwayTeam?.Captain?.User.ToDisplayName() ?? "UNKNOWN",
            OriginalScheduledTime = mr.OriginalScheduledTime,
            NewScheduledTime = mr.NewScheduledTime,
            Reason = mr.Reason,
            RequestedByGamertag = mr.RequestedByUser.ToDisplayName() ?? "UNKNOWN",
            RequestedAt = mr.RequestedAt,
            Status = mr.Status,
            DiscordThreadID = mr.DiscordThreadID
        }).ToList();
    }

    public async Task<List<OverdueMatchDto>> GetOverdueMatchesAsync(CancellationToken ct = default)
    {
        var cutoffDate = DateTime.UtcNow.AddHours(-24); // Consider matches overdue after 24 hours

        var overdueMatches = await _context.SeasonMatches
            .Include(sm => sm.HomeTeam.Captain.User.XboxUser)
            .Include(sm => sm.AwayTeam.Captain.User.XboxUser)
            // A match without both teams (a bracket match still waiting on an earlier result) cannot be played yet,
            // so it is not overdue, whatever time it was given.
            .Where(sm => sm.ScheduledTime.HasValue
                && sm.HomeTeamID != null
                && sm.AwayTeamID != null
                && sm.ScheduledTime < cutoffDate
                && sm.HomeTeamResult == null 
                && sm.AwayTeamResult == null)
            .OrderBy(sm => sm.ScheduledTime)
            .AsNoTracking()
            .ToListAsync(ct);

        return overdueMatches.Select(sm => new OverdueMatchDto
        {
            SeasonMatchID = sm.SeasonMatchID,
            HomeCaptain = sm.HomeTeam?.Captain?.User.ToDisplayName() ?? "UNKNOWN",
            AwayCaptain = sm.AwayTeam?.Captain?.User.ToDisplayName() ?? "UNKNOWN",
            ScheduledTime = sm.ScheduledTime.Value,
            HoursOverdue = (int)Math.Floor((DateTime.UtcNow - sm.ScheduledTime.Value).TotalHours)
        }).ToList();
    }

    /// <summary>
    /// Queues the request's Discord thread on the outbox (posted, made and its captains and commissioners added once
    /// saved). Nothing when it has a thread already, or one is queued.
    /// </summary>
    public async Task CreateDiscordThreadAsync(int rescheduleId, CancellationToken ct = default)
    {
        var reschedule = await _context.MatchReschedules
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.HomeTeam.Captain.User.XboxUser)
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.AwayTeam.Captain.User.XboxUser)
            .Include(mr => mr.RequestedByUser.XboxUser)
            .FirstOrDefaultAsync(mr => mr.MatchRescheduleID == rescheduleId, ct);

        if (reschedule == null)
            throw new ArgumentException("Reschedule request not found");

        if (reschedule.DiscordThreadID.HasValue || await ThreadQueuedAsync(rescheduleId, ct))
            return; // Thread already exists, or is on its way

        // Create a message first to create thread from
        var messageContent = $"🔄 **Match Reschedule Request**\n" +
                            $"**Match:** {reschedule.SeasonMatch.HomeTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"} vs {reschedule.SeasonMatch.AwayTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"}\n" +
                            $"**Original Time:** {reschedule.OriginalScheduledTime?.DiscordTimeEmbed() ?? "Not scheduled"}\n" +
                            $"**Requested New Time:** {reschedule.NewScheduledTime?.DiscordTimeEmbed() ?? "TBD"}\n" +
                            $"**Requested By:** {reschedule.RequestedByUser.XboxUser?.Gamertag}\n" +
                            $"**Reason:** {reschedule.Reason}";

        var threadName = $"Reschedule: {reschedule.SeasonMatch.HomeTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"} vs {reschedule.SeasonMatch.AwayTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"}";

        await _outbox.EnqueueThreadAsync(rescheduleId, messageContent, threadName, ct);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>Whether the request's thread is queued and not given up on.</summary>
    private Task<bool> ThreadQueuedAsync(int rescheduleId, CancellationToken ct)
    {
        var group = RescheduleOutbox.Group(rescheduleId);
        return _context.Outbox.AnyAsync(x => x.GroupKey == group && x.Kind == RescheduleOutbox.ThreadCreate
            && x.Status != OutboxStatus.DeadLettered && x.Status != OutboxStatus.Skipped, ct);
    }
}
