using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A VOD and its team links as one aggregate (#320): made by <see cref="Vod.Found"/>, changed only by its methods, which
/// refuse the moves no VOD makes (the matcher changing one a sysadmin decided, a link without a season).
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class VodAggregateTests
{
    private static readonly DateTime Published = new(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly int[] expected = new[] { 1, 2 };
    private static readonly int[] expectedArray = new[] { 2, 3 };

    [Test]
    public void Found_WaitsToBeMatched_WithItsTextCutToWhatARowHolds()
    {
        var vod = Vod.Found("abcdefghijk", "  " + new string('t', 300) + "  ", new string('d', 6000), Published, true, 600, "UC");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vod.MatchStatus, Is.EqualTo(VodMatchStatus.NeedsReview));
            Assert.That(vod.Title, Is.EqualTo(new string('t', Vod.TitleMaxLength)));
            Assert.That(vod.Description, Has.Length.EqualTo(Vod.DescriptionMaxLength));
            Assert.That((vod.PublishedAt, vod.PublishedAtIsApproximate, vod.DurationSeconds, vod.ChannelID), Is.EqualTo((Published, true, (int?)600, "UC")));
            Assert.That((vod.SeasonID, vod.SeasonMatchID, vod.MatchNote), Is.EqualTo(((int?)null, (int?)null, (string?)null)));
            Assert.That(vod.Teams, Is.Empty);
            Assert.That(vod.IsVisible, Is.False);
            Assert.That(vod.IsUndecided, Is.True);
            Assert.That(Vod.Found("abcdefghijk", "Title", null, Published, false, null, "UC").Description, Is.Null);
            Assert.Throws<ArgumentException>(() => Vod.Found("abcdefghijk", " ", null, Published, false, null, "UC"), "A VOD has a title");
            Assert.Throws<ArgumentException>(() => Vod.Found("", "Title", null, Published, false, null, "UC"), "And a video");
        }
    }

    [Test]
    public void Refresh_GainsWhatTheVodLacked_AndLosesNothing()
    {
        var vod = Vod.Found("abcdefghijk", "Old title", null, Published, true, null, "UC");
        var exact = Published.AddHours(5);

        var changed = vod.Refresh("New title", "A description", exact, false, 900);
        var again = vod.Refresh("New title", "A description", exact.AddDays(1), false, 1200);
        var nothing = vod.Refresh(null, null, null, true, null);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(changed, Is.True);
            Assert.That((vod.Title, vod.Description, vod.PublishedAt, vod.PublishedAtIsApproximate, vod.DurationSeconds),
                Is.EqualTo(("New title", "A description", exact, false, (int?)900)), "The exact date, once; the length it lacked, once");
            Assert.That(again, Is.False, "An exact date and a known length stay");
            Assert.That(nothing, Is.False);
        }
    }

    [Test]
    public void Rematch_LinksOrSuggests_AndSetsTheTeams()
    {
        var vod = Vod.Seed("abcdefghijk", "Alpha vs Bravo", Published, VodMatchStatus.NeedsReview, teamIDs: [1, 3]);

        vod.Rematch(VodMatchStatus.Auto, 7, 70, [1, 2, 2], new string('n', 600));

        using (Assert.EnterMultipleScope())
        {
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.SeasonMatchID), Is.EqualTo((VodMatchStatus.Auto, (int?)7, (int?)70)));
            Assert.That(vod.MatchNote, Has.Length.EqualTo(Vod.MatchNoteMaxLength));
            Assert.That(vod.Teams.Select(t => t.TeamID), Is.EquivalentTo(expected), "A team gone is taken off, a new one added once");
            Assert.That(vod.IsVisible, Is.True);
        }

        vod.Rematch(VodMatchStatus.NeedsReview, null, null, [], "Nothing recognised");
        using (Assert.EnterMultipleScope())
        {
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.SeasonMatchID, vod.MatchNote), Is.EqualTo((VodMatchStatus.NeedsReview, (int?)null, (int?)null, "Nothing recognised")));
            Assert.That(vod.Teams, Is.Empty);
        }
    }

    [Test]
    public void Rematch_RefusesTheMovesTheMatcherNeverMakes()
    {
        Vod Undecided() => Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Auto, 7);

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<InvalidOperationException>(() => Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Confirmed, 7).Rematch(VodMatchStatus.Auto, 7, null, [], "n"),
                "A sysadmin's link stays");
            Assert.Throws<InvalidOperationException>(() => Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Dismissed).Rematch(VodMatchStatus.NeedsReview, null, null, [], "n"),
                "A dismissed VOD is never suggested again");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.Confirmed, 7, null, [], "n"), "Only a sysadmin confirms");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.Dismissed, 7, null, [], "n"), "Only a sysadmin dismisses");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.Auto, null, null, [], "n"), "A linked VOD has a season");
            Assert.Throws<ArgumentException>(() => Undecided().Rematch(VodMatchStatus.NeedsReview, null, 70, [], "n"), "A match is of a season");
        }
    }

    [Test]
    public void Confirm_LinksAnyVod_AndDismiss_HidesIt()
    {
        var vod = Vod.Seed("abcdefghijk", "Title", Published, VodMatchStatus.Dismissed, 7, teamIDs: [1]);

        vod.Confirm(8, 80, [2, 3]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((vod.MatchStatus, vod.SeasonID, vod.SeasonMatchID), Is.EqualTo((VodMatchStatus.Confirmed, (int?)8, (int?)80)), "Even a dismissed VOD");
            Assert.That(vod.Teams.Select(t => t.TeamID), Is.EquivalentTo(expectedArray));
            Assert.That((vod.IsVisible, vod.IsUndecided), Is.EqualTo((true, false)));
        }

        vod.Dismiss();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(vod.MatchStatus, Is.EqualTo(VodMatchStatus.Dismissed));
            Assert.That((vod.IsVisible, vod.IsUndecided), Is.EqualTo((false, false)));
            Assert.That(vod.Teams.Select(t => t.TeamID), Is.EquivalentTo(expectedArray), "Its links stay as they were");
        }
    }
}
