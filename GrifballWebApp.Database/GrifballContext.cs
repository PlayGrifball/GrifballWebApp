using GrifballWebApp.Database.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

public partial class GrifballContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public GrifballContext() : base()
    {
    }

    public GrifballContext(DbContextOptions<GrifballContext> options) : base(options)
    {
    }

    public virtual DbSet<GameVersion> GameVersions { get; set; }
    public virtual DbSet<Match> Matches { get; set; }
    public virtual DbSet<MatchBracketInfo> MatchBracketInfo { get; set; }
    public virtual DbSet<MatchLink> MatchLinks { get; set; }
    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }
    public virtual DbSet<Medal> Medals { get; set; }
    public virtual DbSet<MedalDifficulty> MedalDifficulties { get; set; }
    public virtual DbSet<MedalEarned> MedalEarned { get; set; }
    public virtual DbSet<MedalType> MedalTypes { get; set; }
    public virtual DbSet<Region> Regions { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonMatch> SeasonMatches { get; set; }
    public virtual DbSet<SeasonSignup> SeasonSignups { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<TeamPlayer> TeamPlayers { get; set; }
    public virtual DbSet<UserExperience> UserExperiences { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new Configuration.GameVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchBracketInfoConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchLinkConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalDifficultyConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalEarnedConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalTypeConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RegionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonSignupConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.UserExperienceConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.XboxUserConfiguration());

        modelBuilder.Entity<IdentityRoleClaim<int>>(b => b.ToTable("RoleClaims", "Auth", tb => tb.IsTemporal()));
        modelBuilder.Entity<IdentityRole<int>>(b =>
        {
            b.ToTable("Roles", "Auth", tb => tb.IsTemporal());

            b.HasData(new List<IdentityRole<int>>()
            {
                new IdentityRole<int>()
                {
                    Id = 1,
                    Name = "Sysadmin",
                    NormalizedName = "SYSADMIN",
                    ConcurrencyStamp = "cda78946-53d4-4154-b723-2b33b95341cc"
                },
                new IdentityRole<int>()
                {
                    Id = 2,
                    Name = "Commissioner",
                    NormalizedName = "COMMISSIONER",
                    ConcurrencyStamp = "37ff5fdf-5cb8-403c-bb9e-81db05a5fcdd"
                },
                new IdentityRole<int>()
                {
                    Id = 3,
                    Name = "Player",
                    NormalizedName = "PLAYER",
                    ConcurrencyStamp = "0be992c7-f824-40c7-8975-964068c7fbfe"
                },
            });
        });
        modelBuilder.Entity<IdentityUserClaim<int>>(b => b.ToTable("UserClaims", "Auth", tb => tb.IsTemporal()));
        modelBuilder.Entity<IdentityUserLogin<int>>(b =>
        {
            b.ToTable("UserLogins", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.LoginProvider, e.ProviderKey });
        });
        modelBuilder.Entity<IdentityUserRole<int>>(b =>
        {
            b.ToTable("UserRoles", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.UserId, e.RoleId });
        });
        modelBuilder.Entity<IdentityUserToken<int>>(b =>
        {
            b.ToTable("UserTokens", "Auth", tb => tb.IsTemporal());
            b.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });
        });
        //modelBuilder.Entity<IdentityUser>(b => b.ToTable("Users", "Auth", tb => tb.IsTemporal()));

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
