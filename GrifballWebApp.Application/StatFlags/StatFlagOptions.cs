namespace GrifballWebApp.Application.StatFlags;

/// <summary>
/// The tunable numbers <see cref="StatFlagService"/>'s rules use (the StatFlags section): conservative by default, so a
/// flag means staff should look, not that the game is necessarily wrong. Bound and validated at startup
/// (AddValidatedStatFlagOptions), with the Helm chart's defaults matching these (fourth review: these two were fixed
/// constants, StatFlagThresholds, until the owner asked to tune them per environment without a code change).
/// </summary>
public class StatFlagOptions
{
    /// <summary>A side fielding fewer players than this is flagged (StatFlagRule.TeamSmallerThanFormat): Grifball's own
    /// format, 4v4.</summary>
    public int MinTeamSize { get; set; } = 4;

    /// <summary>A side fielding more players than this is flagged too (the same rule): a sub or two beyond the format is
    /// normal roster depth, but a side that's grown well past it is worth a look.</summary>
    public int MaxTeamSize { get; set; } = 5;

    /// <summary>Accuracy over this percentage is flagged (StatFlagRule.ImpossibleAccuracy): over 100%, allowing a little
    /// for Halo's own float rounding.</summary>
    public float MaxAccuracy { get; set; } = 100.5f;

    /// <summary>A side's kills may exceed the other side's deaths by this percentage (plus
    /// <see cref="KillsOverDeathsSlack"/>) before StatFlagRule.KillsDoNotMatchDeaths flags it: every kill is someone's
    /// death, but a death can also be a suicide or betrayal, and hand-entered numbers are rarely exact.</summary>
    public float KillsOverDeathsPercent { get; set; } = 50f;

    /// <summary>A flat number of kills added to the allowance above, so a low-scoring game isn't flagged for one miscount.</summary>
    public int KillsOverDeathsSlack { get; set; } = 10;

    /// <summary>A hand-entered game played more than this many days from its season match's scheduled time is flagged
    /// (StatFlagRule.DateOutsideMatchWeek): a reschedule moves the scheduled time, so a game a few days off is normal.</summary>
    public int MaxDaysFromScheduled { get; set; } = 3;

    /// <summary>A Halo player's kills or deaths per minute of the match's length above this are flagged
    /// (StatFlagRule.StatsExceedGameLength).</summary>
    public float MaxKillsOrDeathsPerMinute { get; set; } = 6f;

    /// <summary>The same, for goals.</summary>
    public float MaxGoalsPerMinute { get; set; } = 2f;
}
