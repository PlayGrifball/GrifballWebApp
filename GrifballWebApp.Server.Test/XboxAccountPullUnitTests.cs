using GrifballWebApp.Application.Jobs;
using GrifballWebApp.Application.XboxAccounts;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;
using GrifballWebApp.Server.XboxAccounts;
using Microsoft.AspNetCore.Mvc;

namespace GrifballWebApp.Server.Test;

/// <summary>
/// An Xbox account's pull from its page, through the job-backed <see cref="XboxAccountPulls"/> on an in-memory Quartz: queued
/// once, its status read back, one run per account.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class XboxAccountPullUnitTests : SqliteGrifballContextFixture
{
    private InMemoryQuartz? _quartz;

    [TearDown]
    public async Task TearDownQuartz()
    {
        if (_quartz is not null)
            await _quartz.DisposeAsync();
    }

    [Test]
    public async Task APull_IsQueuedOnce_AndItsStatusSaysSo()
    {
        Db.XboxUsers.Add(XboxUser.FromXbox(2535400000000042, "Pull Me"));
        await Db.SaveChangesAsync();
        _quartz = await InMemoryQuartz.Create();
        var pulls = new XboxAccountPulls(_quartz.OnDemand);
        var controller = new XboxAccountsController(new XboxAccountService(Db));

        var idle = await controller.GetXboxAccountPullStatus(2535400000000042, pulls, CancellationToken.None);
        var first = await pulls.Queue(2535400000000042, null);
        var again = await pulls.Queue(2535400000000042, null);
        var queued = await controller.GetXboxAccountPullStatus(2535400000000042, pulls, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(idle.State, Is.EqualTo(OnDemandJobState.Idle));
            Assert.That(first.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(again.State, Is.EqualTo(OnDemandJobState.Queued), "asked again while one is queued: the same run");
            Assert.That(queued.State, Is.EqualTo(OnDemandJobState.Queued));
            Assert.That(await _quartz.Scheduler.GetTriggerState(OnDemandJobs.TriggerKeyOf<XboxAccountPullJob>(XboxAccountPullJob.RunName(2535400000000042))),
                Is.EqualTo(Quartz.TriggerState.Normal));
        }
    }

    [Test]
    public async Task TheAccountPage_IsNotFound_ForAnAccountTheSiteDoesntKnow()
    {
        var result = await new XboxAccountsController(new XboxAccountService(Db)).GetXboxAccount(42, CancellationToken.None);

        Assert.That(result.Result, Is.InstanceOf<ObjectResult>().With.Property(nameof(ObjectResult.StatusCode)).EqualTo(404));
    }
}
