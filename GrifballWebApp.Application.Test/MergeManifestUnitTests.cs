using GrifballWebApp.Application.Profile;
using GrifballWebApp.Application.Test.TestDoubles;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GrifballWebApp.Application.Test;

/// <summary>MergeManifest's JSON round trip, and MergeManifestPlayer's playback (UserMergeAuditService.Undo's engine,
/// tested directly here since that service is a different file): rows a merge added go, rows it moved go back, rows it
/// removed come back with their keys, and it refuses when a row was changed since, is gone, or its key is taken.</summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class MergeManifestUnitTests : ApplicationDbContextTests
{
    [Test]
    public void Serialize_Parse_RoundTrips()
    {
        var manifest = new MergeManifest
        {
            Batches =
            [
                new MergeManifestBatch
                {
                    Entries =
                    [
                        new MergeManifestEntry
                        {
                            Entity = "User",
                            Table = "Auth.Users",
                            Op = MergeManifestOp.Modified,
                            Key = new() { ["Id"] = JsonSerializer.SerializeToElement(5) },
                            Changes = new() { ["DisplayName"] = new MergeManifestChange { Old = JsonSerializer.SerializeToElement("Old"), New = JsonSerializer.SerializeToElement("New") } },
                        },
                    ],
                },
            ],
        };

        var json = manifest.Serialize();
        var parsed = MergeManifest.Parse(json);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(parsed.Batches, Has.Count.EqualTo(1));
            var entry = parsed.Entries.Single();
            Assert.That(entry.Table, Is.EqualTo("Auth.Users"));
            Assert.That(entry.Changes!["DisplayName"].New.GetString(), Is.EqualTo("New"));
            Assert.That(parsed.RowKeys(), Is.EqualTo(new HashSet<string>(StringComparer.Ordinal) { "Auth.Users|Id=5" }));
        }
    }

    [Test]
    public void Parse_EmptyOrBlank_ReturnsEmptyManifest()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MergeManifest.Parse("").Batches, Is.Empty);
            Assert.That(MergeManifest.Parse("   ").Batches, Is.Empty);
        }
    }

    [Test]
    public void RowKey_OrdersItsKeyColumns()
    {
        var entry = new MergeManifestEntry
        {
            Table = "League.PlayerSeasonStats",
            Key = new()
            {
                ["UserID"] = JsonSerializer.SerializeToElement(2),
                ["SeasonID"] = JsonSerializer.SerializeToElement(1),
            },
        };

        Assert.That(entry.RowKey, Is.EqualTo("League.PlayerSeasonStats|SeasonID=1,UserID=2"));
    }

    // ---------- MergeManifestPlayer: undoing a real merge's manifest ----------

    private async Task<(User To, User From, MergeManifest Manifest)> MergedUsers(MergeOptions? options = null)
    {
        var to = User.Seed(userName: "mergeto", displayName: "Merge To");
        var from = User.Seed(userName: "mergefrom", displayName: "Merge From");
        Context.Users.AddRange(to, from);
        await Context.SaveChangesAsync();
        await new UserMergeService(Context).Merge(to.Id, from.Id, options, CancellationToken.None);
        Context.ChangeTracker.Clear();
        var manifest = MergeManifest.Parse((await Context.UserMerges.AsNoTracking().SingleAsync()).Manifest);
        return (to, from, manifest);
    }

    [Test]
    public async Task Undo_ARemovedUser_RestoresItsRowAndKey()
    {
        var (_, from, manifest) = await MergedUsers();

        await new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        var restored = await Context.Users.FindAsync(from.Id);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.UserName, Is.EqualTo("mergefrom"));
        }
    }

    [Test]
    public async Task Undo_AMovedRow_SetsItsOldValueBack()
    {
        var xbox = XboxUser.FromXbox(999, "Gamer");
        Context.XboxUsers.Add(xbox);
        var to = User.Seed(userName: "mergeto");
        var from = User.Seed(userName: "mergefrom", xboxUserID: xbox.XboxUserID);
        Context.Users.AddRange(to, from);
        await Context.SaveChangesAsync();
        await new UserMergeService(Context).Merge(to.Id, from.Id, new MergeOptions { TransferXbox = true, DeleteMergeFrom = false }, CancellationToken.None);
        Context.ChangeTracker.Clear();
        var manifest = MergeManifest.Parse((await Context.UserMerges.AsNoTracking().SingleAsync()).Manifest);
        Assert.That((await Context.Users.FindAsync(to.Id))!.XboxUserID, Is.EqualTo(xbox.XboxUserID), "The move happened");
        Context.ChangeTracker.Clear();

        await new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None);
        await Context.SaveChangesAsync();

        Context.ChangeTracker.Clear();
        var reread = await Context.Users.FindAsync(to.Id);
        Assert.That(reread!.XboxUserID, Is.Null, "The target's XboxUserID is back to what it was before the merge");
    }

    [Test]
    public async Task Undo_ARowChangedSince_IsRefused()
    {
        var xbox = XboxUser.FromXbox(999, "Gamer");
        var other = XboxUser.FromXbox(1000, "Someone Else");
        Context.XboxUsers.AddRange(xbox, other);
        var to = User.Seed(userName: "mergeto");
        var from = User.Seed(userName: "mergefrom", xboxUserID: xbox.XboxUserID);
        Context.Users.AddRange(to, from);
        await Context.SaveChangesAsync();
        await new UserMergeService(Context).Merge(to.Id, from.Id, new MergeOptions { TransferXbox = true, DeleteMergeFrom = false }, CancellationToken.None);
        Context.ChangeTracker.Clear();
        var manifest = MergeManifest.Parse((await Context.UserMerges.AsNoTracking().SingleAsync()).Manifest);

        // Something else re-tied the target to a different Xbox account before the undo runs
        var target = await Context.Users.FindAsync(to.Id);
        target!.UntieXbox();
        target.TieToXbox(other.XboxUserID!.Value);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("changed since"));
    }

    [Test]
    public async Task Undo_ARemovedRowsKeyTaken_IsRefused()
    {
        var (_, from, manifest) = await MergedUsers();

        // Someone (or the database) made a new user with the old row's key since the merge
        Context.Users.Add(User.Seed(userName: "someone-else", id: from.Id));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("taken"));
    }

    [Test]
    public async Task Undo_AnAddedRowThatIsGone_IsRefused()
    {
        // The target already has game version 1; the source also has game version 2, which the merge adds to the target
        // (a true Added entry in the manifest, not merely a modified one).
        var to = User.Seed(userName: "mergeto");
        var from = User.Seed(userName: "mergefrom");
        Context.Users.AddRange(to, from);
        await Context.SaveChangesAsync();
        Context.Set<UserExperience>().AddRange(new UserExperience(1) { UserID = to.Id }, new UserExperience(2) { UserID = from.Id });
        await Context.SaveChangesAsync();

        await new UserMergeService(Context).Merge(to.Id, from.Id, new MergeOptions { DeleteMergeFrom = false }, CancellationToken.None);
        Context.ChangeTracker.Clear();
        var manifest = MergeManifest.Parse((await Context.UserMerges.AsNoTracking().SingleAsync()).Manifest);
        Assert.That(manifest.Entries.Any(e => e.Op == MergeManifestOp.Added && e.Table.Contains("UserExperience")), Is.True, "The merge added a new row for the target");

        // Gone before the undo runs
        Context.Set<UserExperience>().Remove(await Context.Set<UserExperience>().SingleAsync(x => x.UserID == to.Id && x.GameVersionID == 2));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ex = await Assert.ThrowsAsync<MergeUndoRefusedException>(() => new MergeManifestPlayer(Context).UndoAsync(manifest, CancellationToken.None));
        Assert.That(ex!.Message, Does.Contain("gone"));
    }
}
