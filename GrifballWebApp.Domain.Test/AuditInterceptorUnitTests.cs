using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// AuditInterceptor on its own: a context on the SQL Server provider that never connects (the interceptor only reads its
/// change tracker), its SavingChanges called as EF would before a save. No database, host, file or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class AuditInterceptorUnitTests
{
    private static readonly DateTime Earlier = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private const int CreatorID = 7;
    private const int EditorID = 42;

    private sealed class Row : AuditableEntity
    {
        public int RowID { get; set; }
    }

    private sealed class AuditContext() : DbContext(new DbContextOptionsBuilder<AuditContext>().UseSqlServer("Server=never-connected").Options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Row>().HasKey(r => r.RowID);
    }

    /// <summary>The app user the save is made by; counts the interceptor's asks.</summary>
    private sealed class CurrentUser(int? userID) : ICurrentUserService
    {
        public int Asked { get; private set; }
        public int? GetCurrentUserId()
        {
            Asked++;
            return userID;
        }
        public void SetCurrentUserId(int userId) => throw new NotSupportedException("The interceptor only reads the user");
        public void SetCurrentUserIdFromClaims(ClaimsPrincipal? user) => throw new NotSupportedException("The interceptor only reads the user");
    }

    /// <summary>A row as saved before, by <see cref="CreatorID"/> at <see cref="Earlier"/>, tracked as <paramref name="state"/>.</summary>
    private static Row Saved(AuditContext context, int rowID, EntityState state)
    {
        var row = new Row { RowID = rowID };
        IAuditStamp stamp = row;
        stamp.CreatedByID = CreatorID;
        stamp.ModifiedByID = CreatorID;
        stamp.CreatedAt = Earlier;
        stamp.ModifiedAt = Earlier;
        context.Attach(row);
        context.Entry(row).State = state;
        return row;
    }

    /// <summary>A new row, tracked as added.</summary>
    private static Row Added(AuditContext context)
    {
        var row = new Row();
        context.Add(row);
        return row;
    }

    private static DbContextEventData Saving(DbContext? context) => new(null!, null!, context);

    [Test]
    public void SavingChanges_StampsAnAddedRowAndAModifiedOne_WithOneUserAndOneTime()
    {
        using var context = new AuditContext();
        var added = Added(context);
        var modified = Saved(context, 1, EntityState.Modified);
        var interceptor = new AuditInterceptor(new CurrentUser(EditorID));

        var result = interceptor.SavingChanges(Saving(context), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.False, "The save goes on");
            Assert.That((added.CreatedByID, added.ModifiedByID), Is.EqualTo(((int?)EditorID, (int?)EditorID)), "A new row is made and changed by the user");
            Assert.That(added.ModifiedAt, Is.EqualTo(added.CreatedAt), "Made and last changed in the same instant");
            Assert.That(added.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That((modified.CreatedByID, modified.CreatedAt), Is.EqualTo(((int?)CreatorID, Earlier)), "A change never rewrites who made the row, or when");
            Assert.That(modified.ModifiedByID, Is.EqualTo(EditorID));
            Assert.That(modified.ModifiedAt, Is.EqualTo(added.CreatedAt), "One time for the whole save");
            Assert.That(modified.ModifiedAt, Is.Not.EqualTo(Earlier));
        }
    }

    [Test]
    public void SavingChanges_LeavesUnchangedAndDeletedRowsAlone()
    {
        using var context = new AuditContext();
        var unchanged = Saved(context, 1, EntityState.Unchanged);
        var deleted = Saved(context, 2, EntityState.Deleted);

        new AuditInterceptor(new CurrentUser(EditorID)).SavingChanges(Saving(context), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((unchanged.ModifiedByID, unchanged.ModifiedAt), Is.EqualTo(((int?)CreatorID, Earlier)), "Nothing changed: nothing to stamp");
            Assert.That((deleted.ModifiedByID, deleted.ModifiedAt), Is.EqualTo(((int?)CreatorID, Earlier)), "A deleted row isn't stamped");
        }
    }

    [Test]
    public async Task SavingChangesAsync_StampsAsTheSyncSaveDoes_WithNoUserWhenThereIsNone()
    {
        await using var context = new AuditContext();
        var added = Added(context);

        var result = await new AuditInterceptor(new CurrentUser(null)).SavingChangesAsync(Saving(context), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.False);
            Assert.That((added.CreatedByID, added.ModifiedByID), Is.EqualTo(((int?)null, (int?)null)), "A background job's save has no app user");
            Assert.That(added.CreatedAt, Is.Not.EqualTo(default(DateTime)), "Stamped all the same");
            Assert.That(added.ModifiedAt, Is.EqualTo(added.CreatedAt));
        }
    }

    [Test]
    public void SavingChanges_WithoutAContext_DoesNothing()
    {
        var user = new CurrentUser(EditorID);

        var result = new AuditInterceptor(user).SavingChanges(Saving(null), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.False);
            Assert.That(user.Asked, Is.Zero, "No context, no rows: the user isn't even asked");
        }
    }

    [Test]
    public async Task SavingChangesAsync_WithoutAContext_DoesNothing()
    {
        var user = new CurrentUser(EditorID);

        var result = await new AuditInterceptor(user).SavingChangesAsync(Saving(null), default);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.HasResult, Is.False);
            Assert.That(user.Asked, Is.Zero, "No context, no rows: the user isn't even asked");
        }
    }
}
