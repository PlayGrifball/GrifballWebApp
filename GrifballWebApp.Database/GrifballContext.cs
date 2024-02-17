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

    public virtual DbSet<Match> Matches { get; set; }
    public virtual DbSet<MatchLink> MatchLinks { get; set; }
    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }
    public virtual DbSet<Medal> Medals { get; set; }
    public virtual DbSet<MedalDifficulty> MedalDifficulties { get; set; }
    public virtual DbSet<MedalEarned> MedalEarned { get; set; }
    public virtual DbSet<MedalType> MedalTypes { get; set; }
    public virtual DbSet<Person> Persons { get; set; }
    public virtual DbSet<Season> Seasons { get; set; }
    public virtual DbSet<SeasonMatch> SeasonMatches { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<TeamPlayer> TeamPlayers { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configuration.MatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchLinkConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchParticipantConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalDifficultyConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalEarnedConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MedalTypeConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.PersonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.SeasonMatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.TeamPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.XboxUserConfiguration());

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
