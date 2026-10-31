using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> entity)
    {
        entity.ToTable("Seasons", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.SeasonID);

        entity.Property(e => e.SeasonName).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Division).HasMaxLength(50);
        entity.Property(e => e.HistoricalSourceID).HasMaxLength(100);
        entity.Property(e => e.HistoricalSourceUrl).HasMaxLength(500);

        // Season names are unique within a league: two leagues can each have a "Winter League 2019".
        entity.HasIndex(e => new { e.LeagueID, e.SeasonName }).IsUnique();
        // Unique where set (the filter is the provider's own syntax, set in GrifballContext).
        entity.HasIndex(e => e.HistoricalSourceID).IsUnique();

        entity.HasOne(e => e.GameVersion)
            .WithMany(g => g.Seasons)
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(e => e.League)
            .WithMany(l => l.Seasons)
            .HasForeignKey(e => e.LeagueID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(true);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Season> entity);
}
