using GrifballWebApp.Domain.Archive;
using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.LateLeague;
using GrifballWebApp.Domain.League;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;
using static GrifballWebApp.Domain.Test.SeasonPhaseTestData;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// The aggregates' rules for queries (the expressions the database runs), run in memory on aggregates in each state: each
/// says what its loaded counterpart says, and what its own summary says. No database, host, file or network.
/// </summary>
[TestFixture]
[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
[Parallelizable(ParallelScope.All)]
public class QueryRulesUnitTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public void SeasonPhaseQueries_AgreeWithTheLoadedSeason_AtEveryBoundary()
    {
        var season = Scheduled();
        var times = AroundEachDate().ToList();

        bool[] Run(Func<Season, bool> rule) => [.. times.Select(_ => rule(season))];
        bool[] RunAt(Func<DateTime, System.Linq.Expressions.Expression<Func<Season, bool>>> query) =>
            [.. times.Select(t => query(t).Compile()(season))];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(RunAt(Season.SignupsOpenAt), Is.EqualTo(times.Select(season.SignupsAreOpenAt)));
            Assert.That(RunAt(Season.SignupsNotClosedAt), Is.EqualTo(times.Select(t => t < Close)));
            Assert.That(RunAt(Season.NotStartedAt), Is.EqualTo(times.Select(t => season.PhaseAt(t) is not (SeasonPhase.Running or SeasonPhase.Ended))));
            Assert.That(RunAt(Season.StartedAt), Is.EqualTo(times.Select(t => season.PhaseAt(t) is SeasonPhase.Running or SeasonPhase.Ended)));
            Assert.That(RunAt(Season.RunningAt), Is.EqualTo(times.Select(season.IsRunningAt)));
            Assert.That(RunAt(Season.EndedAt), Is.EqualTo(times.Select(season.HasEndedAt)));
            Assert.That(RunAt(Season.NotEndedAt), Is.EqualTo(times.Select(t => !season.HasEndedAt(t))));
            Assert.That(RunAt(Season.AnnouncedAt), Is.EqualTo(Run(_ => false)), "Never announced: PublicAt is null");
            Assert.That(RunAt(Season.PublicAsOf), Is.EqualTo(times.Select(season.IsPublicAsOf)), "Public once started");
        }
    }

    [Test]
    public void SeasonAnnouncement_MakesItPublicBeforeItStarts_InTheQueryAsOnTheSeason()
    {
        var announced = Draft;
        var season = Scheduled().WithDates(publicAt: announced);
        var times = AroundEachDate().ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(times.Select(t => Season.AnnouncedAt(t).Compile()(season)), Is.EqualTo(times.Select(t => t >= announced)));
            Assert.That(times.Select(t => Season.PublicAsOf(t).Compile()(season)), Is.EqualTo(times.Select(season.IsPublicAsOf)));
            Assert.That(season.IsPublicAsOf(Draft.AddMilliseconds(-1)), Is.False, "Not before its announcement");
            Assert.That(Season.PublicAsOf(Draft).Compile()(season), Is.True, "From its announcement, before its start");
        }
    }

    [Test]
    public void VodQueries_AgreeWithTheLoadedVod_InEveryStatus()
    {
        var vods = Enum.GetValues<VodMatchStatus>().Select(s => Vod.Seed($"v{(int)s}", "Week 1", Now, s)).ToList();
        var visible = Vod.Visible.Compile();
        var undecided = Vod.Undecided.Compile();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vods.Where(visible).Select(v => v.MatchStatus), Is.EqualTo(new List<VodMatchStatus> { VodMatchStatus.Auto, VodMatchStatus.Confirmed }));
            Assert.That(vods.Select(visible), Is.EqualTo(vods.Select(v => v.IsVisible)));
            Assert.That(vods.Where(undecided).Select(v => v.MatchStatus), Is.EqualTo(new List<VodMatchStatus> { VodMatchStatus.Auto, VodMatchStatus.NeedsReview }));
            Assert.That(vods.Select(undecided), Is.EqualTo(vods.Select(v => v.IsUndecided)));
        }
    }

    [TestCase(300, false, true)]
    [TestCase(null, false, false)]
    [TestCase(300, true, false)]
    public void VodComplete_NeedsItsLengthAndAnExactDate(int? durationSeconds, bool approximate, bool complete)
    {
        var vod = Vod.Found("abc", "Week 1", null, Now, approximate, durationSeconds, "UC1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(vod.IsComplete, Is.EqualTo(complete));
            Assert.That(Vod.Complete.Compile()(vod), Is.EqualTo(complete), "The query's rule is the loaded one");
        }
    }

    [Test]
    public void MatchQueries_AgreeWithTheLoadedMatch_ForEachLayoutAndVariant()
    {
        var matches = new List<Match> { Match.Seed(Guid.NewGuid()) };
        matches.AddRange(Enum.GetValues<CustomScoreLayout>().Select(l => Match.Seed(Guid.NewGuid(), scoreLayout: l)));
        matches.Add(Match.Seed(Guid.NewGuid(), ugcGameVariantAssetID: Guid.NewGuid(), ugcGameVariantVersionID: Guid.NewGuid()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches.Select(Match.WithHalosPoints.Compile()), Is.EqualTo(matches.Select(m => m.HasHalosPoints)));
            Assert.That(matches.Where(m => m.HasHalosPoints).Select(m => m.ScoreLayout),
                Is.EquivalentTo(new List<CustomScoreLayout?> { null, null, CustomScoreLayout.Plain }), "Not decoded yet, or plain: Halo's points");
            Assert.That(matches.Select(Match.NotDecoded.Compile()), Is.EqualTo(matches.Select(m => !m.IsDecoded)));
            Assert.That(matches.Select(Match.WithGameVariant.Compile()), Is.EqualTo(matches.Select(m => m.HasGameVariant)));
            Assert.That(matches.Select(Match.WithoutGameVariant.Compile()), Is.EqualTo(matches.Select(m => !m.HasGameVariant)));
            Assert.That(matches.Count(m => m.HasGameVariant), Is.EqualTo(1));
        }
    }

    [Test]
    public void MatchedMatchQueries_SayWhichMatchesHaveAResult_AndWhoPlaysInThem()
    {
        var active = MatchedMatch.Start([1, 2], [3, 4], Now);
        var reported = MatchedMatch.Start([1, 5], [6, 7], Now).Reported(Guid.NewGuid());
        var canceled = MatchedMatch.Start([8], [9], Now).Canceled();
        var archived = MatchedMatchTestExtensions.ArchiveGame(1, Now, [10], [11]);
        var matches = new List<MatchedMatch> { active, reported, canceled, archived };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches.Where(MatchedMatch.PlayedToAResult.Compile()), Is.EqualTo(new List<MatchedMatch> { reported, archived }));
            Assert.That(matches.Where(MatchedMatch.ReportedFromHalo.Compile()), Is.EqualTo(new List<MatchedMatch> { reported }),
                "An archive game's result is its recorded game, not Halo's");
            Assert.That(matches.Where(MatchedMatch.DecidedByVote.Compile()), Is.Empty, "None was decided by its players");
            Assert.That(matches.Where(MatchedMatch.HasPlayer(1).Compile()), Is.EqualTo(new List<MatchedMatch> { active, reported }), "Home or away");
            Assert.That(matches.Where(MatchedMatch.HasPlayer(11).Compile()), Is.EqualTo(new List<MatchedMatch> { archived }));
            Assert.That(matches.Where(MatchedMatch.HasPlayer(99).Compile()), Is.Empty);
        }
    }

    [Test]
    public void PasswordResetLinkQueries_FindTheLinkByItsToken_AndAgreeWithItsUse()
    {
        var (link, token) = PasswordResetLink.Issue(4, Now);
        var expiry = Now + PasswordResetLink.Lifetime;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(PasswordResetLink.OfToken(token).Compile()(link), Is.True, "Found by the token it was issued with");
            Assert.That(PasswordResetLink.OfToken(token + "x").Compile()(link), Is.False);
            Assert.That(link.Token, Is.Not.EqualTo(token), "Only the token's hash is stored");
            Assert.That((PasswordResetLink.Usable(Now).Compile()(link), PasswordResetLink.Spent(Now).Compile()(link)), Is.EqualTo((true, false)));
            Assert.That((PasswordResetLink.Usable(expiry).Compile()(link), PasswordResetLink.Spent(expiry).Compile()(link)), Is.EqualTo((false, true)),
                "Spent at its expiry exactly");
            Assert.That(PasswordResetLink.Usable(Now).Compile()(link), Is.EqualTo(link.IsUsableAt(Now)));
            Assert.That(PasswordResetLink.OfToken("seeded").Compile()(PasswordResetLink.Seed(4, "seeded", expiry)), Is.True,
                "A seeded link stores its token hashed, as an issued one does");
        }

        var used = link.Use(Now.AddMinutes(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(used, Is.EqualTo(PasswordResetLinkUse.Ok));
            Assert.That((PasswordResetLink.Usable(Now.AddMinutes(2)).Compile()(link), PasswordResetLink.Spent(Now.AddMinutes(2)).Compile()(link)),
                Is.EqualTo((false, true)), "Used once: spent, though not expired");
            Assert.That(link.IsUsableAt(Now.AddMinutes(2)), Is.False);
        }
    }

    [Test]
    public void TeamQueries_ArchivedRecordAndPicks_AgreeWithTheLoadedTeam()
    {
        var archived = Team.Seed("Archived", recordedWins: 5, recordedLosses: 2);
        var played = Team.Seed("Played");
        var drafted = Team.Seed("Drafted", teamID: 3);
        var captain = drafted.SeedPlayer(userID: 1);
        captain.TeamPlayerID = 10;
        drafted.SetCaptain(1);
        var pick = drafted.SeedPlayer(userID: 2, draftRound: 1);
        pick.TeamPlayerID = 11;
        var hasRecord = Team.HasArchivedRecord.Compile();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((hasRecord(archived), hasRecord(played)), Is.EqualTo((true, false)));
            Assert.That((archived.IsRecordArchived, played.IsRecordArchived), Is.EqualTo((true, false)));
            Assert.That(Team.PicksOf.Compile()(drafted), Is.EqualTo(drafted.Picks), "The players but the captain");
            Assert.That(drafted.Picks, Is.EqualTo(new List<TeamPlayer> { pick }));
        }

        archived.ClearArchivedRecord();
        Assert.That((hasRecord(archived), archived.IsRecordArchived), Is.EqualTo((false, false)), "Its record is its matches' again");
    }

    [Test]
    public void UserMergeQuery_LaterStandingMergesOfItsUsers()
    {
        var merge = UserMergeTestData.Merge(source: 1, target: 2);
        merge.UserMergeID = 10;
        UserMerge Later(int id, int source, int target, bool undone = false)
        {
            var later = UserMergeTestData.Merge(source, target);
            later.UserMergeID = id;
            if (undone)
                later.Undo(UserMergeTestData.MergedAt.AddDays(1), 7, "wrong");
            return later;
        }
        var candidates = new List<UserMerge>
        {
            Later(11, 2, 3), Later(12, 4, 1), Later(13, 5, 6), Later(9, 2, 3), Later(14, 1, 7, undone: true), Later(15, 8, 2),
        };

        var dependents = candidates.Where(merge.LaterStandingMergesOfItsUsers().Compile()).Select(m => m.UserMergeID);

        Assert.That(dependents, Is.EqualTo(new List<int> { 11, 12, 15 }),
            "After it, standing, and into or out of either of its users: not 13 (other users), 9 (before it), 14 (undone)");
    }

    [Test]
    public void DuplicateDismissalQuery_FindsThePairEitherWayRound()
    {
        var dismissal = DuplicateDismissal.Dismiss(3, 8, Now, 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((DuplicateDismissal.Of(3, 8).Compile()(dismissal), DuplicateDismissal.Of(8, 3).Compile()(dismissal)), Is.EqualTo((true, true)));
            Assert.That(DuplicateDismissal.Of(3, 9).Compile()(dismissal), Is.False);
            Assert.That(DuplicateDismissal.Of(8, 3).Compile()(dismissal), Is.EqualTo(dismissal.IsOf(8, 3)));
        }
    }

    [Test]
    public void GamertagHistoryQuery_HeldAt_OnlyLastSeenBoundsIt()
    {
        var stillHeld = new XboxGamertagHistory(1, "Old Tag", firstSeen: Now, lastSeen: null);
        var letGo = new XboxGamertagHistory(1, "Older Tag", firstSeen: Now.AddYears(-2), lastSeen: Now);
        var heldAt = (DateTime? from) => XboxGamertagHistory.HeldAt(from).Compile();

        using (Assert.EnterMultipleScope())
        {
            Assert.That((heldAt(Now.AddYears(-5))(stillHeld), heldAt(Now.AddYears(5))(stillHeld)), Is.EqualTo((true, true)), "Never let go of");
            Assert.That((heldAt(Now)(letGo), heldAt(Now.AddTicks(1))(letGo)), Is.EqualTo((true, false)), "Held up to when it was last seen");
            Assert.That(heldAt(Now.AddYears(-5))(letGo), Is.True, "Before it was first seen counts: FirstSeen isn't when it was taken");
            Assert.That(heldAt(null)(letGo), Is.True, "An unknown time can't rule it out");
        }
    }

    [Test]
    public void PlayerSeasonStatQuery_ClashesOnMerge_TheTargetsRowsInTheSameSlot()
    {
        var totals = new StatSheetTotals { Gamertag = "Shadow" };
        var fromRow = PlayerSeasonStat.Seed(totals, seasonID: 1, userID: 5);
        var fromPlayoffs = PlayerSeasonStat.Seed(totals, seasonID: 2, userID: 5, isPlayoffs: true);
        var clash = PlayerSeasonStat.Seed(totals, seasonID: 1, userID: 6);
        var otherStint = PlayerSeasonStat.Seed(totals, seasonID: 1, userID: 6, stintIndex: 2);
        var regular = PlayerSeasonStat.Seed(totals, seasonID: 2, userID: 6);
        var someoneElse = PlayerSeasonStat.Seed(totals, seasonID: 1, userID: 7);
        var rows = new List<PlayerSeasonStat> { fromRow, fromPlayoffs, clash, otherStint, regular, someoneElse };

        var clashes = rows.Where(PlayerSeasonStat.ClashesOnMerge(rows.AsQueryable(), fromUserID: 5, toUserID: 6).Compile()).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(clashes, Is.EqualTo(new List<PlayerSeasonStat> { clash }), "Same season, stint and playoffs, of the target");
            Assert.That(rows.Where(r => r.UserID == 6).Select(r => fromRow.ClashesWith(r)), Is.EqualTo(new List<bool> { true, false, false }),
                "The loaded rule agrees");
        }
    }

    [Test]
    public void HistoricalImportJobQueries_AQueuedJobIsActiveNotEnded()
    {
        var jobs = new List<HistoricalImportJob> { HistoricalImportJob.Upload("{}"), HistoricalImportJob.Refresh(), HistoricalImportJob.Preview() };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(jobs.Select(j => j.Status), Is.All.EqualTo(HistoricalImportJobStatus.Queued));
            Assert.That(jobs.Select(HistoricalImportJob.Active.Compile()), Is.All.True, "A scheduled run waits for a queued job");
            Assert.That(jobs.Select(HistoricalImportJob.Ended.Compile()), Is.All.False);
            Assert.That(jobs.Select(j => (j.Stage, j.SeasonsDone, j.SeasonsTotal, j.Report)),
                Is.All.EqualTo((HistoricalImportJob.QueuedStage, 0, 0, (string?)null)), "Nothing run yet");
            Assert.That(jobs.Select(j => (j.Error, j.StartedAt, j.HeartbeatAt, j.FinishedAt)),
                Is.All.EqualTo(((string?)null, (DateTime?)null, (DateTime?)null, (DateTime?)null)));
        }
    }
}
