using GrifballWebApp.Database;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Outbox;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
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
        // The match with its requests: whether one is pending is the match's to say
        var seasonMatch = await _context.SeasonMatches
            .Include(sm => sm.Reschedules)
            .FirstOrDefaultAsync(sm => sm.SeasonMatchID == dto.SeasonMatchID, ct);

        if (seasonMatch == null)
            throw new ArgumentException("Season match not found");

        // Only an undecided match between two teams, and only by a player on either team or a commissioner
        if (MatchReschedule.WhyNotReschedulable(seasonMatch) is { } reason)
            throw new RescheduleNotAllowedException(reason);
        if (!await RescheduleRules.MayRequestAsync(_context, seasonMatch, requestedByUserId, ct))
            throw new RescheduleForbiddenException();

        var requested = MatchReschedule.Request(seasonMatch, dto.NewScheduledTime, dto.Reason, requestedByUserId, DateTime.UtcNow);
        // The match can move (above): only a pending request of its refuses this one
        if (requested.Reschedule is not { } reschedule)
            throw new RescheduleAlreadyActiveException();

        // One save, so no transaction of its own. Two requests at once can both pass the check above: the unique pending index
        // keeps the first saved, and the other is told the match has one.
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            if (await PendingSavedAsync(dto.SeasonMatchID, ct))
                throw new RescheduleAlreadyActiveException();
            throw;
        }

        return reschedule;
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

            switch (reschedule.Decide(dto.Approved, commissionerUserId, dto.CommissionerNotes, DateTime.UtcNow))
            {
                case RescheduleDecision.AlreadyDecided:
                    throw new RescheduleAlreadyProcessedException();
                case RescheduleDecision.MatchNotReschedulable:
                    throw new RescheduleNotAllowedException(MatchReschedule.WhyNotReschedulable(reschedule.SeasonMatch)!);
            }

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
        return user.ToDisplayName() ?? "a commissioner";
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
            .Where(MatchReschedule.IsPending)
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
            RequestedByUserID = mr.RequestedByUser?.Id,
            RequestedAt = mr.RequestedAt,
            Status = mr.Status,
            DiscordThreadID = mr.DiscordThreadID,
            WhyNotApprovable = MatchReschedule.WhyNotReschedulable(mr.SeasonMatch),
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
                && sm.ScheduledTime < cutoffDate)
            .Where(SeasonMatch.Undecided)
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

    /// <summary>Whether the match has a pending request saved (read fresh, past this context's unsaved one).</summary>
    private Task<bool> PendingSavedAsync(int seasonMatchID, CancellationToken ct)
        => _context.MatchReschedules.AsNoTracking().Where(MatchReschedule.IsPending).AnyAsync(r => r.SeasonMatchID == seasonMatchID, ct);

    /// <summary>Whether the request's thread is queued and not given up on.</summary>
    private Task<bool> ThreadQueuedAsync(int rescheduleId, CancellationToken ct)
    {
        var group = RescheduleOutbox.Group(rescheduleId);
        return _context.Outbox.AnyAsync(x => x.GroupKey == group && x.Kind == RescheduleOutbox.ThreadCreate
            && x.Status != OutboxStatus.DeadLettered && x.Status != OutboxStatus.Skipped, ct);
    }
}
