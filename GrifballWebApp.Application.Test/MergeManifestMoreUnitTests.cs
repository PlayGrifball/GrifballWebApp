using System.Text.Json;
using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// MergeValues' comparison and MergeManifestPlayer's refusals that MergeManifestUnitTests doesn't reach: an added row
/// changed since, and a moved row that is gone, each from a manifest written by hand for a user row. Not covered: a
/// restored row's shadow properties (Restore's last branch, for a property with neither a writable CLR property nor a
/// backing field) and SaveWithExplicitKeys' SQL Server IDENTITY_INSERT branch (Integration.Test, against SQL Server).
/// </summary>
[TestFixture]
internal sealed class MergeManifestMoreUnitTests : ApplicationDbContextTests
{
    private static readonly byte[] Bytes = [1, 2, 3];
    private static readonly byte[] SameBytes = [1, 2, 3];
    private static readonly byte[] OtherBytes = [1, 2, 4];

    [Test]
    public void Same_ComparesByteArraysByContent()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MergeValues.Same(Bytes, SameBytes), Is.True);
            Assert.That(MergeValues.Same(Bytes, OtherBytes), Is.False);
            Assert.That(MergeValues.Same("a", "a"), Is.True);
            Assert.That(MergeValues.Same(null, "a"), Is.False);
        }
    }

    private string UserEntity => Context.Model.FindEntityType(typeof(User))!.Name;

    private static MergeManifest OneEntry(MergeManifestEntry entry) => new() { Batches = [new MergeManifestBatch { Entries = [entry] }] };

    [Test]
    public async Task Undo_AnAddedRowChangedSince_IsRefused()
    {
        Context.Users.Add(User.Seed(userName: "added", displayName: "Now", id: 5));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var manifest = OneEntry(new MergeManifestEntry
        {
            Entity = UserEntity,
            Table = "Auth.Users",
            Op = MergeManifestOp.Added,
            Key = new() { ["Id"] = JsonSerializer.SerializeToElement(5) },
            Values = new() { ["Id"] = JsonSerializer.SerializeToElement(5), ["DisplayName"] = JsonSerializer.SerializeToElement("Then") },
        });

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None));

        Context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(ex!.Message, Does.Contain("the row it added was changed since (DisplayName)"));
            Assert.That(await Context.Users.AnyAsync(u => u.Id == 5), Is.True, "the row stays");
        }
    }

    [Test]
    public async Task Undo_AnAddedRowUnchanged_IsRemoved()
    {
        Context.Users.Add(User.Seed(userName: "added", displayName: "Same", id: 5));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        var manifest = OneEntry(new MergeManifestEntry
        {
            Entity = UserEntity,
            Table = "Auth.Users",
            Op = MergeManifestOp.Added,
            Key = new() { ["Id"] = JsonSerializer.SerializeToElement(5) },
            Values = new() { ["Id"] = JsonSerializer.SerializeToElement(5), ["DisplayName"] = JsonSerializer.SerializeToElement("Same") },
        });

        await new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None);

        Context.ChangeTracker.Clear();
        Assert.That(await Context.Users.AnyAsync(u => u.Id == 5), Is.False);
    }

    [Test]
    public async Task Undo_AMovedRowThatIsGone_IsRefused()
    {
        var manifest = OneEntry(new MergeManifestEntry
        {
            Entity = UserEntity,
            Table = "Auth.Users",
            Op = MergeManifestOp.Modified,
            Key = new() { ["Id"] = JsonSerializer.SerializeToElement(999) },
            Changes = new()
            {
                ["DisplayName"] = new MergeManifestChange { Old = JsonSerializer.SerializeToElement("Old"), New = JsonSerializer.SerializeToElement("New") },
            },
        });

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None));

        Assert.That(ex!.Message, Is.EqualTo("Can't undo: a row it moved is gone (Auth.Users Id 999)."));
    }
}
