using Microsoft.AspNetCore.Identity;

namespace GrifballWebApp.Database.Models;
public class User : IdentityUser<int>
{
    public User()
    {
        TeamPlayers = new HashSet<TeamPlayer>();
        PersonExperiences = new HashSet<UserExperience>();
        SeasonSignups = new HashSet<SeasonSignup>();
        UserRoles = new HashSet<UserRole>();
        SecurityStamp = Guid.NewGuid().ToString();
        MatchedPlayers = new HashSet<MatchedPlayer>();
    }
    public int? RegionID { get; set; }
    public Region? Region { get; set; }
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
    public ICollection<MatchedPlayer> MatchedPlayers { get; set; }
    public int MMR { get; set; } = 1000; // Default MMR for new players
    public int WinStreak { get; set; } = 0;
    public int LossStreak { get; set; } = 0;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
}
