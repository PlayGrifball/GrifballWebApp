using GrifballWebApp.Domain.Players;
using GrifballWebApp.Infrastructure.HaloInfinite;
using GrifballWebApp.Infrastructure.Jobs;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// FailedAccounts: the accounts a per-account job's run failed for, named by gamertag after its summary, one line a reason, on a real
/// (temp-file SQLite) GrifballContext; and JobSummary's cut.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class FailedAccountsTests : SqliteContextFixture
{
    private static readonly string[] Named = ["Halo answered 503: Alpha (1), Bravo (3)", "Halo gave no answer: 2"];

    private async Task AddAsync(params (long Xuid, string Gamertag)[] accounts)
    {
        foreach (var (xuid, gamertag) in accounts)
            Context.XboxUsers.Add(XboxUser.FromXbox(xuid, gamertag));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task NameAsync_OneLineAReason_ByGamertag_AnAccountWithoutOneByItsXuid()
    {
        await AddAsync((1, "Alpha"), (3, "Bravo"));
        var failed = new FailedAccounts();
        failed.Add(1, "Halo answered 503");
        failed.Add(2, "Halo gave no answer");
        failed.Add(3, "Halo answered 503");

        await failed.NameAsync(Context, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(failed.Lines, Is.EqualTo(Named), "a whole call failing is one line, not one an account");
            Assert.That(failed.After("3 failed"), Is.EqualTo("3 failed. Failed: Halo answered 503: Alpha (1), Bravo (3); Halo gave no answer: 2"));
        }
    }

    [Test]
    public async Task NameAsync_CalledAgain_BuildsTheLinesAfresh()
    {
        await AddAsync((1, "Alpha"));
        var failed = new FailedAccounts();
        failed.Add(1, "Halo answered 503");

        await failed.NameAsync(Context, CancellationToken.None);
        await failed.NameAsync(Context, CancellationToken.None);

        Assert.That(failed.Lines, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task After_CutsALongListToTheLimit_AndLeavesASummaryWithoutFailuresAlone()
    {
        var failed = new FailedAccounts();
        for (var i = 0; i < 40; i++)
            failed.Add(i, $"{i} {new string('x', 300)}");

        await failed.NameAsync(Context, CancellationToken.None);
        var summary = failed.After("40 failed");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(summary, Has.Length.EqualTo(JobSummary.MaxLength).And.EndWith("…"));
            Assert.That(failed.Lines[0], Has.Length.LessThan(140), "each reason cut too");
            Assert.That(new FailedAccounts().After("All fine"), Is.EqualTo("All fine"));
        }
    }

    [Test]
    public void Shorten_NeverCutsInsideASurrogatePair()
    {
        // "ab" then an emoji (two UTF-16 units): a cut to 4 would keep its first half
        var cut = JobSummary.Shorten("ab\U0001F600cdef", 4);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(cut, Is.EqualTo("ab…"));
            Assert.That(JobSummary.Shorten("abcdef", 4), Is.EqualTo("abc…"));
            Assert.That(JobSummary.Shorten("abc", 4), Is.EqualTo("abc"));
            Assert.That(JobSummary.Shorten("abc", 1), Is.EqualTo("…"), "room for the ellipsis alone");
        }
    }
}
