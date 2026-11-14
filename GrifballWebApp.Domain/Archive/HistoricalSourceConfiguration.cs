using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Archive;

public class HistoricalSourceConfiguration : IEntityTypeConfiguration<HistoricalSource>
{
    public void Configure(EntityTypeBuilder<HistoricalSource> entity)
    {
        entity.ToTable("HistoricalSources", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.HistoricalSourceID);
        // Computed: the model's rules, for a loaded source
        entity.Ignore(e => e.IsDownloadable);
        entity.Ignore(e => e.IsImported);

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
        entity.Property(e => e.LastFetchStatus).HasMaxLength(HistoricalSource.MaxFetchStatusLength);
    }
}
