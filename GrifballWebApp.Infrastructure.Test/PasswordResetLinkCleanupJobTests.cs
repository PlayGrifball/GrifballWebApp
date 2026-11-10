using NSubstitute;
using Quartz;
using GrifballWebApp.Application.UserManagement;
using GrifballWebApp.Infrastructure.UserManagement;

namespace GrifballWebApp.Infrastructure.Test;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PasswordResetLinkCleanupJobTests
{
    [Test]
    public async Task ItCleansUpTheExpiredLinks_Daily()
    {
        var users = Substitute.For<IUserManagementService>();
        using var cts = new CancellationTokenSource();

        await new PasswordResetLinkCleanupJob(users).Execute(Substitute.For<IJobExecutionContext>(), cts.Token);

        await users.Received(1).CleanupExpiredPasswordResetLinks(cts.Token);
        var trigger = (ISimpleTrigger)PasswordResetLinkCleanupJob.Definition.DefaultTrigger(DateTimeOffset.UtcNow);
        Assert.That(trigger.RepeatInterval, Is.EqualTo(TimeSpan.FromDays(1)));
    }
}
