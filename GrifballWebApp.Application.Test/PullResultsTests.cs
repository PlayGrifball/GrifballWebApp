using GrifballWebApp.Application.Extensions;
using GrifballWebApp.Application.HaloInfinite;
using GrifballWebApp.Application.TeamPage;

namespace GrifballWebApp.Application.Test;

/// <summary>
/// What a Halo pull and a game variant backfill say they did (MatchPullResult, VariantBackfillResult: the summaries the
/// jobs' status and the Discord replies show), the franchise a team is of (FranchiseKey.Of), and a time as Discord
/// shows it (DateTimeExtensions).
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
internal sealed class PullResultsTests
{
    private static readonly Guid GameA = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid GameB = Guid.Parse("00000000-0000-0000-0000-00000000000b");

    [Test]
    public void MatchPullResult_EverythingFetched_IsCompleteAndSaysOnlyTheCounts()
    {
        var result = new MatchPullResult(Players: 3, FailedHistories: [], MatchesFound: 10, MatchesAlreadySaved: 4, MatchesFetched: 6, FailedMatches: []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Complete, Is.True);
            Assert.That(result.Summary, Is.EqualTo("3 players, 10 Grifball games found, 4 saved before, 6 new saved"));
        }
    }

    [Test]
    public void MatchPullResult_GamesAndHistoriesMissing_IsIncompleteAndSaysWhatIsMissing()
    {
        var result = new MatchPullResult(Players: 2, FailedHistories: ["Alpha"], MatchesFound: 5, MatchesAlreadySaved: 1, MatchesFetched: 2,
            FailedMatches: [GameA, GameB], MatchesNotTeamGames: 1, CappedHistories: ["Alpha", "Bravo"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Complete, Is.False);
            Assert.That(result.Summary, Is.EqualTo("2 players, 5 Grifball games found, 1 saved before, 2 new saved"
                + "; 2 games Halo didn't give, not saved (the next pull tries them again)"
                + "; 1 players' match histories incomplete"
                + "; 1 not team games, not saved"
                + "; 2 players' histories too long to search back to the season"));
        }
    }

    [Test]
    public void MatchPullResult_OnlyAHistoryMissing_IsIncomplete()
    {
        var result = new MatchPullResult(1, ["Alpha"], 0, 0, 0, []);

        Assert.That(result.Complete, Is.False);
    }

    [Test]
    public void MatchPullResult_NoCappedHistories_SaysNothingOfThem()
    {
        var result = new MatchPullResult(1, [], 1, 0, 1, [], CappedHistories: []);

        Assert.That(result.Summary, Does.Not.Contain("too long"));
    }

    [Test]
    public void MatchPullResult_Empty_FoundNothing()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(MatchPullResult.Empty.Complete, Is.True);
            Assert.That(MatchPullResult.Empty.Summary, Is.EqualTo("0 players, 0 Grifball games found, 0 saved before, 0 new saved"));
        }
    }

    [Test]
    public void VariantBackfillResult_AllFilled_LeavesTheRestAndSaysOnlyTheCounts()
    {
        var result = new VariantBackfillResult(Missing: 10, Attempted: 4, Filled: 4, NoVariant: 0, Failed: []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Left, Is.EqualTo(6));
            Assert.That(result.Summary, Is.EqualTo("4 of 4 matches given their game variant, 6 left"));
        }
    }

    [Test]
    public void VariantBackfillResult_FailedAndWithoutVariant_SaysBoth()
    {
        var result = new VariantBackfillResult(Missing: 5, Attempted: 5, Filled: 2, NoVariant: 1, Failed: [GameA, GameB]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Left, Is.EqualTo(3));
            Assert.That(result.Summary, Is.EqualTo("2 of 5 matches given their game variant, 3 left"
                + "; 2 Halo didn't give (a later run tries them again); 1 without a variant"));
        }
    }

    [Test]
    public void FranchiseKey_SameNameInAnyCaseAndSpacing_IsOneFranchise()
    {
        var first = FranchiseKey.Of(teamID: 1, leagueID: 7, teamName: "The  Hammers");
        var later = FranchiseKey.Of(teamID: 2, leagueID: 7, teamName: " the hammers ");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(later, Is.EqualTo(first));
            Assert.That(first.TeamID, Is.Null, "a named team is keyed by its name, not its own ID");
        }
    }

    [Test]
    public void FranchiseKey_SameNameInAnotherLeague_IsAnotherFranchise()
    {
        var one = FranchiseKey.Of(teamID: 1, leagueID: 7, teamName: "Hammers");
        var other = FranchiseKey.Of(teamID: 2, leagueID: 8, teamName: "Hammers");

        Assert.That(other, Is.Not.EqualTo(one));
    }

    [TestCase("Team 1")]
    [TestCase("Team #2")]
    [TestCase("team b")]
    public void FranchiseKey_PlaceholderName_IsTheTeamsOwn(string placeholder)
    {
        var first = FranchiseKey.Of(teamID: 1, leagueID: 7, teamName: placeholder);
        var later = FranchiseKey.Of(teamID: 2, leagueID: 7, teamName: placeholder);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(later, Is.Not.EqualTo(first), "two seasons' placeholder teams are not one franchise");
            Assert.That(first.Name, Is.Null);
            Assert.That(first.TeamID, Is.EqualTo(1));
        }
    }

    [Test]
    public void ToUnix_ReadsTheTimeAsUtc()
    {
        var at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        Assert.That(at.ToUnix(), Is.EqualTo(1767225600));
    }

    [Test]
    public void DiscordTimeEmbed_IsDiscordsTimestampTag()
    {
        var at = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        Assert.That(at.DiscordTimeEmbed(), Is.EqualTo("<t:1767225600>"));
    }
}
