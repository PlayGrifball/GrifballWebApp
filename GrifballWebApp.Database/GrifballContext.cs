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
    public virtual DbSet<MatchParticipant> MatchParticipants { get; set; }
    public virtual DbSet<XboxUser> XboxUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configuration.MatchConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.MatchPlayerConfiguration());
        modelBuilder.ApplyConfiguration(new Configuration.XboxUserConfiguration());

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
