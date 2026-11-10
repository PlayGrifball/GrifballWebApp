using System.Security.Claims;
using Quartz;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>JobUser: who a job runs as, from its trigger's data (run now) or its input (queued from a page).</summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class JobUserTests
{
    [Test]
    public void TheUser_IsReadFromTheTriggersData_ThenTheInput()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JobUser.Of(new JobDataMap { [JobUser.DataKey] = "7" }), Is.EqualTo(7), "Run now");
            Assert.That(JobUser.Of(new JobDataMap { [JobUser.DataKey] = 8 }), Is.EqualTo(8));
            Assert.That(JobUser.Of(new JobDataMap { [SchedulerConstants.JobInput] = "{\"SeasonMatchID\":42,\"RequestedByUserID\":9}" }), Is.EqualTo(9), "Queued from a page");
            Assert.That(JobUser.Of(new JobDataMap { [SchedulerConstants.JobInput] = "{\"seasonMatchID\":42,\"requestedByUserID\":10}" }), Is.EqualTo(10), "Any case");
            Assert.That(JobUser.Of(new JobDataMap { [JobUser.DataKey] = "7", [SchedulerConstants.JobInput] = "{\"RequestedByUserID\":9}" }), Is.EqualTo(7));
        });
    }

    [Test]
    public void NoUser_WhenNobodyStartedIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(JobUser.Of(new JobDataMap()), Is.Null, "A schedule");
            Assert.That(JobUser.Of(new JobDataMap { [SchedulerConstants.JobInput] = "{\"RequestedByUserID\":null}" }), Is.Null);
            Assert.That(JobUser.Of(new JobDataMap { [SchedulerConstants.JobInput] = "{\"SeasonMatchID\":42}" }), Is.Null, "An input without one");
            Assert.That(JobUser.Of(new JobDataMap { [SchedulerConstants.JobInput] = "[1,2]" }), Is.Null);
            Assert.That(JobUser.Of(new JobDataMap { [SchedulerConstants.JobInput] = "not json" }), Is.Null);
            Assert.That(JobUser.Of(new JobDataMap { [JobUser.DataKey] = "someone" }), Is.Null);
        });
    }

    [Test]
    public void TheSignedInUsersID_IsTheirNameIdentifier()
    {
        var signedIn = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "12")], "test"));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "12")]));
        Assert.Multiple(() =>
        {
            Assert.That(JobUser.IdOf(signedIn), Is.EqualTo(12));
            Assert.That(JobUser.IdOf(anonymous), Is.Null, "Not signed in");
            Assert.That(JobUser.IdOf(null), Is.Null);
        });
    }
}
