using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace GrifballWebApp.Server.Brackets;

public class ViewerDataDto
{
    public List<Stage> Stages { get; set; } // 1 Stage for season
    public List<Match> Matches { get; set; } // SeasonMatches
    public List<MatchGame> MatchGames { get; set; } // Links / Infinite Matches
    public List<Participant> Participants { get; set; }
}

//https://code-maze.com/csharp-serialize-enum-to-string/

public class Stage
{
    public int Id { get; set; } // Season ID
    public int Tournament_id { get; set; } // SeasonID
    public string Name { get; set; } // Season Name Playoffs
    public required StageType Type { get; set; }
    public StageSettings Settings { get; set; }
    // The number of the stage in its tournament.
    public int Number { get; set; } // 1
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StageType
{
    round_robin,
    single_elimination,
    double_elimination
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SeedOrdering
{
    natural,
    reverse,
    half_shift,
    reverse_half_shift,
    pair_flip,
    inner_outer,
    //groups.effort_balanced,
    //groups.seed_optimized,
    //groups.bracket_optimized
}

public class StageSettings
{
    /// <summary>
    /// The number of participants. Also includes Byes
    /// </summary>
    public int Size { get; set; }
    public SeedOrdering[] SeedOrdering { get; set; }
    public bool BalanceByes { get; set; }
    public int MatchesChildCount { get; set; }
    public int GroupCount { get; set; }
    public RoundRobinMode RoundRobinMode { get; set; }
    public int[][] ManualOrdering { get; set; }
    public bool ConsolationFinal { get; set; }
    public bool SkipFirstRound { get; set; }
    public GrandFinalType GrandFinal { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GrandFinalType
{
    none,
    simple,
    @double
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoundRobinMode
{
    simple,
    @double
}


public class MatchResults
{
    public Status Status { get; set; }
    public ParticpantResult? Opponent1 { get; set; }
    public ParticpantResult? Opponent2 { get; set; }
}

public class Match : MatchResults
{
    /// <summary>
    /// ID of the match: SeasonMatchID
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// ID of the parent stage: 1
    /// </summary>
    public int Stage_id { get; set; }
    /// <summary>
    /// ID of the parent group: ???
    /// </summary>
    public int Group_id { get; set; }
    /// <summary>
    /// ID of the parent round.
    /// </summary>
    public int Round_id { get; set; }
    /// <summary>
    /// The number of the match in its round.
    /// </summary>
    public int Number { get; set; } // 1 - 3 ??
    /// <summary>
    /// The count of match games this match has. Can be `0` if it's a simple match, or a positive number for "Best Of" matches.
    /// </summary>
    public int Child_count { get; set; }
}

public class MatchGame
{
    /// <summary>
    /// ID of the match game: MatchID
    /// </summary>
    public string Id { get; set; }
    /// <summary>
    /// ID of the parent stage: 1
    /// </summary>
    public int Stage_id { get; set; }
    /// <summary>
    /// ID of the parent match: SeasonMatchID
    /// </summary>
    public int ParentID { get; set; }
    /// <summary>
    /// The number of the match game in its parent match: 1, 2, 3...
    /// </summary>
    public int Number { get; set; }
}

// Not string?
public enum Status
{
    // The two matches leading to this one are not completed yet.
    Locked = 0,
    // One participant is ready and waiting for the other one.
    Waiting = 1,
    // Both participants are ready to start.
    Ready = 2,
    // The match is running.
    Running = 3,
    // The match is completed.
    Completed = 4,
    // At least one participant completed his following match.
    Archived = 5,
}

/// <summary>
/// Only contains information about match status and results.
/// </summary>
public class ParticpantResult
{
    // If `null`, the participant is to be determined.
    public int? Id { get; set; }
    // Indicates where the participant comes from.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Position { get; set; }
    // If this participant forfeits, the other automatically wins.
    public bool Forfeit { get; set; }
    /// <summary>
    /// The current score of the participant.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Score { get; set; }
    /// <summary>
    /// Tells what is the result of a duel for this participant.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Result? Result { get; set; } // win, draw, loss
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Result
{
    win,
    draw,
    loss
}

public class Participant
{
    /// <summary>
    /// ID of the participant: TeamID
    /// </summary>
    public int Id { get; set; }
    /// <summary>
    /// ID of the tournament this participant belongs to: SeasonID
    /// </summary>
    public int Tournament_id { get; set; }
    /// <summary>
    /// Name of the participant: Team Name
    /// </summary>
    public string Name { get; set; }
}
