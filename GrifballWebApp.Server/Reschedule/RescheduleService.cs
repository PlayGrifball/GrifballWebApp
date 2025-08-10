using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Reschedule;

public class RescheduleService
{
    private readonly GrifballContext _context;
    private readonly IDiscordClient _discordClient;
    private readonly ILogger<RescheduleService> _logger;

    public RescheduleService(GrifballContext context, IDiscordClient discordClient, ILogger<RescheduleService> logger)
    {
        _context = context;
        _discordClient = discordClient;
        _logger = logger;
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
        using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            var reschedule = await _context.MatchReschedules
                .Include(mr => mr.SeasonMatch)
                .FirstOrDefaultAsync(mr => mr.MatchRescheduleID == rescheduleId, ct);

            if (reschedule == null)
                throw new ArgumentException("Reschedule request not found");

            if (reschedule.Status != RescheduleStatus.Pending)
                throw new InvalidOperationException("Reschedule request has already been processed");

            reschedule.Status = dto.Approved ? RescheduleStatus.Approved : RescheduleStatus.Rejected;
            reschedule.ApprovedByUserID = commissionerUserId;
            reschedule.ProcessedAt = DateTime.UtcNow;
            reschedule.CommissionerNotes = dto.CommissionerNotes;

            // If approved, update the season match scheduled time
            if (dto.Approved && reschedule.NewScheduledTime.HasValue)
            {
                reschedule.SeasonMatch.ScheduledTime = reschedule.NewScheduledTime;
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
            HomeCaptain = mr.SeasonMatch.HomeTeam.Captain.User.XboxUser.Gamertag,
            AwayCaptain = mr.SeasonMatch.AwayTeam.Captain.User.XboxUser.Gamertag,
            OriginalScheduledTime = mr.OriginalScheduledTime,
            NewScheduledTime = mr.NewScheduledTime,
            Reason = mr.Reason,
            RequestedByGamertag = mr.RequestedByUser.XboxUser.Gamertag,
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
            .Where(sm => sm.ScheduledTime.HasValue 
                && sm.ScheduledTime < cutoffDate
                && sm.HomeTeamResult == null 
                && sm.AwayTeamResult == null)
            .OrderBy(sm => sm.ScheduledTime)
            .AsNoTracking()
            .ToListAsync(ct);

        return overdueMatches.Select(sm => new OverdueMatchDto
        {
            SeasonMatchID = sm.SeasonMatchID,
            HomeCaptain = sm.HomeTeam.Captain.User.XboxUser.Gamertag,
            AwayCaptain = sm.AwayTeam.Captain.User.XboxUser.Gamertag,
            ScheduledTime = sm.ScheduledTime.Value,
            HoursOverdue = (int)Math.Floor((DateTime.UtcNow - sm.ScheduledTime.Value).TotalHours)
        }).ToList();
    }

    public async Task<MatchReschedule?> CreateDiscordThreadAsync(int rescheduleId, ulong channelId, CancellationToken ct = default)
    {
        var reschedule = await _context.MatchReschedules
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.HomeTeam.Captain.User.XboxUser)
            .Include(mr => mr.SeasonMatch)
                .ThenInclude(sm => sm.AwayTeam.Captain.User.XboxUser)
            .Include(mr => mr.RequestedByUser.XboxUser)
            .FirstOrDefaultAsync(mr => mr.MatchRescheduleID == rescheduleId, ct);

        if (reschedule == null || reschedule.DiscordThreadID.HasValue)
            return reschedule;

        try
        {
            // Create a message first to create thread from
            var messageContent = $"🔄 **Match Reschedule Request**\n" +
                               $"**Match:** {reschedule.SeasonMatch.HomeTeam.Captain.User.XboxUser.Gamertag} vs {reschedule.SeasonMatch.AwayTeam.Captain.User.XboxUser.Gamertag}\n" +
                               $"**Original Time:** {reschedule.OriginalScheduledTime?.ToString("yyyy-MM-dd HH:mm") ?? "Not scheduled"}\n" +
                               $"**Requested New Time:** {reschedule.NewScheduledTime?.ToString("yyyy-MM-dd HH:mm") ?? "TBD"}\n" +
                               $"**Requested By:** {reschedule.RequestedByUser.XboxUser.Gamertag}\n" +
                               $"**Reason:** {reschedule.Reason}";

            var message = await _discordClient.SendMessageAsync(channelId, new NetCord.Rest.MessageProperties { Content = messageContent }, ct: ct);

            var threadName = $"Reschedule: {reschedule.SeasonMatch.HomeTeam.Captain.User.XboxUser.Gamertag} vs {reschedule.SeasonMatch.AwayTeam.Captain.User.XboxUser.Gamertag}";
            var thread = await _discordClient.CreateGuildThreadAsync(channelId, message.Id, 
                new NetCord.Rest.GuildThreadFromMessageProperties(threadName), cancellationToken: ct);

            reschedule.DiscordThreadID = thread.Id;
            await _context.SaveChangesAsync(ct);

            return reschedule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Discord thread for reschedule {RescheduleId}", rescheduleId);
            return reschedule;
        }
    }
}