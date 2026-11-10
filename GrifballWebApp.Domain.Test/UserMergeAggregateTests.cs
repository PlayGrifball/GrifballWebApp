using GrifballWebApp.Domain.Players;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// The record of a merge as an aggregate (#349): made by <see cref="UserMerge.Record"/>, which refuses what no merge makes,
/// and changed once at most, by <see cref="UserMerge.Undo"/>.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UserMergeAggregateTests
{
    private static readonly DateTime MergedAt = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    internal static UserMerge Merge(int source = 1, int target = 2, string? reason = "claim 12", int moved = 3, int removed = 2, int added = 1)
        => UserMerge.Record(MergedAt, MergeTrigger.ClaimApproval, 7, reason, source, "historical:Old Tag", target, "real", "{}", moved, removed, added);

    [Test]
    public void Record_StandsAsTheMergeWasMade_WithItsReasonCutToWhatARowHolds()
    {
        var merge = Merge(reason: new string('r', UserMerge.ReasonMaxLength + 5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((merge.MergedAt, merge.Trigger, merge.TriggeredByUserID), Is.EqualTo((MergedAt, MergeTrigger.ClaimApproval, (int?)7)));
            Assert.That((merge.SourceUserID, merge.SourceUserName, merge.TargetUserID, merge.TargetUserName), Is.EqualTo((1, "historical:Old Tag", 2, "real")));
            Assert.That((merge.Manifest, merge.RowsMoved, merge.RowsRemoved, merge.RowsAdded), Is.EqualTo(("{}", 3, 2, 1)));
            Assert.That(merge.Reason, Is.EqualTo(new string('r', UserMerge.ReasonMaxLength)));
            Assert.That(merge.IsStanding, Is.True);
            Assert.That((merge.UndoneAt, merge.UndoneByUserID, merge.UndoNote), Is.EqualTo(((DateTime?)null, (int?)null, (string?)null)));
            Assert.That(Merge(reason: null).Reason, Is.Null);
        }
    }

    [Test]
    public void Record_RefusesWhatNoMergeMakes()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => Merge(source: 4, target: 4), Throws.ArgumentException, "A user into itself");
            Assert.That(() => Merge(moved: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => Merge(removed: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => Merge(added: -1), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => UserMerge.Record(MergedAt, (MergeTrigger)99, null, null, 1, null, 2, null, "{}", 0, 0, 0),
                Throws.InstanceOf<ArgumentOutOfRangeException>(), "A trigger the model doesn't know");
            Assert.That(() => UserMerge.Record(MergedAt, MergeTrigger.Manual, null, null, 1, null, 2, null, null!, 0, 0, 0),
                Throws.ArgumentNullException, "No manifest");
        }
    }

    [Test]
    public void Undo_MarksAStandingMergeUndone_Once()
    {
        var merge = Merge();
        var undoneAt = MergedAt.AddDays(1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(merge.Undo(undoneAt, 9, "  wrong person  "), Is.EqualTo(UserMergeUndo.Undone));
            Assert.That((merge.UndoneAt, merge.UndoneByUserID, merge.UndoNote), Is.EqualTo(((DateTime?)undoneAt, (int?)9, "wrong person")), "Trimmed");
            Assert.That(merge.IsStanding, Is.False);

            Assert.That(merge.Undo(undoneAt.AddDays(1), 10, "again"), Is.EqualTo(UserMergeUndo.AlreadyUndone));
            Assert.That((merge.UndoneAt, merge.UndoneByUserID, merge.UndoNote), Is.EqualTo(((DateTime?)undoneAt, (int?)9, "wrong person")), "As it was");
        }
    }

    [Test]
    public void Undo_KeepsNoBlankNote_AndCutsALongOne()
    {
        var blank = Merge();
        blank.Undo(MergedAt, null, "   ");
        var none = Merge();
        none.Undo(MergedAt, null, null);
        var tooLong = Merge();
        tooLong.Undo(MergedAt, null, new string('n', UserMerge.UndoNoteMaxLength + 1));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(blank.UndoNote, Is.Null);
            Assert.That(none.UndoNote, Is.Null);
            Assert.That(tooLong.UndoNote, Is.EqualTo(new string('n', UserMerge.UndoNoteMaxLength)));
        }
    }
}
