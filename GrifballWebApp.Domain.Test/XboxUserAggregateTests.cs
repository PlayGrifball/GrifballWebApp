using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// An Xbox account and its past gamertags as one aggregate (#334): made by <see cref="XboxUser.FromXbox"/>, its past gamertags
/// recorded, widened and removed only through it (<see cref="XboxUser.Renamed"/>, <see cref="XboxUser.RecordPastGamertag"/>,
/// <see cref="XboxUser.RemovePastGamertag"/>), which refuse what no history holds.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class XboxUserAggregateTests
{
    private static DateTime Utc(int year, int month = 1) => new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public void FromXbox_KeepsTheAccount_AndRefusesABlankGamertag()
    {
        var xbox = XboxUser.FromXbox(42, "Shadow S16");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((xbox.XboxUserID, xbox.Gamertag), Is.EqualTo((42L, "Shadow S16")));
            Assert.That(xbox.PastGamertags, Is.Empty);
            Assert.That(xbox.LastRenamedAt, Is.Null);
            Assert.Throws<ArgumentException>(() => XboxUser.FromXbox(42, " "));
            Assert.Throws<ArgumentNullException>(() => XboxUser.FromXbox(42, null!));
        }
    }

    [Test]
    public void Renamed_RecordsTheOldGamertag_FromItsLastChangeUntilNow()
    {
        var xbox = XboxUser.FromXbox(1, "Old  Name");
        ((IAuditStamp)xbox).ModifiedAt = Utc(2020); // as the audit interceptor stamped its last save

        xbox.Renamed("New Name", Utc(2024));

        var past = xbox.PastGamertags.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(xbox.Gamertag, Is.EqualTo("New Name"));
            Assert.That((past.XboxUserID, past.Gamertag, past.NormalizedGamertag, past.FirstSeen, past.LastSeen, past.Source),
                Is.EqualTo((1L, "Old Name", "OLD NAME", (DateTime?)Utc(2020), (DateTime?)Utc(2024), GamertagHistorySource.RenameDetected)));
            Assert.That(past.XboxUser, Is.SameAs(xbox));
            Assert.That(xbox.LastRenamedAt, Is.EqualTo(Utc(2024)));
        }
    }

    /// <summary>The row's last change (the wall clock) after the rename's time (the caller's clock, e.g. another pod's or a
    /// fake one): the old gamertag is still recorded, dated from the rename's time.</summary>
    [Test]
    public void Renamed_ModifiedAfterTheRename_StillRecordsTheOldGamertag()
    {
        var xbox = XboxUser.FromXbox(1, "Old Name");
        ((IAuditStamp)xbox).ModifiedAt = Utc(2025);

        xbox.Renamed("New Name", Utc(2024));

        var past = xbox.PastGamertags.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(xbox.Gamertag, Is.EqualTo("New Name"));
            Assert.That((past.Gamertag, past.FirstSeen, past.LastSeen), Is.EqualTo(("Old Name", (DateTime?)Utc(2024), (DateTime?)Utc(2024))));
        }
    }

    [Test]
    public void Renamed_NeverStored_HasNoStart_AndASpellingChangeIsNoRename()
    {
        var unsaved = XboxUser.FromXbox(1, "Name");
        unsaved.Renamed("Other", Utc(2024));
        var respelled = XboxUser.FromXbox(2, "shadow s16");
        respelled.Renamed("Shadow  S16", Utc(2024));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unsaved.PastGamertags.Single().FirstSeen, Is.Null, "Never saved: when it was first used is unknown");
            Assert.That(respelled.Gamertag, Is.EqualTo("Shadow  S16"), "Xbox's spelling");
            Assert.That(respelled.PastGamertags, Is.Empty, "The same gamertag, spelled differently");
            Assert.Throws<ArgumentException>(() => respelled.Renamed("", Utc(2024)));
        }
    }

    [Test]
    public void RecordPastGamertag_AddsOnce_AndWidensTheDates_OfAGamertagItHas()
    {
        var xbox = XboxUser.FromXbox(1, "Now");

        var results = new[]
        {
            xbox.RecordPastGamertag(" Old  Tag ", Utc(2019, 6), Utc(2020), GamertagHistorySource.Manual, " WL 2019 sheet "),
            xbox.RecordPastGamertag("old tag", Utc(2018), Utc(2019), GamertagHistorySource.RosterMatch),
            xbox.RecordPastGamertag("OLD TAG", null, Utc(2021), GamertagHistorySource.RenameDetected, "  "),
        };

        var past = xbox.PastGamertags.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(results, Is.All.EqualTo(RecordPastGamertagResult.Recorded));
            Assert.That((past.Gamertag, past.NormalizedGamertag, past.FirstSeen, past.LastSeen, past.Source),
                Is.EqualTo(("Old Tag", "OLD TAG", (DateTime?)Utc(2018), (DateTime?)Utc(2021), GamertagHistorySource.Manual)), "Widened; the source it was first known by");
            Assert.That(past.Note, Is.EqualTo("WL 2019 sheet"), "A blank note leaves the note");
        }
    }

    [TestCase(null, null, 2019, 2020, 2019, 2020)]
    [TestCase(2019, 2020, null, null, 2019, 2020)]
    [TestCase(2018, 2021, 2019, 2020, 2018, 2021)]
    [TestCase(2019, 2020, 2018, 2021, 2018, 2021)]
    [TestCase(null, 2020, null, 2021, null, 2021)]
    [TestCase(2019, null, 2018, null, 2018, null)]
    public void RecordPastGamertag_Again_WidensToCoverBoth(int? firstSeen, int? lastSeen, int? newFirst, int? newLast, int? widenedFirst, int? widenedLast)
    {
        DateTime? At(int? year) => year is null ? null : Utc(year.Value);
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Tag", At(firstSeen), At(lastSeen), GamertagHistorySource.Manual);

        xbox.RecordPastGamertag("Tag", At(newFirst), At(newLast), GamertagHistorySource.Manual);

        var past = xbox.PastGamertags.Single();
        Assert.That((past.FirstSeen, past.LastSeen), Is.EqualTo((At(widenedFirst), At(widenedLast))));
    }

    [Test]
    public void LastRenamedAt_IsTheLatestEnd_OfThePastGamertagsWithOne()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Undated", null, null, GamertagHistorySource.Manual);
        Assert.That(xbox.LastRenamedAt, Is.Null, "No past gamertag has an end");

        xbox.RecordPastGamertag("Older", null, Utc(2019), GamertagHistorySource.Manual);
        xbox.RecordPastGamertag("Newer", Utc(2020), Utc(2022), GamertagHistorySource.Manual);

        Assert.That(xbox.LastRenamedAt, Is.EqualTo(Utc(2022)));
    }

    [Test]
    public void RecordPastGamertag_SeenAfterItsLastSighting_NeverStartsAfterItEnds()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Tag", null, Utc(2020), GamertagHistorySource.RenameDetected);

        xbox.RecordPastGamertag("Tag", Utc(2022), null, GamertagHistorySource.Manual);

        var past = xbox.PastGamertags.Single();
        Assert.That((past.FirstSeen, past.LastSeen), Is.EqualTo(((DateTime?)Utc(2020), (DateTime?)Utc(2020))),
            "The end, which matching reads, stays; the start is no later");
    }

    [Test]
    public void RecordPastGamertag_RefusesABlankGamertag_AndDatesOutOfOrder_AndCutsALongNote()
    {
        var xbox = XboxUser.FromXbox(1, "Now");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(xbox.RecordPastGamertag(" ", null, null, GamertagHistorySource.Manual), Is.EqualTo(RecordPastGamertagResult.NoGamertag));
            Assert.That(xbox.RecordPastGamertag("Tag", Utc(2021), Utc(2020), GamertagHistorySource.Manual), Is.EqualTo(RecordPastGamertagResult.DatesOutOfOrder));
            Assert.That(xbox.PastGamertags, Is.Empty);
            Assert.That(xbox.RecordPastGamertag("Tag", null, null, GamertagHistorySource.Manual, new string('n', 250)), Is.EqualTo(RecordPastGamertagResult.Recorded));
            Assert.That(xbox.PastGamertags.Single().Note, Has.Length.EqualTo(XboxGamertagHistory.MaxNoteLength));
        }
    }

    [Test]
    public void RemovePastGamertag_TakesOutOnlyOneAddedByHand()
    {
        var xbox = XboxUser.FromXbox(1, "Now");
        xbox.RecordPastGamertag("Manual", null, null, GamertagHistorySource.Manual);
        xbox.RecordPastGamertag("Seen", null, null, GamertagHistorySource.RenameDetected);
        var manual = xbox.PastGamertags.First();
        var seen = xbox.PastGamertags.Last();
        manual.XboxGamertagHistoryID = 10;
        seen.XboxGamertagHistoryID = 11;

        using (Assert.EnterMultipleScope())
        {
            Assert.That((manual.IsRemovable, seen.IsRemovable), Is.EqualTo((true, false)));
            Assert.That(xbox.RemovePastGamertag(99), Is.EqualTo(RemovePastGamertagResult.NotFound));
            Assert.That(xbox.RemovePastGamertag(11), Is.EqualTo(RemovePastGamertagResult.NotManual));
            Assert.That(xbox.RemovePastGamertag(10), Is.EqualTo(RemovePastGamertagResult.Removed));
            Assert.That(xbox.PastGamertags, Is.EqualTo(new[] { seen }));
        }
    }

    [TestCase(null, null, true)]
    [TestCase(2020, null, true)]
    [TestCase(null, 2019, true)]
    [TestCase(2020, 2020, true)]
    [TestCase(2020, 2019, true)]
    [TestCase(2020, 2021, false)]
    public void WasHeldAt_OnlyItsEndBoundsIt(int? lastSeen, int? from, bool held)
    {
        var past = new XboxGamertagHistory(1, "Tag", firstSeen: Utc(2030), lastSeen: lastSeen is null ? null : Utc(lastSeen.Value));

        Assert.That(past.WasHeldAt(from is null ? null : Utc(from.Value)), Is.EqualTo(held));
    }
}
