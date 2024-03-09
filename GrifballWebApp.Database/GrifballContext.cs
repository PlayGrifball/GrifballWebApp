using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GrifballWebApp.Database;

public partial class GrifballContext : DbContext
{
    public GrifballContext() : base()
    {
    }

    public GrifballContext(DbContextOptions<GrifballContext> options) : base(options)
    {
    }

    public virtual DbSet<BracketMatch> BracketMatches { get; set; }
    public virtual DbSet<GameVersion> GameVersions { get; set; }
    public virtual DbSet<Match> Matches { get; set; }
    public virtual DbSet<MatchLink> MatchLinks { get; set; }
    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }
    public virtual DbSet<Medal> Medals { get; set; }
    public virtual DbSet<MedalDifficulty> MedalDifficulties { get; set; }
    public virtual DbSet<MedalEarned> MedalEarned { get; set; }
    public virtual DbSet<MedalType> MedalTypes { get; set; }
    public virtual DbSet<Password> Passwords { get; set; }
    public virtual DbSet<Person> Persons { get; set; }
    public virtual DbSet<PersonExperience> PersonExperiences { get; set; }
    public virtual DbSet<PersonRole> PersonRole { get; set; }
    public virtual DbSet<Region> Regions { get; set; }
    public virtual DbSet<Role> Role { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonMatch> SeasonMatches { get; set; }
    public virtual DbSet<SeasonSignup> SeasonSignups { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<TeamPlayer> TeamPlayers { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configuration.BracketMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.GameVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchLinkConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalDifficultyConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalEarnedConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalTypeConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.PasswordConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.PersonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.PersonExperienceConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.PersonRoleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RegionConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.RoleConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonSignupConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.XboxUserConfiguration());

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
