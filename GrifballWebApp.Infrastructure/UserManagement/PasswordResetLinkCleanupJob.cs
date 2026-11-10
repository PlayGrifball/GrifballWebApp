using Quartz;
using GrifballWebApp.Application.UserManagement;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Infrastructure.UserManagement;

/// <summary>Deletes the password reset links that expired or were used (<see cref="IUserManagementService.CleanupExpiredPasswordResetLinks"/>), daily by default.</summary>
public sealed class PasswordResetLinkCleanupJob : IJob
{
    public static readonly JobKey Key = new("password-reset-link-cleanup", RecurringJobs.Group);

    public static RecurringJob Definition => new()
    {
        Key = Key,
        Description = "Deletes expired and used password reset links",
        DefaultSchedule = (trigger, now) => trigger
            .StartAt(now + TimeSpan.FromMinutes(15))
            .WithSimpleSchedule(s => s.WithInterval(TimeSpan.FromDays(1)).RepeatForever().WithMisfireInstruction(SimpleTriggerMisfireInstruction.NowWithExistingCount)),
    };

    private readonly IUserManagementService _users;

    public PasswordResetLinkCleanupJob(IUserManagementService users)
    {
        _users = users;
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct = default) => await _users.CleanupExpiredPasswordResetLinks(ct);
}
