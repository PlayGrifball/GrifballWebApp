using System.Linq.Expressions;

namespace GrifballWebApp.Domain.Models;

/// <summary>
/// A player's signup for a season: what they chose (a team name, whether they'll captain, whether they need help drafting)
/// and the season's timeslots they can play in (<see cref="SignupAvailability"/>, its parts: made and removed only through
/// its methods). It begins when the player signs up (<see cref="SignUp"/>) and changes when they change it
/// (<see cref="Change"/>), both only while the season's signups are open (<see cref="Season.SignupsAreOpenAt"/>). The
/// organizer copies a season's signups to another (<see cref="Copy"/>), and a merge of accounts moves it to the account
/// kept (<see cref="MoveToUser"/>). The draft reads it: who is signed up (<see cref="Of"/>) and a captain's team name
/// (<see cref="TeamNameAtDraft"/>).
/// <list type="bullet">
/// <item>a team name is stored trimmed, or not at all (CK_Event_SeasonSignups_TeamNameTrimmed);</item>
/// <item>only a captain asks for help drafting (CK_Event_SeasonSignups_AssistanceOnlyForCaptains).</item>
/// </list>
/// Nothing outside it sets its fields: only its own methods, and EF (which uses the private setters).
/// </summary>
[AggregateRoot]
public class SeasonSignup : AuditableEntity
{
    /// <summary>For EF. A signup begins through <see cref="SignUp"/>.</summary>
    private SeasonSignup() { }

    /// <summary>
    /// The player signs up for <paramref name="season"/> at <paramref name="now"/>, choosing <paramref name="choices"/>.
    /// Refused for a captain's team name too long to be a team's (<see cref="SignupResult.TeamNameTooLong"/>), while its
    /// signups aren't open, and for a timeslot the season doesn't offer (the season is loaded with its
    /// <see cref="Season.SeasonAvailability"/> when any is chosen). The signup is the outcome's when signed up.
    /// </summary>
    public static SignupOutcome SignUp(Season season, int userID, DateTime now, SignupChoices choices)
    {
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(choices);
        if (CaptainsTeamNameIsTooLong(choices))
            return new(SignupResult.TeamNameTooLong);
        if (!season.SignupsAreOpenAt(now))
            return new(SignupResult.SignupsClosed);
        var refused = NotOffered(season, choices.Timeslots);
        if (refused.Count > 0)
            return new(SignupResult.TimeslotsNotOffered, RefusedTimeslots: refused);

        var signup = new SeasonSignup { SeasonID = season.SeasonID, UserID = userID };
        signup.Choose(choices, now);
        return new(SignupResult.SignedUp, signup);
    }

    /// <summary>
    /// A signup of any kind, past the rules: for tests, which seed signups as a player left them (the season's window
    /// and timeslots aside), and old rows for the migration tests.
    /// </summary>
    internal static SeasonSignup Seed(int seasonID, int userID, DateTime? timestamp = null, string? teamName = null, bool willCaptain = false,
        bool requiresAssistanceDrafting = false, IEnumerable<int>? timeslots = null)
    {
        var signup = new SeasonSignup
        {
            SeasonID = seasonID,
            UserID = userID,
            Timestamp = timestamp ?? DateTime.UtcNow,
            TeamName = teamName,
            WillCaptain = willCaptain,
            RequiresAssistanceDrafting = requiresAssistanceDrafting,
        };
        foreach (var option in (timeslots ?? []).Distinct())
            signup._signupAvailability.Add(new SignupAvailability(option));
        return signup;
    }

    /// <summary>The same for a season not saved yet: the season gives its key when they're saved together.</summary>
    internal static SeasonSignup Seed(Season season, DateTime? timestamp = null)
    {
        var signup = Seed(season.SeasonID, userID: 0, timestamp);
        signup.Season = season;
        return signup;
    }

    // The key is internal: the database makes it, and tests may give one
    public int SeasonSignupID { get; internal set; }
    public int UserID { get; private set; }
    public int SeasonID { get; private set; }
    /// <summary>When the player signed up, or last changed it.</summary>
    public DateTime Timestamp { get; private set; }
    /// <summary>The name the player wants for their team, if they captain one: trimmed, never blank (null: none).</summary>
    public string? TeamName { get; private set; }
    public bool WillCaptain { get; private set; }
    /// <summary>A captain who wants help drafting; never a player who won't captain.</summary>
    public bool RequiresAssistanceDrafting { get; private set; }
    public User User { get; private set; } = null!;
    public Season Season { get; private set; } = null!;
    /// <summary>The season's timeslots the player can play in. Read-only: only the signup's methods change them (EF uses the field).</summary>
    public IReadOnlyCollection<SignupAvailability> SignupAvailability => _signupAvailability;
    private readonly HashSet<SignupAvailability> _signupAvailability = [];

    /// <summary>The signup of <paramref name="userID"/> for <paramref name="seasonID"/>, for queries: whether they're signed up.</summary>
    public static Expression<Func<SeasonSignup, bool>> Of(int seasonID, int userID) => s => s.SeasonID == seasonID && s.UserID == userID;

    /// <summary>
    /// The player changes their signup at <paramref name="now"/>: their choices become <paramref name="choices"/>, and their
    /// timeslots its (a timeslot no longer chosen goes, EF deletes it; one they keep stays even if the season no longer
    /// offers it). Refused for a captain's team name too long to be a team's (<see cref="SignupResult.TeamNameTooLong"/>),
    /// while the season's signups aren't open, and for a new timeslot the season doesn't offer.
    /// </summary>
    public SignupOutcome Change(Season season, DateTime now, SignupChoices choices)
    {
        ArgumentNullException.ThrowIfNull(season);
        ArgumentNullException.ThrowIfNull(choices);
        if (season.SeasonID != SeasonID)
            throw new ArgumentException($"Signup {SeasonSignupID} is for season {SeasonID}, not {season.SeasonID}", nameof(season));
        if (CaptainsTeamNameIsTooLong(choices))
            return new(SignupResult.TeamNameTooLong, this);
        if (!season.SignupsAreOpenAt(now))
            return new(SignupResult.SignupsClosed, this);
        var refused = NotOffered(season, choices.Timeslots.Where(o => _signupAvailability.All(a => a.AvailabilityOptionID != o)));
        if (refused.Count > 0)
            return new(SignupResult.TimeslotsNotOffered, this, refused);

        Choose(choices, now);
        return new(SignupResult.Changed, this);
    }

    /// <summary>
    /// A copy for another season (the organizer's <see cref="Season.CopySignupsFrom"/>): the same player, choices and
    /// timeslots. The season it is added to gives its key.
    /// </summary>
    public SeasonSignup Copy()
    {
        var copy = new SeasonSignup
        {
            UserID = UserID,
            User = User,
            Timestamp = Timestamp,
            TeamName = TeamName,
            WillCaptain = WillCaptain,
            RequiresAssistanceDrafting = RequiresAssistanceDrafting,
        };
        foreach (var availability in _signupAvailability)
            copy._signupAvailability.Add(availability.Copy());
        return copy;
    }

    /// <summary>A merge of accounts moves the signup to the account kept (the caller keeps one signup per season).</summary>
    public void MoveToUser(int userID) => UserID = userID;

    /// <summary>The name of the team the player gets when made a captain at the draft: theirs, else "<paramref name="captainName"/>'s Team".</summary>
    public string TeamNameAtDraft(string captainName) => TeamName ?? $"{captainName}'s Team";

    private void Choose(SignupChoices choices, DateTime now)
    {
        Timestamp = now;
        // Stored trimmed: a franchise is its teams of one name across seasons (TeamHistoryService)
        TeamName = string.IsNullOrWhiteSpace(choices.TeamName) ? null : choices.TeamName.Trim();
        WillCaptain = choices.WillCaptain;
        RequiresAssistanceDrafting = choices.WillCaptain && choices.RequiresAssistanceDrafting;

        var chosen = choices.Timeslots.ToHashSet();
        _signupAvailability.RemoveWhere(a => !chosen.Contains(a.AvailabilityOptionID));
        foreach (var option in chosen.Where(o => _signupAvailability.All(a => a.AvailabilityOptionID != o)))
            _signupAvailability.Add(new SignupAvailability(option));
    }

    /// <summary>
    /// A captain's team name becomes the team's at the draft (<see cref="TeamNameAtDraft"/>), which can't be longer than
    /// <see cref="Team.TeamNameMaxLength"/>: refused at signup rather than cut there. Counted as it is stored, trimmed.
    /// Only a captain's: no one else's name becomes a team's, so an old long one doesn't block them.
    /// </summary>
    private static bool CaptainsTeamNameIsTooLong(SignupChoices choices)
        => choices.WillCaptain && choices.TeamName?.Trim().Length > Team.TeamNameMaxLength;

    /// <summary>Those of <paramref name="options"/> the season doesn't offer, in order.</summary>
    private static List<int> NotOffered(Season season, IEnumerable<int> options)
    {
        var offered = season.SeasonAvailability.Select(a => a.AvailabilityOptionID).ToHashSet();
        return options.Where(o => !offered.Contains(o)).Distinct().ToList();
    }
}

/// <summary>What a player chooses when they sign up or change their signup: <paramref name="Timeslots"/> are the
/// availability options they can play in.</summary>
public sealed record SignupChoices(string? TeamName, bool WillCaptain, bool RequiresAssistanceDrafting, IReadOnlyCollection<int> Timeslots);

/// <summary>A signup's answer (<see cref="SeasonSignup.SignUp"/>, <see cref="SeasonSignup.Change"/>): the signup when
/// there is one, and the timeslots refused.</summary>
public sealed record SignupOutcome(SignupResult Result, SeasonSignup? Signup = null, IReadOnlyList<int>? RefusedTimeslots = null);

public enum SignupResult
{
    SignedUp,
    Changed,
    /// <summary>The season's signups aren't open: not yet, or no longer.</summary>
    SignupsClosed,
    /// <summary>A timeslot chosen isn't one of the season's.</summary>
    TimeslotsNotOffered,
    /// <summary>A captain's team name is longer, trimmed, than a team's can be (<see cref="Team.TeamNameMaxLength"/>).</summary>
    TeamNameTooLong,
}
