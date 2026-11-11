using GrifballWebApp.Domain.Halo;
using GrifballWebApp.Domain.Players;
using GrifballWebApp.Domain.Shared;

namespace GrifballWebApp.Domain.Test;

/// <summary>
/// A Halo Infinite match, its teams, their players and their medals as one aggregate (#332): made by
/// <see cref="Match.FromHalo"/> with its teams, its players added through it, and changed only by its methods (the
/// backfill's variant, the decoder's layout), which refuse the moves no match makes.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HaloMatchAggregateTests
{
    private static readonly DateTime Start = new(2026, 3, 2, 20, 0, 0, DateTimeKind.Utc);
    private static readonly long[] expected = new[] { 42L };

    private static Match Pulled(Guid? asset = null, Guid? version = null) =>
        Match.FromHalo(Guid.NewGuid(), Start, Start.AddMinutes(10), TimeSpan.FromMinutes(10), Start.AddHours(1), asset, version,
            [new MatchTeamResult(0, 5, Outcomes.Won), new MatchTeamResult(1, 3, Outcomes.Lost)]);

    [Test]
    public void FromHalo_KeepsTheMatchWithItsTeams()
    {
        var (asset, version) = (Guid.NewGuid(), Guid.NewGuid());
        var match = Pulled(asset, version);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.StartTime, match.EndTime, match.Duration, match.StatsPullDate),
                Is.EqualTo(((DateTime?)Start, (DateTime?)Start.AddMinutes(10), TimeSpan.FromMinutes(10), (DateTime?)Start.AddHours(1))));
            Assert.That((match.UgcGameVariantAssetID, match.UgcGameVariantVersionID), Is.EqualTo(((Guid?)asset, (Guid?)version)));
            Assert.That(match.HasGameVariant, Is.True);
            Assert.That((match.ScoreLayout, match.IsDecoded), Is.EqualTo(((CustomScoreLayout?)null, false)), "Decoded once its players are in");
            Assert.That(match.MatchTeams.Select(t => (t.MatchID, t.TeamID, t.Score, t.Outcome)),
                Is.EqualTo(new[] { (match.MatchID, 0, 5, Outcomes.Won), (match.MatchID, 1, 3, Outcomes.Lost) }));
            Assert.That(match.MatchTeams.Select(t => t.Match), Is.All.SameAs(match));
            Assert.That(match.Participants, Is.Empty);
            Assert.That(Pulled().HasGameVariant, Is.False, "Saved without one: the backfill's to fetch");
        }
    }

    [Test]
    public void FromHalo_RefusesAMatchNoTeamGameMakes()
    {
        Match With(Guid? asset, Guid? version, params MatchTeamResult[] teams) =>
            Match.FromHalo(Guid.NewGuid(), Start, null, TimeSpan.Zero, Start, asset, version, teams);

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentException>(() => With(null, null), "No teams: a free for all isn't kept");
            Assert.Throws<ArgumentException>(() => With(Guid.NewGuid(), null, new MatchTeamResult(0, 1, Outcomes.Won)), "An asset without its version");
            Assert.Throws<ArgumentException>(() => With(null, Guid.NewGuid(), new MatchTeamResult(0, 1, Outcomes.Won)), "A version without its asset");
            Assert.Throws<ArgumentException>(() => With(null, null, new MatchTeamResult(0, 1, Outcomes.Won), new MatchTeamResult(0, 2, Outcomes.Lost)), "A team twice");
            Assert.Throws<ArgumentOutOfRangeException>(() => With(null, null, new MatchTeamResult(0, 1, (Outcomes)0)), "An outcome Halo doesn't give");
        }
    }

    [Test]
    public void AddParticipant_PutsThePlayerOnTheirTeam_WithTheirStatsAndMedals()
    {
        var match = Pulled();
        var xbox = XboxUser.FromXbox(42, "Chief");
        var stats = new MatchParticipantStats
        {
            Score = 3, PersonalScore = 900, Kills = 12, Deaths = 4, Assists = 2, Kda = 10.67f, Accuracy = 51.5f, ShotsFired = 200, ShotsHit = 103,
            AverageLife = TimeSpan.FromSeconds(30), TimePlayed = TimeSpan.FromMinutes(10), FirstJoinedTime = Start, LastLeaveTime = Start.AddMinutes(10),
            PresentAtBeginning = true, PresentAtCompletion = true, Rank = 1, Spawns = 5, ObjectivesCompleted = 3, MaxKillingSpree = 4,
        };

        var player = match.AddParticipant(1, 42, stats, [new MedalCount(77, 2, 50), new MedalCount(78, 1)], xbox);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((player.MatchID, player.TeamID, player.XboxUserID), Is.EqualTo((match.MatchID, 1, 42L)));
            Assert.That(player.MatchTeam, Is.SameAs(match.MatchTeams.Single(t => t.TeamID == 1)));
            Assert.That(player.MatchTeam.MatchParticipants, Is.EqualTo(new[] { player }));
            Assert.That(player.XboxUser, Is.SameAs(xbox));
            Assert.That(match.Participants, Is.EqualTo(new[] { player }));
            Assert.That((player.Score, player.PersonalScore, player.Kills, player.Deaths, player.Assists, player.Kda, player.Accuracy, player.ShotsFired, player.ShotsHit),
                Is.EqualTo((3, 900, 12, 4, 2, 10.67f, 51.5f, 200, 103)));
            Assert.That((player.AverageLife, player.TimePlayed, player.FirstJoinedTime, player.LastLeaveTime),
                Is.EqualTo((TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10), Start, (DateTime?)Start.AddMinutes(10))));
            Assert.That((player.PresentAtBeginning, player.PresentAtCompletion, player.JoinedInProgress, player.LeftInProgress), Is.EqualTo((true, true, false, false)));
            Assert.That((player.Rank, player.Spawns, player.ObjectivesCompleted, player.MaxKillingSpree), Is.EqualTo((1, 5, 3, 4)));
            Assert.That(player.Goals, Is.Null, "Not decoded yet");
            Assert.That(player.MedalEarned.Select(m => (m.MatchID, m.XboxUserID, m.MedalID, m.Count, m.TotalPersonalScoreAwarded)),
                Is.EqualTo(new[] { (match.MatchID, 42L, 77L, 2, 50), (match.MatchID, 42L, 78L, 1, 0) }));
            Assert.That(player.MedalEarned.Select(m => m.MatchParticipant), Is.All.SameAs(player));
        }
    }

    [Test]
    public void AddParticipant_RefusesAPlayerNoMatchHas()
    {
        var match = Pulled();
        match.AddParticipant(0, 42, new MatchParticipantStats());

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<InvalidOperationException>(() => match.AddParticipant(2, 43, new MatchParticipantStats()), "A team the match doesn't have");
            Assert.Throws<InvalidOperationException>(() => match.AddParticipant(1, 42, new MatchParticipantStats()), "A player twice, on either team");
            Assert.Throws<ArgumentException>(() => match.AddParticipant(1, 44, new MatchParticipantStats(), [new MedalCount(77, 1), new MedalCount(77, 2)]), "A medal counted twice");
            Assert.Throws<ArgumentException>(() => match.AddParticipant(1, 45, new MatchParticipantStats(), xboxUser: XboxUser.FromXbox(46, "Other")), "Someone else's Xbox user");
            Assert.That(match.Participants.Select(p => p.XboxUserID), Is.EqualTo(expected), "Nothing refused was added");
        }
    }

    [Test]
    public void GiveGameVariant_FillsInAMissingVariant_AndNeverChangesOne()
    {
        var (asset, version) = (Guid.NewGuid(), Guid.NewGuid());
        var match = Pulled();

        match.GiveGameVariant(asset, version);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((match.UgcGameVariantAssetID, match.UgcGameVariantVersionID, match.HasGameVariant), Is.EqualTo(((Guid?)asset, (Guid?)version, true)));
            Assert.Throws<InvalidOperationException>(() => match.GiveGameVariant(Guid.NewGuid(), Guid.NewGuid()), "The variant it was played on");
            Assert.Throws<InvalidOperationException>(() => Pulled(asset, version).GiveGameVariant(asset, version));
            Assert.That(match.UgcGameVariantVersionID, Is.EqualTo(version));
        }
    }

    [Test]
    public void Decode_SetsTheLayoutAndEachPlayersStats_AndSaysWhetherAnythingChanged()
    {
        var match = Pulled();
        var scorer = match.AddParticipant(0, 1, new MatchParticipantStats { Score = 201000054, PersonalScore = 201000054 });
        var other = match.AddParticipant(1, 2, new MatchParticipantStats());
        var decoded = new Dictionary<MatchParticipant, DecodedScore>
        {
            [scorer] = new(2, CarrierKills: 1, BallPunches: 0, BallHeldTime: TimeSpan.FromSeconds(108)),
            [other] = DecodedScore.None,
        };

        var first = match.Decode(CustomScoreLayout.B, decoded);
        var again = match.Decode(CustomScoreLayout.B, decoded);
        var relaid = match.Decode(CustomScoreLayout.Undecoded, new Dictionary<MatchParticipant, DecodedScore> { [scorer] = DecodedScore.None, [other] = DecodedScore.None });

        using (Assert.EnterMultipleScope())
        {
            Assert.That((first, again, relaid), Is.EqualTo((true, false, true)), "The same result changes nothing");
            Assert.That((match.ScoreLayout, match.IsDecoded), Is.EqualTo(((CustomScoreLayout?)CustomScoreLayout.Undecoded, true)));
            Assert.That((scorer.Goals, scorer.CarrierKills, scorer.BallHeldTime), Is.EqualTo(((int?)null, (int?)null, (TimeSpan?)null)), "Decoded again: nothing kept from before");
            Assert.That((scorer.Score, scorer.PersonalScore), Is.EqualTo((201000054, 201000054)), "The raw values stay");
        }

        match.Decode(CustomScoreLayout.B, decoded);
        Assert.That((scorer.Goals, scorer.CarrierKills, scorer.BallPunches, scorer.BallHeldTime, other.Goals),
            Is.EqualTo(((int?)2, (int?)1, (int?)0, (TimeSpan?)TimeSpan.FromSeconds(108), (int?)null)));
    }

    [Test]
    public void Decode_RefusesAResultThatIsntForEachOfItsPlayers()
    {
        var match = Pulled();
        var player = match.AddParticipant(0, 1, new MatchParticipantStats());
        var stranger = Pulled().AddParticipant(0, 1, new MatchParticipantStats());

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentException>(() => match.Decode(CustomScoreLayout.Plain, new Dictionary<MatchParticipant, DecodedScore>()), "A player left out");
            Assert.Throws<ArgumentException>(() => match.Decode(CustomScoreLayout.Plain, new Dictionary<MatchParticipant, DecodedScore> { [stranger] = DecodedScore.None }), "Another match's player");
            Assert.Throws<ArgumentException>(() => match.Decode(CustomScoreLayout.Plain, new Dictionary<MatchParticipant, DecodedScore> { [player] = DecodedScore.None, [stranger] = DecodedScore.None }), "One too many");
            Assert.Throws<ArgumentOutOfRangeException>(() => match.Decode((CustomScoreLayout)9, new Dictionary<MatchParticipant, DecodedScore> { [player] = DecodedScore.None }), "A layout the model doesn't know");
            Assert.That(match.IsDecoded, Is.False, "Nothing refused was set");
        }
    }

    [Test]
    public void CountedGoals_AreTheDecodedGoals_OrAnUndecodedScoreOfUpTo20()
    {
        var count = MatchParticipant.CountedGoals.Compile();
        MatchParticipant Player(int score, CustomScoreLayout? layout = null, int? goals = null)
        {
            var match = Pulled();
            var player = match.AddParticipant(0, 1, new MatchParticipantStats { Score = score });
            if (layout is { } decided)
                match.Decode(decided, new Dictionary<MatchParticipant, DecodedScore> { [player] = new(goals) });
            return player;
        }

        using (Assert.EnterMultipleScope())
        {
            Assert.That(count(Player(Match.PlainMaxScore)), Is.EqualTo(Match.PlainMaxScore));
            Assert.That(count(Player(Match.PlainMaxScore + 1)), Is.Null, "A packed score isn't goals");
            Assert.That(count(Player(504_010_048, CustomScoreLayout.B, 5)), Is.EqualTo(5));
            Assert.That(count(Player(3, CustomScoreLayout.Undecoded)), Is.Null, "Decoded: only what the layout gives");
        }
    }
}
