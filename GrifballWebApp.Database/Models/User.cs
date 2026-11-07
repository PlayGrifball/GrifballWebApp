using Microsoft.AspNetCore.Identity;
using System.Linq.Expressions;

namespace GrifballWebApp.Database.Models;
public class User : IdentityUser<int>, IAuditable
{
    public User()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        PersonExperiences = new HashSet<UserExperience>();
        SeasonSignups = new HashSet<SeasonSignup>();
        UserRoles = new HashSet<UserRole>();
        SecurityStamp = Guid.NewGuid().ToString();
    }
    public int? RegionID { get; set; }
    public Region? Region { get; set; }
    /// <summary>Who set <see cref="RegionID"/>: the import never overwrites a region the user or a sysadmin chose.</summary>
    public RegionSource? RegionSource { get; set; }
    public string? DisplayName { get; set; }
    public bool IsDummyUser { get; set; }
    // TODO: Availablity
    public long? XboxUserID { get; set; }
    public XboxUser? XboxUser { get; set; }
    public long? DiscordUserID { get; set; }
    public DiscordUser? DiscordUser { get; set; }
    public virtual ICollection<TeamPlayer> TeamPlayers { get; set; }
    public virtual ICollection<UserExperience> PersonExperiences { get; set; }
    public virtual ICollection<SeasonSignup> SeasonSignups { get; set; }
    public virtual ICollection<UserRole> UserRoles { get; set; }
    public virtual ICollection<UserLogin> UserLogins { get; set; }
    public virtual ICollection<UserClaim> UserClaims { get; set; }

    // Late League:
    public QueuedPlayer? QueuedPlayer { get; set; }
    /// <summary>Read-only: a matched player is made by its match (<see cref="MatchedMatch"/>).</summary>
    public IReadOnlyCollection<MatchedPlayer> MatchedPlayers => _matchedPlayers;
    private readonly HashSet<MatchedPlayer> _matchedPlayers = [];
    public int MMR { get; set; } = 1000; // Default MMR for new players
    public int WinStreak { get; set; } = 0;
    public int LossStreak { get; set; } = 0;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
    public int? CreatedByID { get; set; }
    public int? ModifiedByID { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

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

