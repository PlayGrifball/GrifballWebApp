#nullable disable
using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;
public class Season : AuditableEntity
{
    public Season()
    {
        Teams = new HashSet<Team>();
        SeasonSignups = new HashSet<SeasonSignup>();
        SeasonAvailability = new HashSet<SeasonAvailability>();
    }
    public int SeasonID { get; set; }
    public string SeasonName { get; set; }
    public bool CaptainsLocked { get; set; }

    // The season's dates change only through Schedule, ScheduleArchived and AnnounceAt below, which keep them in an order
    // that can happen (CK_Event_Seasons_DatesInOrder), and through EF (the private setters). Which phase of its life a season
    // is in is worked out from them here only: PhaseAt and the bools for a loaded season, the expressions for queries.

    /// <summary>
    /// The date and time when the season is/was made public (posted to the Discord events channel). Null: not public
    /// yet, as a season is when it is created, until a commissioner sets it (<see cref="AnnounceAt"/>).
    /// </summary>
    public DateTime? PublicAt { get; private set; }
    /// <summary>When signups open: they are open from then until <see cref="SignupsClose"/>, not including it.</summary>
    public DateTime SignupsOpen { get; private set; }
    /// <summary>When signups close, no earlier than they open. It may be after the draft or the season's start: late
    /// signups stay open into the season.</summary>
    public DateTime SignupsClose { get; private set; }
    /// <summary>When the draft starts, no later than the season.</summary>
    public DateTime DraftStart { get; private set; }
    /// <summary>When the season starts: it runs from then until <see cref="SeasonEnd"/>, not including it.</summary>
    public DateTime SeasonStart { get; private set; }
    /// <summary>When the season ends, no earlier than it starts.</summary>
    public DateTime SeasonEnd { get; private set; }
    public int? GameVersionID { get; set; }
    /// <summary>The league the season is in; the app's own seasons are in <see cref="League.DefaultLeagueID"/>.</summary>
    public int LeagueID { get; set; } = League.DefaultLeagueID;
    /// <summary>The season's division (Pro, Legacy, ...), the tier its champions win; null: one open tier. Archive
    /// seasons of different divisions are separate seasons (often named "... (Pro)"), so a stat row has no division of
    /// its own.</summary>
    public string Division { get; set; }
    /// <summary>The archive's key for a season imported from the historical archive: the workbook and sheet of its stats,
    /// "{sourceWorkbook}/{sourceSheet}" (e.g. "2018-sml18-fantasy-rey/Total Stats"), what a re-run of the import finds it
    /// by, even after its name is corrected in the archive. Unique where set; null for the app's own seasons, so set is
    /// what makes a season an imported one (teams, rosters, player stats and championships from its spreadsheets, see
    /// <see cref="HistoricalSourceUrl"/>). Otherwise it is a season like any other: what it lacks (signups, a schedule,
    /// matches) is simply not there. Its dates span the year its name gives, ending no later than the import.</summary>
    public string HistoricalSourceID { get; set; }
    /// <summary>The source workbook's link (Google Sheets) from the archive catalog, kept for provenance even when the
    /// sheet is gone. Null for the app's own seasons.</summary>
    public string HistoricalSourceUrl { get; set; }
    /// <summary>What kind of competition the season is (<see cref="SeasonFormat"/>): which sections its page has, and whether it
    /// crowns a champion. Seasons from before formats existed are league seasons (the column's default).</summary>
    public SeasonFormat Format { get; set; } = SeasonFormat.LeagueSeason;
    public virtual GameVersion GameVersion { get; set; }
    public virtual League League { get; set; }
    public virtual ICollection<Team> Teams { get; set; }
    // Read-only: a match is made in its season through its own factories (EF uses the field)
    public IReadOnlyCollection<SeasonMatch> SeasonMatches => _seasonMatches;
    private readonly HashSet<SeasonMatch> _seasonMatches = [];
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
    public virtual ICollection<SeasonAvailability> SeasonAvailability { get; set; }

    /// <summary>
    /// Sets the season's dates, as a commissioner schedules or reschedules it; refused, the dates left as they were, when
    /// the season was imported from the historical archive (<see cref="SeasonScheduleResult.Imported"/>: its spreadsheets
    /// date it, <see cref="ScheduleArchived"/>) or when their order can't happen (<see cref="SeasonScheduleResult"/>):
    /// signups that close before they open, a draft after the season starts, a season that ends before it starts. Equal
    /// dates are fine: a window that ends as it begins is never open.
    /// </summary>
    public SeasonScheduleResult Schedule(DateTime signupsOpen, DateTime signupsClose, DateTime draftStart, DateTime seasonStart, DateTime seasonEnd)
        => HistoricalSourceID is not null ? SeasonScheduleResult.Imported
        : ScheduleInOrder(signupsOpen, signupsClose, draftStart, seasonStart, seasonEnd);

    /// <summary>
    /// A test's or seeder's dates for any season, an imported one too, as <see cref="Schedule"/> keeps them in order;
    /// the app dates an imported season through <see cref="ScheduleArchived"/> only.
    /// </summary>
    internal SeasonScheduleResult ScheduleSeeded(DateTime signupsOpen, DateTime signupsClose, DateTime draftStart, DateTime seasonStart, DateTime seasonEnd)
        => ScheduleInOrder(signupsOpen, signupsClose, draftStart, seasonStart, seasonEnd);

    /// <summary>The dates set when their order can happen (what <see cref="Schedule"/> and <see cref="ScheduleArchived"/> share).</summary>
    private SeasonScheduleResult ScheduleInOrder(DateTime signupsOpen, DateTime signupsClose, DateTime draftStart, DateTime seasonStart, DateTime seasonEnd)
    {
        if (signupsClose < signupsOpen)
            return SeasonScheduleResult.SignupsCloseBeforeTheyOpen;
        if (seasonStart < draftStart)
            return SeasonScheduleResult.DraftAfterSeasonStart;
        if (seasonEnd < seasonStart)
            return SeasonScheduleResult.EndsBeforeItStarts;
        SignupsOpen = signupsOpen;
        SignupsClose = signupsClose;
        DraftStart = draftStart;
        SeasonStart = seasonStart;
        SeasonEnd = seasonEnd;
        return SeasonScheduleResult.Scheduled;
    }

    /// <summary>
    /// The dates of a season imported from the historical archive (<see cref="HistoricalSourceID"/> set): it ran from
    /// <paramref name="start"/> to <paramref name="end"/>, and had no signups or draft in the app, so those are at its start
    /// (signups that close as they open: never open). Public since it began. The import works its dates out in order, so
    /// any other is its mistake: that throws.
    /// </summary>
    public void ScheduleArchived(DateTime start, DateTime end)
    {
        if (HistoricalSourceID is null)
            throw new InvalidOperationException($"Season {SeasonID} ({SeasonName}) is not an imported season");
        var result = ScheduleInOrder(start, start, start, start, end);
        if (result is not SeasonScheduleResult.Scheduled)
            throw new InvalidOperationException($"Season {SeasonName}: {result} ({start:o} to {end:o})");
        PublicAt = start;
    }

    /// <summary>Makes the season public at <paramref name="publicAt"/> (posted to the events channel then); null: not public.</summary>
    public void AnnounceAt(DateTime? publicAt) => PublicAt = publicAt;

    /// <summary>
    /// Where the season is in its life at <paramref name="now"/>: the latest of its dates that has passed decides. Each
    /// phase runs from its date up to the next one, not including it, so a season has ended at its <see cref="SeasonEnd"/>
    /// exactly. Late signups can be open while the draft is or the season runs, and signups can open after the draft has
    /// started: the phase is then the later one (the draft has started: drafting), and <see cref="SignupsAreOpenAt"/> says
    /// whether one can sign up.
    /// </summary>
    public SeasonPhase PhaseAt(DateTime now) =>
        SeasonEnd <= now ? SeasonPhase.Ended
        : SeasonStart <= now ? SeasonPhase.Running
        : DraftStart <= now || SignupsClose <= now ? SeasonPhase.Drafting
        : SignupsOpen <= now ? SeasonPhase.SignupsOpen
        : SeasonPhase.Upcoming;

    /// <summary>One can sign up at <paramref name="now"/>, late signups included (<see cref="SignupsOpenAt"/> for queries).</summary>
    public bool SignupsAreOpenAt(DateTime now) => SignupsOpen <= now && now < SignupsClose;

    /// <summary>Started and not ended at <paramref name="now"/> (<see cref="RunningAt"/> for queries).</summary>
    public bool IsRunningAt(DateTime now) => PhaseAt(now) is SeasonPhase.Running;

    /// <summary>Over at <paramref name="now"/> (<see cref="EndedAt"/> for queries).</summary>
    public bool HasEndedAt(DateTime now) => PhaseAt(now) is SeasonPhase.Ended;

    /// <summary>Anyone may see it at <paramref name="now"/> (<see cref="PublicAsOf"/> for queries).</summary>
    public bool IsPublicAsOf(DateTime now) => SeasonStart <= now || PublicAt <= now;

    // The same rules for queries, which the database runs: alike to the methods above, boundaries included (SeasonPhaseTests)

    /// <summary>Signups are open at <paramref name="now"/> (<see cref="SignupsAreOpenAt"/>).</summary>
    public static Expression<Func<Season, bool>> SignupsOpenAt(DateTime now) => s => s.SignupsOpen <= now && now < s.SignupsClose;

    /// <summary>Signups haven't closed by <paramref name="now"/>: open, or still to open.</summary>
    public static Expression<Func<Season, bool>> SignupsNotClosedAt(DateTime now) => s => now < s.SignupsClose;

    /// <summary>The season hasn't started by <paramref name="now"/>: still to come, draft and all.</summary>
    public static Expression<Func<Season, bool>> NotStartedAt(DateTime now) => s => now < s.SeasonStart;

    /// <summary>The season has started by <paramref name="now"/>: running or ended.</summary>
    public static Expression<Func<Season, bool>> StartedAt(DateTime now) => s => s.SeasonStart <= now;

    /// <summary>The season is running at <paramref name="now"/> (<see cref="IsRunningAt"/>): started, not ended.</summary>
    public static Expression<Func<Season, bool>> RunningAt(DateTime now) => s => s.SeasonStart <= now && now < s.SeasonEnd;

    /// <summary>The season is over at <paramref name="now"/> (<see cref="HasEndedAt"/>).</summary>
    public static Expression<Func<Season, bool>> EndedAt(DateTime now) => s => s.SeasonEnd <= now;

    /// <summary>The season hasn't ended by <paramref name="now"/>: running, or still to come.</summary>
    public static Expression<Func<Season, bool>> NotEndedAt(DateTime now) => s => now < s.SeasonEnd;

    /// <summary>Announced by <paramref name="now"/>: its <see cref="PublicAt"/> has passed.</summary>
    public static Expression<Func<Season, bool>> AnnouncedAt(DateTime now) => s => s.PublicAt != null && s.PublicAt <= now;

    /// <summary>
    /// The seasons anyone may see at <paramref name="now"/> (<see cref="IsPublicAsOf"/>): started, or announced, whose pages
    /// the quick search links to. (The season list shows only started seasons, and the Discord events post only announced
    /// ones not yet over: narrower questions.)
    /// </summary>
    public static Expression<Func<Season, bool>> PublicAsOf(DateTime now) => s => s.SeasonStart <= now || (s.PublicAt != null && s.PublicAt <= now);
}

/// <summary>Where a season is in its life at a time (<see cref="Season.PhaseAt"/>), worked out from its dates.</summary>
public enum SeasonPhase
{
    /// <summary>Signups haven't opened, nor has the draft started. It may be public already (<see cref="Season.PublicAt"/>): announced, signups to come.</summary>
    Upcoming,
    /// <summary>Signups are open, and the draft hasn't started.</summary>
    SignupsOpen,
    /// <summary>Signups have closed or the draft has started, and the season hasn't.</summary>
    Drafting,
    /// <summary>Started, not ended.</summary>
    Running,
    /// <summary>Over.</summary>
    Ended,
}

/// <summary>What <see cref="Season.Schedule"/> did: scheduled the season, or which order of dates it refused.</summary>
public enum SeasonScheduleResult
{
    Scheduled,
    SignupsCloseBeforeTheyOpen,
    DraftAfterSeasonStart,
    EndsBeforeItStarts,
    /// <summary>The season was imported from the historical archive: its spreadsheets date it, not the editor.</summary>
    Imported,
}
