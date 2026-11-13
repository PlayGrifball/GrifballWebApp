using GrifballWebApp.Domain.Halo;
using Microsoft.EntityFrameworkCore;
using Match = GrifballWebApp.Domain.Halo.Match;
using User = GrifballWebApp.Domain.Players.User;

namespace GrifballWebApp.Infrastructure.Test;

/// <summary>
/// GrifballContext's layered transactions (StartTransactionAsync, CommitTransactionAsync, RollbackTransactionAsync):
/// the outermost commit commits, an inner one waits for it, a rollback undoes, and the misuses are refused. And the
/// read-only views of a Halo match's teams and players. On a real (temp-file SQLite) GrifballContext; what was saved is
/// read back through a second context on the same file.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class GrifballContextTransactionTests : SqliteContextFixture
{
    private static readonly long[] BothPlayers = [11, 12];

    private GrifballContext Other() =>
        new(new DbContextOptionsBuilder<GrifballContext>().UseGrifballDatabase(DatabaseProvider.Sqlite, ConnectionString).Options);

    private async Task<bool> SavedAsync(string userName)
    {
        await using var other = Other();
        return await other.Users.AnyAsync(u => u.UserName == userName);
    }

    private async Task AddUserAsync(string userName)
    {
        Context.Users.Add(User.Seed(userName: userName));
        await Context.SaveChangesAsync();
    }

    [Test]
    public async Task Commit_TheOutermost_CommitsWhatWasSaved()
    {
        await Context.StartTransactionAsync();
        await AddUserAsync("committed");

        await Context.CommitTransactionAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(await SavedAsync("committed"), Is.True);
            Assert.That(Context.Database.CurrentTransaction, Is.Null);
        }
    }

    [Test]
    public async Task Commit_AnInnerLayer_LeavesTheTransactionOpen_UntilTheOutermostCommits()
    {
        await Context.StartTransactionAsync();
        await Context.StartTransactionAsync();
        await AddUserAsync("layered");

        await Context.CommitTransactionAsync();
        var openAfterInner = Context.Database.CurrentTransaction is not null;
        await Context.CommitTransactionAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(openAfterInner, Is.True, "the inner commit commits nothing");
            Assert.That(Context.Database.CurrentTransaction, Is.Null);
            Assert.That(await SavedAsync("layered"), Is.True);
        }
    }

    [Test]
    public async Task Rollback_UndoesWhatWasSaved()
    {
        await Context.StartTransactionAsync();
        await AddUserAsync("rolled back");

        await Context.RollbackTransactionAsync();

        Assert.That(await SavedAsync("rolled back"), Is.False);
    }

    [Test]
    public async Task Commit_AfterAnInnerRollback_IsRefused()
    {
        await Context.StartTransactionAsync();
        await Context.StartTransactionAsync();
        await Context.RollbackTransactionAsync();

        await Assert.ThatAsync(() => Context.CommitTransactionAsync(),
            Throws.Exception.With.Message.EqualTo("An inner transaction has rolled back and this transaction can no longer be committed."));
    }

    [Test]
    public async Task Commit_WithNoTransaction_IsRefused()
        => await Assert.ThatAsync(() => Context.CommitTransactionAsync(), Throws.InvalidOperationException.With.Message.EqualTo("No transaction to commit."));

    [Test]
    public async Task Rollback_WithNoTransaction_IsRefused()
        => await Assert.ThatAsync(() => Context.RollbackTransactionAsync(), Throws.InvalidOperationException.With.Message.EqualTo("No transaction to rollback."));

    [Test]
    public async Task HaloMatchTeamsAndParticipants_AreEveryMatchsTeamsAndPlayers()
    {
        Context.XboxUsers.AddRange(GrifballWebApp.Domain.Players.XboxUser.FromXbox(11, "a"), GrifballWebApp.Domain.Players.XboxUser.FromXbox(12, "b"));
        var match = Match.Seed(Guid.NewGuid(), startTime: new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc));
        match.AddTeam(0, 5, Outcomes.Won);
        match.AddTeam(1, 3, Outcomes.Lost);
        match.AddParticipant(0, 11, new MatchParticipantStats());
        match.AddParticipant(1, 12, new MatchParticipantStats());
        Context.Matches.Add(match);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var teams = await Context.HaloMatchTeams.Select(t => new { t.MatchID, t.TeamID, t.Outcome }).OrderBy(t => t.TeamID).ToListAsync();
        var players = await Context.HaloMatchParticipants.Select(p => p.XboxUserID).OrderBy(x => x).ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(teams.Select(t => (t.MatchID, t.TeamID, t.Outcome)), Is.EqualTo(new[] { (match.MatchID, 0, Outcomes.Won), (match.MatchID, 1, Outcomes.Lost) }));
            Assert.That(players, Is.EqualTo(BothPlayers));
        }
    }
}
