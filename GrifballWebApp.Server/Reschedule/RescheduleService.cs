using DiscordInterface.Generated;
using GrifballWebApp.Database;
using GrifballWebApp.Database.Models;
using GrifballWebApp.Server.Extensions;
using GrifballWebApp.Server.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using System.Threading;

namespace GrifballWebApp.Server.Reschedule;

public class RescheduleService
{
    private readonly GrifballContext _context;
    private readonly IDiscordRestClient _discordClient;
    private readonly ILogger<RescheduleService> _logger;
    private readonly IOptions<DiscordOptions> _options;

    public RescheduleService(GrifballContext context, IDiscordRestClient discordClient, ILogger<RescheduleService> logger, IOptions<DiscordOptions> options)
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
        var reschedule = await SaveDecisionAsync(rescheduleId, dto, commissionerUserId, ct);

        // After the commit: the decision stands whether or not Discord hears about it.
        if (reschedule.DiscordThreadID is not null)
            await NotifyThreadAsync(reschedule.DiscordThreadID.Value, reschedule.Status, commissionerUserId, ct);

        return reschedule;
    }

    private async Task<MatchReschedule> SaveDecisionAsync(int rescheduleId, ProcessRescheduleDto dto, int commissionerUserId, CancellationToken ct)
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
    /// Tells the request's Discord thread the decision and who made it. Never fails the decision: in a preview without
    /// Discord every call throws <see cref="PreviewDiscordDisabledException"/>, and Discord can be down; both are logged.
    /// </summary>
    private async Task NotifyThreadAsync(ulong threadID, RescheduleStatus status, int approverUserID, CancellationToken ct)
    {
        try
        {
            var approver = await ApproverNameAsync(approverUserID, ct);
            await _discordClient.SendMessageAsync(threadID,
                new NetCord.Rest.MessageProperties { Content = $"Your request has been {status} by {approver}" },
                cancellationToken: ct);
        }
        catch (PreviewDiscordDisabledException ex)
        {
            _logger.LogInformation(ex, "Preview without Discord: not telling thread {ThreadId} the reschedule was {Status}", threadID, status);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to tell thread {ThreadId} the reschedule was {Status}", threadID, status);
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
                            $"**Match:** {reschedule.SeasonMatch.HomeTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"} vs {reschedule.SeasonMatch.AwayTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"}\n" +
                            $"**Original Time:** {reschedule.OriginalScheduledTime?.DiscordTimeEmbed() ?? "Not scheduled"}\n" +
                            $"**Requested New Time:** {reschedule.NewScheduledTime?.DiscordTimeEmbed() ?? "TBD"}\n" +
                            $"**Requested By:** {reschedule.RequestedByUser.XboxUser?.Gamertag}\n" +
                            $"**Reason:** {reschedule.Reason}";

        var message = await _discordClient.SendMessageAsync(channelId, new NetCord.Rest.MessageProperties { Content = messageContent }, cancellationToken: ct);

        var threadName = $"Reschedule: {reschedule.SeasonMatch.HomeTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"} vs {reschedule.SeasonMatch.AwayTeam?.Captain?.User.XboxUser?.Gamertag ?? "UNKNOWN"}";
        var thread = await _discordClient.CreateGuildThreadAsync(channelId, message.Id,
            new NetCord.Rest.GuildThreadFromMessageProperties(threadName), cancellationToken: ct);

        await AddUsersToThread(reschedule, thread, ct);

        reschedule.DiscordThreadID = thread.Id;
        await _context.SaveChangesAsync(ct);
    }

    private async Task AddUsersToThread(MatchReschedule reschedule, IDiscordGuildThread thread, CancellationToken ct)
    {
        var sb = new StringBuilder();
        await AddCaptainToThread(reschedule.SeasonMatch.HomeTeam, thread, sb, ct);
        await AddCaptainToThread(reschedule.SeasonMatch.AwayTeam, thread, sb, ct);

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
                cancellationToken: ct);
        }
    }

    private async Task AddCaptainToThread(Team? team, IDiscordGuildThread thread, StringBuilder sb, CancellationToken ct)
    {
        if (team is null)
        {
            sb.AppendLine("Could not add a captain to thread, the match is missing a team.");
            return;
        }

        if (team.Captain is null)
        {
            sb.AppendLine($"Could not add {team.TeamName} captain to thread, team has no captain.");
            return;
        }

        await AddUserToThread(team.Captain.User, thread, sb, ct);
    }

    private async Task AddUserToThread(User user, IDiscordGuildThread thread, StringBuilder sb, CancellationToken ct)
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