using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class RecordedGameConfiguration : IEntityTypeConfiguration<RecordedGame>
{
    public void Configure(EntityTypeBuilder<RecordedGame> entity)
    {
        entity.ToTable("Games", "Recorded", tb => tb.IsTemporal());

        entity.HasKey(e => e.RecordedGameID);

        entity.Property(e => e.SourceKey).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Map).HasMaxLength(100);
        entity.Property(e => e.Gametype).HasMaxLength(100);
        // Every game before manual match entry was imported
        entity.Property(e => e.Origin).HasDefaultValue(RecordedGameOrigin.HistoricalImport);

        entity.HasIndex(e => e.SourceKey).IsUnique();
        entity.HasIndex(e => e.SeasonID);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict);

        // No cascade: the season already cascades to its series games through its season matches (SQL Server refuses two
        // paths). Deleting a season with recorded games is refused until they are removed.
        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);
    }
}
