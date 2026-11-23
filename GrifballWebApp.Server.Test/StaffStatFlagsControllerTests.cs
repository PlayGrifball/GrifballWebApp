using GrifballWebApp.Application.StatFlags;
using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Server.StatFlags;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Server.Test;

/// <summary>StaffStatFlagsController's authorization: no database, host, file or network.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StaffStatFlagsControllerUnitTests
{
    [Test]
    public void Controller_RequiresCommissionerOrSysadmin()
        => Assert.That(ControllerTestHelpers.ClassAuthorize(typeof(StaffStatFlagsController))!.Roles, Is.EqualTo("Commissioner,Sysadmin"));

    // A flag is read-only information and dismissing one never merges, signs anyone out, or touches roles/the
    // account - the same "normal staff write" category VodController's own Dismiss is in (#549/#562).
    [TestCase(nameof(StaffStatFlagsController.List))]
    [TestCase(nameof(StaffStatFlagsController.ForSeasonMatch))]
    [TestCase(nameof(StaffStatFlagsController.Dismiss))]
    public void Action_AllowsAnAccessToken(string action)
        => Assert.That(ControllerTestHelpers.ActionAllowsAccessToken(typeof(StaffStatFlagsController), action), Is.True);
}

/// <summary>StaffStatFlagsController's database-backed unit tests: a real GrifballContext (temp-file SQLite, #402).</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class StaffStatFlagsControllerDatabaseUnitTests : SqliteGrifballContextFixture
{
    private static readonly DateTime FoundAt = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid MatchID = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private StaffStatFlagsController Controller() => new(new StaffStatFlagService(Db));

    [Test]
    public async Task List_ReturnsOnlyOpenFlags()
    {
        Db.Matches.Add(Domain.Halo.Match.Seed(MatchID));
        Db.StatFlags.Add(StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, null, "open one", FoundAt));
        var dismissed = StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, null, "dismissed one", FoundAt);
        dismissed.Dismiss(1, FoundAt);
        Db.StatFlags.Add(dismissed);
        await Db.SaveChangesAsync();

        var flags = await Controller().List(CancellationToken.None);

        Assert.That(flags.Single().Reason, Is.EqualTo("open one"));
    }

    [Test]
    public async Task ForSeasonMatch_IgnoresFlagsOfGamesInNoSeries()
    {
        // A pulled match not yet reported to any season match: its flag doesn't belong to season match 7's series
        Db.Matches.Add(Domain.Halo.Match.Seed(MatchID));
        Db.StatFlags.Add(StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, null, "unrelated", FoundAt));
        await Db.SaveChangesAsync();

        var flags = await Controller().ForSeasonMatch(7, CancellationToken.None);

        Assert.That(flags, Is.Empty);
    }

    [Test]
    public async Task Dismiss_ClosesTheFlag_AndReturnsNoContent()
    {
        Db.Matches.Add(Domain.Halo.Match.Seed(MatchID));
        var flag = StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, null, "reason", FoundAt);
        Db.StatFlags.Add(flag);
        await Db.SaveChangesAsync();

        var result = await Controller().Dismiss(flag.StatFlagID, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.InstanceOf<NoContentResult>());
            Assert.That((await Db.StatFlags.AsNoTracking().SingleAsync(f => f.StatFlagID == flag.StatFlagID)).IsOpen, Is.False);
        }
    }

    [Test]
    public void Dismiss_ThatDoesNotExist_ThrowsNotFound()
        => Assert.That(async () => await Controller().Dismiss(999, CancellationToken.None), Throws.InstanceOf<StatFlagNotFoundException>());

    [Test]
    public async Task Dismiss_AlreadyDismissed_StillReturnsNoContent()
    {
        Db.Matches.Add(Domain.Halo.Match.Seed(MatchID));
        var flag = StatFlag.Found(StatFlagRule.ImpossibleAccuracy, MatchID, null, null, null, "reason", FoundAt);
        flag.Dismiss(1, FoundAt);
        Db.StatFlags.Add(flag);
        await Db.SaveChangesAsync();

        // Two staff dismissing it, or a stale list: a second dismiss says the same thing, not an error
        var result = await Controller().Dismiss(flag.StatFlagID, CancellationToken.None);

        Assert.That(result, Is.InstanceOf<NoContentResult>());
    }
}
