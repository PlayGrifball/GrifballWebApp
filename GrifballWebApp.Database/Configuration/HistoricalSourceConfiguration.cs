using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class HistoricalSourceConfiguration : IEntityTypeConfiguration<HistoricalSource>
{
    public void Configure(EntityTypeBuilder<HistoricalSource> entity)
    {
        // Not temporal: the worker writes the fetch status on every refresh
        entity.ToTable("HistoricalSources", "Event");

        entity.HasKey(e => e.HistoricalSourceID);

        entity.Property(e => e.HistoricalSourceID).HasMaxLength(100);
        entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Title).HasMaxLength(300).IsRequired();
        entity.Property(e => e.Url).HasMaxLength(1000).IsRequired();
        entity.Property(e => e.SheetKey).HasMaxLength(100);
        entity.Property(e => e.Gid).HasMaxLength(30);
        entity.Property(e => e.CsvSheetName).HasMaxLength(100);
        entity.Property(e => e.WaybackTimestamp).HasMaxLength(14);
        entity.Property(e => e.League).HasMaxLength(50);
        entity.Property(e => e.GameVersion).HasMaxLength(30);
        entity.Property(e => e.Format).HasMaxLength(30);
        entity.Property(e => e.LastFetchStatus).HasMaxLength(300);
    }
}
