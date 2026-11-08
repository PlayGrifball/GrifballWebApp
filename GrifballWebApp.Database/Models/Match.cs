using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A Halo Infinite match, as Halo's stats API gave it: the root of its teams (<see cref="MatchTeam"/>), their players
/// (<see cref="MatchParticipant"/>) and the medals they earned (<see cref="Models.MedalEarned"/>), which are made only through it.
/// <list type="bullet">
/// <item>pulled (<see cref="FromHalo"/>), with its teams; its players are added as they're resolved (<see cref="AddParticipant"/>);</item>
/// <item>given its game variant when it's fetched again (<see cref="GiveGameVariant"/>), if it was saved before it was kept;</item>
/// <item>decoded (<see cref="Decode"/>): its layout and the stats it packs into its players' scores (#217).</item>
/// </list>
/// Its variant is whole or missing (CK_Infinite_Matches_GameVariantWhole), and its layout is one the model knows
/// (CK_Infinite_Matches_KnownScoreLayout).
/// </summary>
[AggregateRoot]
public class Match : AuditableEntity
{
    /// <summary>The most a Score can be and still be goals: a <see cref="CustomScoreLayout.Plain"/> match's, and what an
    /// undecoded match's goals are read from (<see cref="MatchParticipant.CountedGoals"/>).</summary>
    public const int PlainMaxScore = 20;

    /// <summary>For EF. A match begins through <see cref="FromHalo"/>.</summary>
    private Match() { }

    /// <summary>
    /// A match as Halo's stats API gives it, with its teams; its players are added after (<see cref="AddParticipant"/>),
    /// once each is resolved. A match without teams (free for all) isn't kept, and a variant is its asset and that
    /// asset's version, or neither.
    /// </summary>
    public static Match FromHalo(Guid matchID, DateTime? startTime, DateTime? endTime, TimeSpan duration, DateTime pulledAt,
        Guid? ugcGameVariantAssetID, Guid? ugcGameVariantVersionID, IEnumerable<MatchTeamResult> teams)
    {
        ArgumentNullException.ThrowIfNull(teams);
        if ((ugcGameVariantAssetID is null) != (ugcGameVariantVersionID is null))
            throw new ArgumentException("A game variant is its asset and that asset's version, or neither", nameof(ugcGameVariantVersionID));

        var match = new Match
        {
            MatchID = matchID,
            StartTime = startTime,
            EndTime = endTime,
            Duration = duration,
            StatsPullDate = pulledAt,
            UgcGameVariantAssetID = ugcGameVariantAssetID,
            UgcGameVariantVersionID = ugcGameVariantVersionID,
        };
        foreach (var team in teams)
            match.AddTeam(team.TeamID, team.Score, team.Outcome);
        if (match._matchTeams.Count == 0)
            throw new ArgumentException("A match without teams isn't kept", nameof(teams));
        return match;
    }

    /// <summary>
    /// The same in any state, past the rules: for tests, which seed matches as a pull and the decoder leave them (and old
    /// rows for the migration tests). Teams are added with <see cref="AddTeam"/>.
    /// </summary>
    internal static Match Seed(Guid matchID, DateTime? startTime = null, DateTime? endTime = null, TimeSpan duration = default,
        DateTime? statsPullDate = null, CustomScoreLayout? scoreLayout = null, Guid? ugcGameVariantAssetID = null, Guid? ugcGameVariantVersionID = null) => new()
    {
        MatchID = matchID,
        StartTime = startTime,
        EndTime = endTime,
        Duration = duration,
        StatsPullDate = statsPullDate,
        ScoreLayout = scoreLayout,
        UgcGameVariantAssetID = ugcGameVariantAssetID,
        UgcGameVariantVersionID = ugcGameVariantVersionID,
    };

    // Nothing outside the match sets its fields or its navigations: only its own methods below, and EF (which uses the
    // private setters). The key is internal: it's Halo's, given by the factory (and tests may give one).
    public Guid MatchID { get; internal set; }
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public TimeSpan Duration { get; private set; }

    public DateTime? StatsPullDate { get; private set; }

    /// <summary>The custom game mode the match was played on (Waypoint's MatchInfo.UgcGameVariant): its asset and that
    /// asset's version. Null for a match saved before they were kept, until the backfill fetches them
    /// (<see cref="GiveGameVariant"/>). Both or neither.</summary>
    public Guid? UgcGameVariantAssetID { get; private set; }
    public Guid? UgcGameVariantVersionID { get; private set; }

    /// <summary>How its players' Score and PersonalScore were decoded into their goals and the mode's other stats
    /// (MatchParticipant.Goals and the rest); null until it's been decided (the stats then read Score as goals when
    /// it's 20 or less).</summary>
    public CustomScoreLayout? ScoreLayout { get; private set; }

    public SeriesGame? SeriesGame { get; private set; } // A season match's game: set from the match's side only (SeasonMatch.AddGame)
    public MatchedMatch? MatchedMatch { get; private set; } // Late league: set from the match's side only (MatchedMatch.Report)

    // Read-only: only the match's own methods add a team (EF uses the field)
    public IReadOnlyCollection<MatchTeam> MatchTeams => _matchTeams;
    private readonly List<MatchTeam> _matchTeams = [];

    /// <summary>Every player of the match, team by team (not stored: its teams'). Needs the teams' players loaded.</summary>
    public IEnumerable<MatchParticipant> Participants => _matchTeams.SelectMany(t => t.MatchParticipants);

    /// <summary>Saved before its game variant was kept, and not given it since: what the backfill fetches
    /// (<see cref="HasGameVariant"/> for a loaded match).</summary>
    public static readonly Expression<Func<Match, bool>> WithoutGameVariant = m => m.UgcGameVariantVersionID == null;

    /// <summary>Whether it has its game variant (<see cref="WithoutGameVariant"/> for queries).</summary>
    public bool HasGameVariant => UgcGameVariantVersionID is not null;

    /// <summary>Its layout not decided yet: what the decoder looks at (<see cref="IsDecoded"/> for a loaded match).</summary>
    public static readonly Expression<Func<Match, bool>> NotDecoded = m => m.ScoreLayout == null;

    /// <summary>Whether its layout has been decided (<see cref="NotDecoded"/> for queries).</summary>
    public bool IsDecoded => ScoreLayout is not null;

    /// <summary>A team of the match: by <see cref="FromHalo"/>, and by tests seeding a match.</summary>
    internal MatchTeam AddTeam(int teamID, int score, Outcomes outcome)
    {
        if (!Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Outcome out of range");
        if (_matchTeams.Any(t => t.TeamID == teamID))
            throw new ArgumentException($"Match {MatchID} already has team {teamID}", nameof(teamID));
        var team = new MatchTeam(this, teamID, score, outcome);
        _matchTeams.Add(team);
        return team;
    }

    /// <summary>
    /// A player of the match: on the team they ended it on, with their stats and the medals they earned (one count a
    /// medal). <paramref name="xboxUser"/> is their Xbox user when it isn't saved yet (it's saved with the match); null
    /// ties them by <paramref name="xboxUserID"/> alone. A player is in a match once, on one of its teams.
    /// </summary>
    public MatchParticipant AddParticipant(int teamID, long xboxUserID, MatchParticipantStats stats, IEnumerable<MedalCount>? medals = null, XboxUser? xboxUser = null)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var team = _matchTeams.FirstOrDefault(t => t.TeamID == teamID)
            ?? throw new InvalidOperationException($"Match {MatchID} has no team {teamID}");
        if (Participants.Any(p => p.XboxUserID == xboxUserID))
            throw new InvalidOperationException($"Player {xboxUserID} is in match {MatchID} already");
        if (xboxUser is not null && xboxUser.XboxUserID != xboxUserID)
            throw new ArgumentException($"Xbox user {xboxUser.XboxUserID} is not player {xboxUserID}", nameof(xboxUser));
        var medalList = (medals ?? []).ToList();
        if (medalList.GroupBy(m => m.MedalID).FirstOrDefault(g => g.Count() > 1) is { } twice)
            throw new ArgumentException($"Medal {twice.Key} is counted twice", nameof(medals));

        return team.Add(xboxUserID, stats, medalList, xboxUser);
    }

    /// <summary>
    /// Its game variant, fetched again for a match saved before it was kept (the backfill). A match that has one keeps it:
    /// the variant a match was played on doesn't change.
    /// </summary>
    public void GiveGameVariant(Guid assetID, Guid versionID)
    {
        if (HasGameVariant)
            throw new InvalidOperationException($"Match {MatchID} has its game variant already");
        UgcGameVariantAssetID = assetID;
        UgcGameVariantVersionID = versionID;
    }

    /// <summary>
    /// Its layout decided, and what it decodes on each of its players (<paramref name="decoded"/>, one for every player
    /// of the match, no other). Decoded again when the layout of its variant changes. True when anything changed.
    /// </summary>
    public bool Decode(CustomScoreLayout layout, IReadOnlyDictionary<MatchParticipant, DecodedScore> decoded)
    {
        ArgumentNullException.ThrowIfNull(decoded);
        if (!Enum.IsDefined(layout))
            throw new ArgumentOutOfRangeException(nameof(layout), layout, "A layout the model doesn't know");
        var players = Participants.ToList();
        if (decoded.Count != players.Count || players.Any(p => !decoded.ContainsKey(p)))
            throw new ArgumentException($"Match {MatchID} is decoded for each of its players, no other", nameof(decoded));

        var changed = ScoreLayout != layout;
        ScoreLayout = layout;
        foreach (var player in players)
            changed |= player.SetDecoded(decoded[player]);
        return changed;
    }
}

/// <summary>A team of a match as Halo gives it: its ID in the match, its score and its outcome.</summary>
public sealed record MatchTeamResult(int TeamID, int Score, Outcomes Outcome);

/// <summary>A medal a player earned in a match: how many times, and the personal score it gave them.</summary>
public sealed record MedalCount(long MedalID, int Count, int TotalPersonalScoreAwarded = 0);
