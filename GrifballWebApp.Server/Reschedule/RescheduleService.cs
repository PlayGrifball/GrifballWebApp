using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Threading;

namespace GrifballWebApp.Server.Reschedule;

public class RescheduleService
{
    private readonly GrifballContext _context;
    private readonly IDiscordClient _discordClient;
    private readonly ILogger<RescheduleService> _logger;
    private readonly IOptions<DiscordOptions> _options;

    public RescheduleService(GrifballContext context, IDiscordClient discordClient, ILogger<RescheduleService> logger, IOptions<DiscordOptions> options)
    {
        _context = context;
        _discordClient = discordClient;
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

            // Prevent new request if there is already an active one
            if (seasonMatch.ActiveRescheduleRequestId.HasValue)
                throw new InvalidOperationException("There is already an active reschedule request for this match.");

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

            // Clear the active request if processed
            reschedule.SeasonMatch.ActiveRescheduleRequestId = null;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            if (reschedule.DiscordThreadID is not null)
            {
                await _discordClient.SendMessageAsync(reschedule.DiscordThreadID.Value,
                new NetCord.Rest.MessageProperties { Content = $"Your request has been {reschedule.Status} by {reschedule.ApprovedByUserID}" },
                ct: ct);
            }

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

        if (reschedule.DiscordThreadID.HasValue)
            return; // Thread already exists

        var channelId = _options.Value.ReschedulesChannel;

        // Create a message first to create thread from
        var messageContent = $"🔄 **Match Reschedule Request**\n" +
                            $"**Match:** {reschedule.SeasonMatch.HomeTeam.Captain.User.XboxUser?.Gamertag} vs {reschedule.SeasonMatch.AwayTeam.Captain.User.XboxUser?.Gamertag}\n" +
                            $"**Original Time:** {reschedule.OriginalScheduledTime?.DiscordTimeEmbed() ?? "Not scheduled"}\n" +
                            $"**Requested New Time:** {reschedule.NewScheduledTime?.DiscordTimeEmbed() ?? "TBD"}\n" +
                            $"**Requested By:** {reschedule.RequestedByUser.XboxUser?.Gamertag}\n" +
                            $"**Reason:** {reschedule.Reason}";

        var message = await _discordClient.SendMessageAsync(channelId, new NetCord.Rest.MessageProperties { Content = messageContent }, ct: ct);

        var threadName = $"Reschedule: {reschedule.SeasonMatch.HomeTeam.Captain.User.XboxUser?.Gamertag} vs {reschedule.SeasonMatch.AwayTeam.Captain.User.XboxUser?.Gamertag}";
        var thread = await _discordClient.CreateGuildThreadAsync(channelId, message.Id,
            new NetCord.Rest.GuildThreadFromMessageProperties(threadName), cancellationToken: ct);

        await AddUsersToThread(reschedule, thread, ct);

        reschedule.DiscordThreadID = thread.Id;
        await _context.SaveChangesAsync(ct);
    }

    private async Task AddUsersToThread(MatchReschedule reschedule, IGuildThread thread, CancellationToken ct)
    {
        var sb = new StringBuilder();
        await AddUserToThread(reschedule.SeasonMatch.HomeTeam.Captain.User, thread, sb, ct);
        await AddUserToThread(reschedule.SeasonMatch.AwayTeam.Captain.User, thread, sb, ct);

        var commisioners = await _context.Users
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == "Commissioner"))
            .ToListAsync(ct);
        foreach (var commisioner in commisioners)
        {
            await AddUserToThread(commisioner, thread, sb, ct);
        }
        var finalErrors = sb.ToString();
        if (!string.IsNullOrEmpty(finalErrors))
        {
            await _discordClient.SendMessageAsync(thread.Id,
                new NetCord.Rest.MessageProperties { Content = finalErrors },
                ct: ct);
        }
    }

    private async Task AddUserToThread(User user, IGuildThread thread, StringBuilder sb, CancellationToken ct)
    {
        var discordUserId = (ulong?)user.DiscordUserID;
        if (discordUserId.HasValue)
        {
            try
            {
                await thread.AddUserAsync(discordUserId.Value, null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add {DiscordUserId} to thread {ThreadId}", discordUserId.Value, thread.Id);   
                sb.Append($"Could not add {user.DisplayName ?? user.UserName} to thread, exception thrown");
            }
        }
        else
        {
            sb.AppendLine($"Could not add {user.DisplayName ?? user.UserName} to thread, missing discord id.");
        }
    }
}