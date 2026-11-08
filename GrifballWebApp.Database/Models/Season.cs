#nullable disable
using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A season of a league: the root of its timeslots (<see cref="SeasonAvailability"/>) and its titles
/// (<see cref="PlayerChampionships"/>). It begins one of two ways: an organizer's (<see cref="Organized"/>), which the
/// event organizer edits (<see cref="Edit"/>, <see cref="Schedule"/>), or the historical archive's (<see cref="Imported"/>,
/// <see cref="HistoricalSourceID"/> set), which only the import changes (<see cref="UpdateArchived"/>,
/// <see cref="ScheduleArchived"/>): an imported season is refused for an organizer's edits. Nothing outside it sets its
/// fields: only its own methods, and EF (which uses the private setters).
/// </summary>
[AggregateRoot]
public class Season : AuditableEntity
{
    /// <summary>The tier of a season's title when the season has no division: the app's seasons are one open tier.</summary>
    public const string DefaultChampionTier = "Open";

    /// <summary>For EF. A season begins through <see cref="Organized"/> or <see cref="Imported"/>.</summary>
    private Season() { }

    /// <summary>
    /// A season an organizer makes in the event organizer: not public and not scheduled yet (<see cref="Schedule"/> and
    /// <see cref="AnnounceAt"/> set its dates).
    /// </summary>
    public static Season Organized(string seasonName, int leagueID, int? gameVersionID, string division = null, SeasonFormat format = SeasonFormat.LeagueSeason) => new()
    {
        SeasonName = seasonName,
        LeagueID = leagueID,
        GameVersionID = gameVersionID,
        Division = NormalizedDivision(division),
        Format = format,
    };

    /// <summary>
    /// A season of the historical archive, found again by <paramref name="historicalSourceID"/> (its workbook and sheet):
    /// the import dates it (<see cref="ScheduleArchived"/>) and keeps it as the archive says (<see cref="UpdateArchived"/>).
    /// </summary>
    public static Season Imported(string historicalSourceID, string seasonName, int leagueID, int? gameVersionID, string division, string historicalSourceUrl, SeasonFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(historicalSourceID);
        return new()
        {
            HistoricalSourceID = historicalSourceID,
            SeasonName = seasonName,
            LeagueID = leagueID,
            GameVersionID = gameVersionID,
            Division = division,
            HistoricalSourceUrl = historicalSourceUrl,
            Format = format,
        };
    }

    /// <summary>
    /// A season of any kind, for tests and the seeder, which seed seasons of their own (with a key, a league entity not
    /// saved yet, or an imported season's source) and then change them through its methods.
    /// </summary>
    internal static Season Seed(string seasonName = null, int seasonID = 0, int leagueID = League.DefaultLeagueID, int? gameVersionID = null,
        string division = null, SeasonFormat format = SeasonFormat.LeagueSeason, string historicalSourceID = null, string historicalSourceUrl = null,
        bool captainsLocked = false, League league = null) => new()
    {
        SeasonID = seasonID,
        SeasonName = seasonName,
        LeagueID = league is { LeagueID: not 0 } ? league.LeagueID : leagueID,
        League = league,
        GameVersionID = gameVersionID,
        Division = division,
        Format = format,
        HistoricalSourceID = historicalSourceID,
        HistoricalSourceUrl = historicalSourceUrl,
        CaptainsLocked = captainsLocked,
    };

    // The key is internal: the database makes it, and tests may give one
    public int SeasonID { get; internal set; }
    public string SeasonName { get; private set; }
    /// <summary>Captains can't be changed while locked (the draft), <see cref="LockCaptains"/>.</summary>
    public bool CaptainsLocked { get; private set; }

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
    public int? GameVersionID { get; private set; }
    /// <summary>The league the season is in; the app's own seasons are in <see cref="League.DefaultLeagueID"/>.</summary>
    public int LeagueID { get; private set; } = League.DefaultLeagueID;
    /// <summary>The season's division (Pro, Legacy, ...), the tier its champions win; null: one open tier. Archive
    /// seasons of different divisions are separate seasons (often named "... (Pro)"), so a stat row has no division of
    /// its own.</summary>
    public string Division { get; private set; }
    /// <summary>The archive's key for a season imported from the historical archive: the workbook and sheet of its stats,
    /// "{sourceWorkbook}/{sourceSheet}" (e.g. "2018-sml18-fantasy-rey/Total Stats"), what a re-run of the import finds it
    /// by, even after its name is corrected in the archive. Unique where set; null for the app's own seasons, so set is
    /// what makes a season an imported one (teams, rosters, player stats and championships from its spreadsheets, see
    /// <see cref="HistoricalSourceUrl"/>). Otherwise it is a season like any other: what it lacks (signups, a schedule,
    /// matches) is simply not there. Its dates span the year its name gives, ending no later than the import.</summary>
    public string HistoricalSourceID { get; private set; }
    /// <summary>The source workbook's link (Google Sheets) from the archive catalog, kept for provenance even when the
    /// sheet is gone. Null for the app's own seasons (CK_Event_Seasons_SourceUrlOnlyImported).</summary>
    public string HistoricalSourceUrl { get; private set; }
    /// <summary>What kind of competition the season is (<see cref="SeasonFormat"/>): which sections its page has, and whether it
    /// crowns a champion. Seasons from before formats existed are league seasons (the column's default).</summary>
    public SeasonFormat Format { get; private set; } = SeasonFormat.LeagueSeason;
    public GameVersion GameVersion { get; private set; }
    public League League { get; private set; }
    // Read-only collections: a team, a signup and a match are added to the season by their own code (setting the season
    // on them) or by its methods below; its timeslots and titles by its methods alone (EF uses the fields)
    public IReadOnlyCollection<Team> Teams => _teams;
    private readonly HashSet<Team> _teams = [];
    public IReadOnlyCollection<SeasonMatch> SeasonMatches => _seasonMatches;
    private readonly HashSet<SeasonMatch> _seasonMatches = [];
    public IReadOnlyCollection<SeasonSignup> SeasonSignups => _seasonSignups;
    private readonly HashSet<SeasonSignup> _seasonSignups = [];
    /// <summary>The weekly timeslots the season's matches can be played in (<see cref="SetAvailability"/>).</summary>
    public IReadOnlyCollection<SeasonAvailability> SeasonAvailability => _seasonAvailability;
    private readonly HashSet<SeasonAvailability> _seasonAvailability = [];
    /// <summary>The season's titles: a row per player on a tier's championship roster (<see cref="RecordChampions"/>,
    /// <see cref="RecordArchivedChampions"/>). Read them through SeasonQueries.Championships unless the season is loaded with them.</summary>
    public IReadOnlyCollection<PlayerChampionship> PlayerChampionships => _playerChampionships;
    private readonly HashSet<PlayerChampionship> _playerChampionships = [];

    /// <summary>
    /// The seasons imported from the historical archive (<see cref="HistoricalSourceID"/> set), for queries: the one rule,
    /// which <see cref="IsImported"/> runs for a loaded season. (Named apart from the <see cref="Imported"/> factory.)
    /// </summary>
    public static readonly Expression<Func<Season, bool>> ImportedFromArchive = s => s.HistoricalSourceID != null;

    private static readonly Func<Season, bool> IsImportedRule = ImportedFromArchive.Compile();

    /// <summary>Imported from the historical archive (<see cref="ImportedFromArchive"/>): only the import changes it.</summary>
    public bool IsImported => IsImportedRule(this);

    /// <summary>The tier the season's champions win (<see cref="RecordChampions"/>): its division, <see cref="DefaultChampionTier"/> without one.</summary>
    public string ChampionTier => string.IsNullOrWhiteSpace(Division) ? DefaultChampionTier : Division;

    /// <summary>
    /// The event organizer's edit of the season's name, league, game, division and format (its dates are
    /// <see cref="Schedule"/>'s): refused, nothing changed, for a season imported from the historical archive
    /// (<see cref="SeasonEditResult.Imported"/>: its spreadsheets say what it is). A blank division is none.
    /// </summary>
    public SeasonEditResult Edit(string seasonName, int leagueID, int? gameVersionID, string division, SeasonFormat format)
    {
        if (IsImported)
            return SeasonEditResult.Imported;
        SeasonName = seasonName;
        LeagueID = leagueID;
        GameVersionID = gameVersionID;
        Division = NormalizedDivision(division);
        Format = format;
        return SeasonEditResult.Edited;
    }

    private static string NormalizedDivision(string division) => string.IsNullOrWhiteSpace(division) ? null : division.Trim();

    /// <summary>The import's update of a season it made: as the archive gives it now. Only an imported season (throws otherwise).</summary>
    public void UpdateArchived(string seasonName, int leagueID, int? gameVersionID, string division, string historicalSourceUrl, SeasonFormat format)
    {
        RequireImported();
        SeasonName = seasonName;
        LeagueID = leagueID;
        GameVersionID = gameVersionID;
        Division = division;
        HistoricalSourceUrl = historicalSourceUrl;
        Format = format;
    }

    /// <summary>
    /// The import adopts a season it made under another key (a titles-only season of the champions record, or another tab
    /// of the same workbook) as the season of <paramref name="historicalSourceID"/>. Only an imported season (throws otherwise).
    /// </summary>
    public void AdoptArchiveSource(string historicalSourceID)
    {
        RequireImported();
        ArgumentException.ThrowIfNullOrWhiteSpace(historicalSourceID);
        HistoricalSourceID = historicalSourceID;
    }

    private void RequireImported()
    {
        if (!IsImported)
            throw new InvalidOperationException($"Season {SeasonID} ({SeasonName}) is not an imported season");
    }

    private void RequireOrganized()
    {
        if (IsImported)
            throw new InvalidOperationException($"Season {SeasonID} ({SeasonName}) was imported from the historical archive: only the import changes it");
    }

    /// <summary>Locks the season's captains (the draft is on) or unlocks them.</summary>
    public void LockCaptains(bool locked) => CaptainsLocked = locked;

    /// <summary>
    /// The organizer's edit of the season's timeslots: they become <paramref name="options"/>. A timeslot not among them
    /// goes, one not yet the season's is added (an option may be new, not saved yet), and one it has stays as it is.
    /// Refused, nothing changed, for a season imported from the historical archive (<see cref="SeasonEditResult.Imported"/>).
    /// </summary>
    public SeasonEditResult SetAvailability(IReadOnlyCollection<AvailabilityOption> options)
    {
        if (IsImported)
            return SeasonEditResult.Imported;
        _seasonAvailability.RemoveWhere(a => !options.Any(a.IsFor));
        foreach (var option in options.Where(o => !_seasonAvailability.Any(a => a.IsFor(o))).ToList())
            _seasonAvailability.Add(Models.SeasonAvailability.Of(this, option));
        return SeasonEditResult.Edited;
    }

    /// <summary>The organizer copies another season's timeslots to this one (the timeslots it had and the other hasn't go).
    /// Refused for an imported season (throws).</summary>
    public void CopyAvailabilityFrom(Season other)
    {
        RequireOrganized();
        var options = other._seasonAvailability.Select(a => a.AvailabilityOptionID).ToHashSet();
        _seasonAvailability.RemoveWhere(a => !options.Contains(a.AvailabilityOptionID));
        foreach (var option in options.Where(o => !_seasonAvailability.Any(a => a.AvailabilityOptionID == o)).ToList())
            _seasonAvailability.Add(Models.SeasonAvailability.Of(this, option));
    }

    /// <summary>The organizer copies another season's signups to this one, in place of its own. Refused for an imported season (throws).</summary>
    public void CopySignupsFrom(Season other)
    {
        RequireOrganized();
        _seasonSignups.Clear();
        foreach (var signup in other._seasonSignups.Select(s => s.Copy()).ToList())
            _seasonSignups.Add(signup);
    }

    /// <summary>
    /// The organizer copies another season's teams and rosters to this one, in place of its own. A captain can't be copied
    /// until the copies are saved: each copied team's captain (their user) is returned, for the caller to set then
    /// (<see cref="Team.SetCaptain"/>). Refused for an imported season (throws).
    /// </summary>
    public IReadOnlyList<(Team Team, int CaptainUserID)> CopyTeamsFrom(Season other)
    {
        RequireOrganized();
        _teams.Clear();
        var captains = new List<(Team, int)>();
        foreach (var team in other._teams.ToList())
        {
            var copy = team.Copy(out var captainUserID);
            if (captainUserID is int captain)
                captains.Add((copy, captain));
            _teams.Add(copy);
        }
        return captains;
    }

    // Its titles: the season's titles must be loaded (SeasonQueries.WithChampionships) for these to find the ones it has

    /// <summary>
    /// Records the champions of a season of the app's, decided by its playoff bracket: <paramref name="team"/> (one of the
    /// season's) wins its <see cref="ChampionTier"/>, a title for each of <paramref name="roster"/> (its captain flagged),
    /// and the tier's titles of anyone else (a champion a corrected result replaced) go. A null team takes the tier's titles
    /// away. Idempotent. An imported season keeps its record's titles: null, nothing changed (its bracket is rebuilt from
    /// game order and decides nothing).
    /// </summary>
    public ChampionshipChanges RecordChampions(Team team, IReadOnlyCollection<ChampionMember> roster)
        => IsImported ? null : SetChampions(ChampionTier, team, roster, GameVersionID);

    /// <summary>
    /// Records a title of the archive's champions record: <paramref name="team"/> (one of the season's) won
    /// <paramref name="tier"/> on <paramref name="gameVersionID"/>, with <paramref name="roster"/>, as
    /// <see cref="RecordChampions"/> does. Only an imported season (throws otherwise).
    /// </summary>
    public ChampionshipChanges RecordArchivedChampions(string tier, Team team, IReadOnlyCollection<ChampionMember> roster, int? gameVersionID)
    {
        RequireImported();
        ArgumentNullException.ThrowIfNull(team);
        return SetChampions(tier, team, roster, gameVersionID);
    }

    /// <summary>The titles of any tier, for tests, which seed titles of seasons of either kind.</summary>
    internal ChampionshipChanges SeedChampions(string tier, Team team, IReadOnlyCollection<ChampionMember> roster, int? gameVersionID)
        => SetChampions(tier, team, roster, gameVersionID);

    private ChampionshipChanges SetChampions(string tier, Team team, IReadOnlyCollection<ChampionMember> roster, int? gameVersionID)
    {
        if (team is not null && team.SeasonID != SeasonID)
            throw new ArgumentException($"Team {team.TeamID} is not a team of season {SeasonID}", nameof(team));
        tier = tier.Trim();
        var existing = _playerChampionships.Where(c => string.Equals(c.Tier, tier, StringComparison.OrdinalIgnoreCase)).ToList();
        // The tier as its titles already spell it (tiers compare case-insensitively)
        tier = existing.FirstOrDefault()?.Tier ?? tier;

        int inserted = 0, updated = 0;
        var kept = new HashSet<int>();
        if (team is not null)
        {
            foreach (var member in roster)
            {
                if (!kept.Add(member.UserID))
                    continue;
                var title = existing.FirstOrDefault(c => c.UserID == member.UserID);
                if (title is null)
                {
                    title = PlayerChampionship.Won(this, tier, member.UserID);
                    _playerChampionships.Add(title);
                    inserted++;
                }
                else
                {
                    updated++;
                }
                title.HeldBy(team.TeamID, gameVersionID, member.IsCaptain, member.Gamertag);
            }
        }

        var stale = existing.Where(c => !kept.Contains(c.UserID)).ToList();
        foreach (var title in stale)
            _playerChampionships.Remove(title);
        return new ChampionshipChanges(inserted, updated, stale.Count);
    }

    /// <summary>The titles of tiers the champions record no longer gives this imported season go; returns how many.
    /// Only an imported season (throws otherwise).</summary>
    public int RemoveArchivedTitlesExcept(IReadOnlySet<string> tiers)
    {
        RequireImported();
        return _playerChampionships.RemoveWhere(c => !tiers.Contains(c.Tier));
    }

    /// <summary>The titles of the imported season's teams the import removes go. Only an imported season (throws otherwise).</summary>
    public int RemoveArchivedTitlesOf(IReadOnlyCollection<int> teamIDs)
    {
        RequireImported();
        return _playerChampionships.RemoveWhere(c => teamIDs.Contains(c.TeamID));
    }

    /// <summary>
    /// A user merge: the season's titles of <paramref name="fromUserID"/> become <paramref name="toUserID"/>'s, except a
    /// title the target already holds for the same tier (one person listed twice on a roster under two gamertags): the
    /// target keeps its own, the captaincy too if the source had it, and the source's goes. Returns how many moved.
    /// </summary>
    public int MergeTitles(int fromUserID, int toUserID)
    {
        var moved = 0;
        foreach (var title in _playerChampionships.Where(c => c.UserID == fromUserID).ToList())
        {
            var held = _playerChampionships.FirstOrDefault(h => h.UserID == toUserID && string.Equals(h.Tier, title.Tier, StringComparison.OrdinalIgnoreCase));
            if (held is not null)
            {
                held.KeepCaptaincyOf(title);
                _playerChampionships.Remove(title);
            }
            else
            {
                title.ReassignUser(toUserID);
                moved++;
            }
        }
        return moved;
    }

    /// <summary>
    /// Sets the season's dates, as a commissioner schedules or reschedules it; refused, the dates left as they were, when
    /// the season was imported from the historical archive (<see cref="SeasonScheduleResult.Imported"/>: its spreadsheets
    /// date it, <see cref="ScheduleArchived"/>) or when their order can't happen (<see cref="SeasonScheduleResult"/>):
    /// signups that close before they open, a draft after the season starts, a season that ends before it starts. Equal
    /// dates are fine: a window that ends as it begins is never open.
    /// </summary>
    public SeasonScheduleResult Schedule(DateTime signupsOpen, DateTime signupsClose, DateTime draftStart, DateTime seasonStart, DateTime seasonEnd)
        => IsImported ? SeasonScheduleResult.Imported
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
        RequireImported();
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

/// <summary>What <see cref="Season.Edit"/> or <see cref="Season.SetAvailability"/> did: edited the season, or refused an imported one.</summary>
public enum SeasonEditResult
{
    Edited,
    /// <summary>The season was imported from the historical archive: its spreadsheets say what it is, not the editor.</summary>
    Imported,
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
