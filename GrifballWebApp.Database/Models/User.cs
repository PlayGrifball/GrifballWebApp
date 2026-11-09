using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Database.Models;

/// <summary>
/// A person's account, or a dummy user standing in for a player who never signed up (the import's placeholders). Identity's
/// own fields (user name, email, password, lockout) are Identity's and stay as it defines them; the app's own fields change
/// only through the methods below. The root of the game versions the user played (<see cref="UserExperience"/>). Its moves:
/// <list type="bullet">
/// <item>made by <see cref="Register"/> (a person) or <see cref="ImportPlaceholder"/> (a dummy user for a gamertag);</item>
/// <item>tied to an Xbox account and untied (<see cref="TieToXbox(XboxUser)"/>, <see cref="UntieXbox"/>), and a dummy user
/// made a real one or back (<see cref="BecomeRealUser"/>, <see cref="BecomeDummy"/>): the placeholder's lifecycle;</item>
/// <item>linked to a Discord account (<see cref="LinkDiscord(long)"/>), a region chosen (<see cref="ChooseRegion"/>), a
/// display name given (<see cref="Rename"/>);</item>
/// <item>a Late League result recorded (<see cref="RecordLateLeagueWin"/>, <see cref="RecordLateLeagueLoss"/>);</item>
/// <item>a merge taking the source's accounts, record and experiences (<see cref="TakeXboxFrom"/>,
/// <see cref="TakeDiscordFrom"/>, <see cref="TakeLateLeagueRecordFrom"/>, <see cref="TakeExperiencesFrom"/>).</item>
/// </list>
/// A region is set with who chose it (CK_Auth_Users_KnownRegionSource, CK_Auth_Users_RegionHasSource); the Late League
/// record is never negative, has one streak at a time, and no streak longer than its wins or losses
/// (CK_Auth_Users_LateLeagueRecord).
/// </summary>
[AggregateRoot]
public class User : IdentityUser<int>, IAuditStamp
{
    public const int DisplayNameMaxLength = 30;
    /// <summary>A new player's Late League MMR.</summary>
    public const int StartingMMR = 1000;

    /// <summary>For EF. A user begins through <see cref="Register"/> or <see cref="ImportPlaceholder"/>.</summary>
    private User() => SecurityStamp = Guid.NewGuid().ToString();

    /// <summary>
    /// A person's account: signed up on the site (with a password or an external login), or made by a sysadmin or the bot.
    /// Not a dummy user, and tied to nothing; Identity's <c>UserManager</c> saves it.
    /// </summary>
    public static User Register(string userName, string? email = null, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        var user = new User { UserName = userName, Email = email };
        user.Rename(displayName);
        return user;
    }

    /// <summary>
    /// The dummy user the import stands in for a gamertag (<see cref="ImportPlaceholderPrefix"/>): shown by the gamertag (cut
    /// to what a display name holds), and tied to the Xbox account that holds it now when one is known and no user holds
    /// it. Saved straight to the context, not by Identity, so its normalized name is set here.
    /// </summary>
    public static User ImportPlaceholder(string userName, string gamertag, long? xboxUserID)
    {
        if (PlaceholderGamertagOf(userName) is null)
            throw new ArgumentException($"An import placeholder's user name begins {ImportPlaceholderPrefix}", nameof(userName));
        ArgumentException.ThrowIfNullOrWhiteSpace(gamertag);
        return new User
        {
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = gamertag.Length > DisplayNameMaxLength ? gamertag[..DisplayNameMaxLength] : gamertag,
            IsDummyUser = true,
            XboxUserID = xboxUserID,
        };
    }

    /// <summary>
    /// A user in any state, past the rules: for tests, which seed users as the app leaves them (and old rows for the
    /// migration tests). Identity's fields too, so a test makes one in one expression.
    /// </summary>
    internal static User Seed(string? userName = null, string? displayName = null, bool isDummyUser = false, XboxUser? xboxUser = null,
        long? xboxUserID = null, DiscordUser? discordUser = null, long? discordUserID = null, int? regionID = null, RegionSource? regionSource = null,
        int mmr = StartingMMR, int wins = 0, int losses = 0, int winStreak = 0, int lossStreak = 0, int id = 0, string? normalizedUserName = null,
        string? email = null, string? passwordHash = null)
        => new()
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = normalizedUserName,
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            IsDummyUser = isDummyUser,
            XboxUser = xboxUser,
            XboxUserID = xboxUserID ?? xboxUser?.XboxUserID,
            DiscordUser = discordUser,
            DiscordUserID = discordUserID ?? discordUser?.DiscordUserID,
            RegionID = regionID,
            RegionSource = regionSource,
            MMR = mmr,
            Wins = wins,
            Losses = losses,
            WinStreak = winStreak,
            LossStreak = lossStreak,
        };

    /// <summary>A Late League record as a test needs it, past the rules: what is not given stays.</summary>
    internal void SeedLateLeagueRecord(int? mmr = null, int? wins = null, int? losses = null, int? winStreak = null, int? lossStreak = null)
    {
        MMR = mmr ?? MMR;
        Wins = wins ?? Wins;
        Losses = losses ?? Losses;
        WinStreak = winStreak ?? WinStreak;
        LossStreak = lossStreak ?? LossStreak;
    }

    /// <summary>A game version the user played, for tests (the app has no move that adds one yet).</summary>
    internal void SeedExperience(int gameVersionID) => _personExperiences.Add(new UserExperience(gameVersionID));

    // Nothing outside the user sets its own fields, foreign keys or navigations: only its methods below, and EF (which uses
    // the private setters). Identity's fields (inherited) are Identity's.
    public int? RegionID { get; private set; }
    public Region? Region { get; private set; }
    /// <summary>Who set <see cref="RegionID"/>, set whenever it is: the import never overwrites a region the user or a
    /// sysadmin chose.</summary>
    public RegionSource? RegionSource { get; private set; }
    public string? DisplayName { get; private set; }
    public bool IsDummyUser { get; private set; }

    /// <summary>The prefix of an import placeholder's user name: a dummy user the historical import stands in for a
    /// gamertag with ("historical:Name"), until it is claimed or tied to the Xbox account that holds the gamertag now.
    /// Identity's allowed user name characters have no colon or space, so no account made by signing up can ever collide
    /// with one.</summary>
    public const string ImportPlaceholderPrefix = "historical:";

    /// <summary><see cref="IsImportPlaceholder"/> as an expression a query can translate: case-insensitive on the prefix
    /// as it is, on every database (whose own LIKE differs). #301.</summary>
    public static readonly Expression<Func<User, bool>> IsImportPlaceholderExpression = u =>
        u.IsDummyUser && u.XboxUserID == null && u.UserName != null && u.UserName.ToUpper().StartsWith(UpperImportPlaceholderPrefix);

    private static readonly string UpperImportPlaceholderPrefix = ImportPlaceholderPrefix.ToUpperInvariant();

    /// <summary>An import placeholder not yet tied to an Xbox account: the one definition, for a loaded user. Inside a
    /// query lambda use <see cref="IsImportPlaceholderExpression"/>, so it translates to SQL. Not a column. #301.</summary>
    public bool IsImportPlaceholder => IsDummyUser && XboxUserID is null && PlaceholderGamertagOf(UserName) is not null;

    /// <summary>The gamertag an import placeholder's user name is for: "historical:Name" is "Name", and so is
    /// "historical:Name:123" (an Xbox account's own placeholder, named after its XUID too when the plain name is taken: the
    /// one suffix the app adds). An archive name keeps its own colons ("historical:Dr:Who" is "Dr:Who"). Null for anyone
    /// else, whose user name is their login name, never shown. Case-insensitive on the prefix. #301.</summary>
    public static string? PlaceholderGamertagOf(string? userName)
    {
        if (userName is null || !userName.StartsWith(ImportPlaceholderPrefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var name = userName[ImportPlaceholderPrefix.Length..];
        var suffix = name.LastIndexOf(':');
        return suffix > 0 && suffix < name.Length - 1 && name[(suffix + 1)..].All(char.IsAsciiDigit) ? name[..suffix] : name;
    }

    public long? XboxUserID { get; private set; }
    public XboxUser? XboxUser { get; private set; }
    public long? DiscordUserID { get; private set; }
    public DiscordUser? DiscordUser { get; private set; }
    // Read-only: other aggregates (a team, a season's signups) and Identity add these rows, never through the user (EF uses
    // the fields)
    public IReadOnlyCollection<TeamPlayer> TeamPlayers => _teamPlayers;
    private readonly HashSet<TeamPlayer> _teamPlayers = [];
    /// <summary>The game versions the user played: part of the user, changed only through its methods.</summary>
    public IReadOnlyCollection<UserExperience> PersonExperiences => _personExperiences;
    private readonly HashSet<UserExperience> _personExperiences = [];
    public IReadOnlyCollection<SeasonSignup> SeasonSignups => _seasonSignups;
    private readonly HashSet<SeasonSignup> _seasonSignups = [];
    public IReadOnlyCollection<UserRole> UserRoles => _userRoles;
    private readonly HashSet<UserRole> _userRoles = [];
    public IReadOnlyCollection<UserLogin> UserLogins => _userLogins;
    private readonly HashSet<UserLogin> _userLogins = [];
    public IReadOnlyCollection<UserClaim> UserClaims => _userClaims;
    private readonly HashSet<UserClaim> _userClaims = [];

    // Late League:
    public QueuedPlayer? QueuedPlayer { get; private set; }
    /// <summary>Read-only: a matched player is made by its match (<see cref="MatchedMatch"/>).</summary>
    public IReadOnlyCollection<MatchedPlayer> MatchedPlayers => _matchedPlayers;
    private readonly HashSet<MatchedPlayer> _matchedPlayers = [];
    public int MMR { get; private set; } = StartingMMR;
    public int WinStreak { get; private set; }
    public int LossStreak { get; private set; }
    public int Wins { get; private set; }
    public int Losses { get; private set; }

    // The audit columns: the audit interceptor's alone, through IAuditStamp
    public int? CreatedByID { get; private set; }
    public int? ModifiedByID { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ModifiedAt { get; private set; }
    int? IAuditStamp.CreatedByID { get => CreatedByID; set => CreatedByID = value; }
    int? IAuditStamp.ModifiedByID { get => ModifiedByID; set => ModifiedByID = value; }
    DateTime IAuditStamp.CreatedAt { get => CreatedAt; set => CreatedAt = value; }
    DateTime IAuditStamp.ModifiedAt { get => ModifiedAt; set => ModifiedAt = value; }

    /// <summary>
    /// Tied to the Xbox account: the user plays as it, and its gamertag is the name the app shows
    /// (<see cref="XboxTie.Tied"/>); the same account again changes nothing (<see cref="XboxTie.AlreadyTied"/>). Refused,
    /// changing nothing, while the user is tied to another account (<see cref="XboxTie.TiedToOther"/>), or else another
    /// user holds this one (<see cref="XboxTie.HeldByOther"/>): untie that first (<see cref="UntieXbox"/>).
    /// </summary>
    public XboxTie TieToXbox(XboxUser xboxUser)
    {
        ArgumentNullException.ThrowIfNull(xboxUser);
        if (XboxUserID is null && xboxUser.User is { } holder && holder != this && holder.XboxUserID == xboxUser.XboxUserID)
            return XboxTie.HeldByOther;
        var tie = TieToXbox(xboxUser.XboxUserID);
        if (tie is not XboxTie.TiedToOther)
            XboxUser = xboxUser;
        return tie;
    }

    /// <summary>
    /// Tied to the Xbox account by its XUID alone (the import, whose caller has checked that no user holds it): as
    /// <see cref="TieToXbox(XboxUser)"/>, refused while the user is tied to another one.
    /// </summary>
    public XboxTie TieToXbox(long xboxUserID)
    {
        if (XboxUserID == xboxUserID)
            return XboxTie.AlreadyTied;
        if (XboxUserID is not null)
            return XboxTie.TiedToOther;
        XboxUserID = xboxUserID;
        return XboxTie.Tied;
    }

    /// <summary>No longer tied to an Xbox account: a sysadmin's change, or another user taking it. Untied already, nothing
    /// changes.</summary>
    public void UntieXbox()
    {
        XboxUserID = null;
        XboxUser = null;
    }

    /// <summary>A dummy user made a real one (a sysadmin's call): no longer a placeholder a sign-up takes over.</summary>
    public void BecomeRealUser() => IsDummyUser = false;

    /// <summary>Marked a dummy user (a sysadmin's call): an account nobody plays as, which a real one may take over.</summary>
    public void BecomeDummy() => IsDummyUser = true;

    /// <summary>The name the app shows when it has no gamertag or Discord name for the user (null for none). At most
    /// <see cref="DisplayNameMaxLength"/> characters.</summary>
    public void Rename(string? displayName)
    {
        if (displayName?.Length > DisplayNameMaxLength)
            throw new ArgumentException($"A display name is at most {DisplayNameMaxLength} characters", nameof(displayName));
        DisplayName = displayName;
    }

    /// <summary>
    /// Linked to the Discord account (the bot's and the site's Discord sign-in): <see cref="DiscordLinking.Linked"/>, or
    /// <see cref="DiscordLinking.AlreadyLinked"/> for the same one. Refused while the user is linked to another
    /// (<see cref="DiscordLinking.LinkedToOther"/>): nothing changes.
    /// </summary>
    public DiscordLinking LinkDiscord(long discordUserID)
    {
        if (DiscordUserID == discordUserID)
            return DiscordLinking.AlreadyLinked;
        if (DiscordUserID is not null)
            return DiscordLinking.LinkedToOther;
        DiscordUserID = discordUserID;
        return DiscordLinking.Linked;
    }

    /// <summary>The same as <see cref="LinkDiscord(long)"/>, given the Discord account itself.</summary>
    public DiscordLinking LinkDiscord(DiscordUser discordUser)
    {
        ArgumentNullException.ThrowIfNull(discordUser);
        var linking = LinkDiscord(discordUser.DiscordUserID);
        if (linking is not DiscordLinking.LinkedToOther)
            DiscordUser = discordUser;
        return linking;
    }

    /// <summary>No longer linked to a Discord account. Unlinked already, nothing changes.</summary>
    public void UnlinkDiscord()
    {
        DiscordUserID = null;
        DiscordUser = null;
    }

    /// <summary>
    /// The user's region, chosen <paramref name="by"/> the user or a sysadmin (null clears it, and the import may fill it
    /// again), or filled in by the import: the import fills a region only where no one chose one, never clears one, and
    /// never overwrites the user's or a sysadmin's (<see cref="RegionChoice.KeptChosen"/>).
    /// </summary>
    public RegionChoice ChooseRegion(int? regionID, RegionSource by)
    {
        if (!Enum.IsDefined(by))
            throw new ArgumentOutOfRangeException(nameof(by), by, "Not a source of a region");
        if (by is Models.RegionSource.Import)
        {
            if (RegionID is not null && RegionSource is not Models.RegionSource.Import)
                return RegionChoice.KeptChosen;
            if (regionID is null)
                return RegionChoice.Unchanged;
        }
        var source = regionID is null ? (RegionSource?)null : by;
        if (RegionID == regionID && RegionSource == source)
            return RegionChoice.Unchanged;
        RegionID = regionID;
        RegionSource = source;
        return RegionChoice.Set;
    }

    /// <summary>
    /// A Late League win: a win more, the win streak one longer (any loss streak ended), and the MMR up by the rules' gain
    /// and streak bonus. Returns the MMR gained.
    /// </summary>
    public int RecordLateLeagueWin(LateLeagueRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Wins++;
        WinStreak++;
        LossStreak = 0;
        var bonus = WinStreak >= rules.WinThreshold ? Math.Min(rules.MaxBonus, (WinStreak - rules.WinThreshold + 1) * rules.BonusPerWin) : 0;
        var gained = rules.Gain + bonus;
        MMR += gained;
        return gained;
    }

    /// <summary>
    /// A Late League loss: a loss more, the loss streak one longer (any win streak ended), and the MMR down by the rules'
    /// loss and streak penalty, never below 0. Returns the MMR lost.
    /// </summary>
    public int RecordLateLeagueLoss(LateLeagueRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Losses++;
        LossStreak++;
        WinStreak = 0;
        var penalty = LossStreak >= rules.LossThreshold ? Math.Min(rules.MaxPenalty, (LossStreak - rules.LossThreshold + 1) * rules.PenaltyPerLoss) : 0;
        var lost = Math.Min(MMR, rules.Loss + penalty);
        MMR -= lost;
        return lost;
    }

    /// <summary>A merge: the source's Xbox account becomes this user's, in place of any it had, and the source is untied.
    /// Nothing changes when the source has none.</summary>
    public void TakeXboxFrom(User from)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (from == this || from.XboxUserID is not long xboxUserID)
            return;
        // The keys alone, as EF fixes the navigations up from them (either user's may be loaded)
        from.XboxUserID = null;
        XboxUserID = xboxUserID;
    }

    /// <summary>A merge: the source's Discord account becomes this user's, in place of any it had, and the source is
    /// unlinked. Nothing changes when the source has none.</summary>
    public void TakeDiscordFrom(User from)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (from == this || from.DiscordUserID is not long discordUserID)
            return;
        from.DiscordUserID = null;
        DiscordUserID = discordUserID;
    }

    /// <summary>
    /// A merge: the source's Late League games join this user's record. The MMR is the two weighted by games played, and the
    /// streak the source's when this user has played none; the source is left with no games and no streak.
    /// </summary>
    public void TakeLateLeagueRecordFrom(User from)
    {
        ArgumentNullException.ThrowIfNull(from);
        var games = Wins + Losses;
        var theirs = from.Wins + from.Losses;
        if (from == this || theirs == 0)
            return;
        MMR = (int)Math.Round(((double)MMR * games + (double)from.MMR * theirs) / (games + theirs));
        if (games == 0)
        {
            WinStreak = from.WinStreak;
            LossStreak = from.LossStreak;
        }
        Wins += from.Wins;
        Losses += from.Losses;
        from.Wins = 0;
        from.Losses = 0;
        from.WinStreak = 0;
        from.LossStreak = 0;
    }

    /// <summary>
    /// A merge: the game versions the source played are this user's. An experience is keyed (user, version), so the source's
    /// rows are removed (EF deletes the orphans) and this user gains the versions it lacked. Both users' experiences are loaded.
    /// </summary>
    public void TakeExperiencesFrom(User from)
    {
        ArgumentNullException.ThrowIfNull(from);
        if (from == this)
            return;
        foreach (var experience in from._personExperiences.ToList())
        {
            from._personExperiences.Remove(experience);
            if (_personExperiences.All(e => e.GameVersionID != experience.GameVersionID))
                _personExperiences.Add(new UserExperience(experience.GameVersionID));
        }
    }

    /// <summary>
    /// The name the app shows for a user: their Xbox gamertag, else their Discord name, else their display name. Never their
    /// user name, which is their login name and private. For queries; <see cref="ShownName"/> for a loaded user.
    /// </summary>
    public static readonly Expression<Func<User, UserShownName>> Named = u => new UserShownName
    {
        UserID = u.Id,
        Name = u.XboxUser != null ? u.XboxUser.Gamertag : u.DiscordUser != null ? u.DiscordUser.DiscordUsername : u.DisplayName,
    };

    private static readonly Func<User, UserShownName> NamedOf = Named.Compile();

    /// <summary>The name the app shows for this user (<see cref="Named"/>): needs its Xbox and Discord accounts loaded.</summary>
    public string? ShownName() => NamedOf(this).Name;
}

/// <summary>A user and the name the app shows for them (<see cref="User.Named"/>); null for a user with none of the three.</summary>
public sealed class UserShownName
{
    public int UserID { get; init; }
    public string? Name { get; init; }
}

/// <summary>What <see cref="User.TieToXbox(XboxUser)"/> did.</summary>
public enum XboxTie
{
    Tied,
    /// <summary>Tied to that Xbox account already: nothing changed.</summary>
    AlreadyTied,
    /// <summary>Tied to another Xbox account: refused, nothing changed.</summary>
    TiedToOther,
    /// <summary>Another user holds the Xbox account: refused, nothing changed.</summary>
    HeldByOther,
}

/// <summary>What <see cref="User.LinkDiscord(long)"/> did.</summary>
public enum DiscordLinking
{
    Linked,
    /// <summary>Linked to that Discord account already: nothing changed.</summary>
    AlreadyLinked,
    /// <summary>Linked to another Discord account: refused, nothing changed.</summary>
    LinkedToOther,
}

/// <summary>What <see cref="User.ChooseRegion"/> did.</summary>
public enum RegionChoice
{
    Set,
    /// <summary>The region (and who chose it) was that already, or the import had none to fill in.</summary>
    Unchanged,
    /// <summary>The import's region, refused: the user or a sysadmin chose the one the user has.</summary>
    KeptChosen,
}

/// <summary>
/// The Late League's MMR rules (the bot's options): a win gains <see cref="Gain"/> and a loss loses <see cref="Loss"/>, and
/// from a streak's threshold each game more adds its bonus or penalty, up to the cap.
/// </summary>
public sealed record LateLeagueRules(int KFactor, int WinThreshold, int BonusPerWin, int MaxBonus, int LossThreshold, int PenaltyPerLoss, int MaxPenalty)
{
    /// <summary>A win's MMR before any streak bonus: three quarters of the K-factor.</summary>
    public int Gain => (int)Math.Round(KFactor * 0.75);
    /// <summary>A loss's MMR before any streak penalty: five eighths of the K-factor.</summary>
    public int Loss => (int)Math.Round(KFactor * 0.625);
}
