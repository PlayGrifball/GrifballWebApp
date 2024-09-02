using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

public partial class GrifballContext : IdentityDbContext<User, Role, int, IdentityUserClaim<int>, UserRole, IdentityUserLogin<int>, IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    public GrifballContext() : base()
    {
    }

    public GrifballContext(DbContextOptions<GrifballContext> options) : base(options)
    {
    }

    public virtual DbSet<AvailabilityOption> Availability { get; set; }
    public virtual DbSet<GameVersion> GameVersions { get; set; }
    public virtual DbSet<Match> Matches { get; set; }
    public virtual DbSet<MatchBracketInfo> MatchBracketInfo { get; set; }
    public virtual DbSet<MatchLink> MatchLinks { get; set; }
    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }
    public virtual DbSet<MatchTeam> MatchTeams { get; set; }
    public virtual DbSet<Medal> Medals { get; set; }
    public virtual DbSet<MedalDifficulty> MedalDifficulties { get; set; }
    public virtual DbSet<MedalEarned> MedalEarned { get; set; }
    public virtual DbSet<MedalType> MedalTypes { get; set; }
    public virtual DbSet<Region> Regions { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonAvailability> SeasonAvailability { get; set; }
    public virtual DbSet<SeasonMatch> SeasonMatches { get; set; }
    public virtual DbSet<SeasonSignup> SeasonSignups { get; set; }
    public virtual DbSet<SignupAvailability> SignupAvailability { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<TeamAvailability> TeamAvailability { get; set; }
    public virtual DbSet<TeamPlayer> TeamPlayers { get; set; }
    public virtual DbSet<UserExperience> UserExperiences { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new Configuration.AvailabilityOptionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.GameVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchBracketInfoConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchLinkConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchTeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalDifficultyConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalEarnedConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalTypeConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RegionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RoleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonSignupConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SignupAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamAvailabilityConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserExperienceConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.XboxUserConfiguration());

        modelBuilder.Entity<IdentityRoleClaim<int>>(b => b.ToTable("RoleClaims", "Auth", tb => tb.IsTemporal()));
        modelBuilder.Entity<IdentityUserClaim<int>>(b => b.ToTable("UserClaims", "Auth", tb => tb.IsTemporal()));
        modelBuilder.Entity<IdentityUserLogin<int>>(b =>
        {
            b.ToTable("UserLogins", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.LoginProvider, e.ProviderKey });
        });
        modelBuilder.Entity<IdentityUserToken<int>>(b =>
        {
            b.ToTable("UserTokens", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
