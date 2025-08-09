using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> entity)
    {
        entity.ToTable("Teams", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.TeamID);

        entity.HasOne(d => d.Season)
            .WithMany(p => p.Teams)
            .HasForeignKey(d => d.SeasonID);

        entity.HasOne(d => d.Captain)
            .WithOne(p => p.CaptainTeam)
            .HasForeignKey<Team>(d => d.CaptainID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.Property(e => e.TeamName).HasMaxLength(30);

        // Team name must be unique for any given season
        entity.HasIndex(e => new { e.TeamName, e.SeasonID }).IsUnique();

        // Configure audit fields
        AuditableEntityConfiguration<Team>.ConfigureAuditFields(entity);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Team> entity);
}
