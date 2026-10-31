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

        entity.HasIndex(e => e.SeasonName).IsUnique();

        entity.HasOne(e => e.GameVersion)
            .WithMany(g => g.Seasons)
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(e => e.League)
            .WithMany(l => l.Seasons)
            .HasForeignKey(e => e.LeagueID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Season> entity);
}
