using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// FailedAccounts: the accounts a per-account job's run failed for, named by gamertag after its summary, on a real (temp-file SQLite)
/// GrifballContext.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class FailedAccountsTests : SqliteContextFixture
{
    private static readonly string[] Named = ["Alpha (1): Halo answered 503", "2: Halo gave no answer"];

    [Test]
    public async Task NameAsync_NamesByGamertag_AnAccountWithoutOneByItsXuid()
    {
        Context.XboxUsers.Add(XboxUser.FromXbox(1, "Alpha"));
        await Context.SaveChangesAsync();
        var failed = new FailedAccounts();
        failed.Add(1, "Halo answered 503");
        failed.Add(2, "Halo gave no answer");

        await failed.NameAsync(Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failed.Lines, Is.EqualTo(Named));
            Assert.That(failed.After("2 failed"), Is.EqualTo("2 failed. Failed: Alpha (1): Halo answered 503; 2: Halo gave no answer"));
        }
    }

    [Test]
    public async Task After_CutsALongListToTheLimit_AndLeavesASummaryWithoutFailuresAlone()
    {
        var failed = new FailedAccounts();
        for (var i = 0; i < 40; i++)
            failed.Add(i, new string('x', 300));

        await failed.NameAsync(Context, CancellationToken.None);
        var summary = failed.After("40 failed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary, Has.Length.EqualTo(FailedAccounts.MaxSummary).And.EndWith("…"));
            Assert.That(failed.Lines[0], Has.Length.LessThan(140), "each reason cut too");
            Assert.That(new FailedAccounts().After("All fine"), Is.EqualTo("All fine"));
        }
    }
}
