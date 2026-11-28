using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Archive;

public class HistoricalProvenanceConfiguration : IEntityTypeConfiguration<HistoricalProvenance>
{
    public void Configure(EntityTypeBuilder<HistoricalProvenance> entity)
    {
        // Written by the import only, its Read rows replaced on every run: no row history
        entity.ToTable("HistoricalProvenance", "Event", tb =>
            tb.HasCheckConstraint("CK_Event_HistoricalProvenance_KnownKind", "Kind IN (1, 2, 3)"));

        entity.HasKey(e => e.HistoricalProvenanceID);

        entity.Property(e => e.Entity).HasMaxLength(HistoricalProvenance.MaxEntityLength).IsRequired();
        entity.Property(e => e.EntityKey).HasMaxLength(HistoricalProvenance.MaxEntityKeyLength).IsRequired();
        entity.Property(e => e.SourceID).HasMaxLength(HistoricalProvenance.MaxSourceIDLength).IsRequired();
        entity.Property(e => e.Sheet).HasMaxLength(HistoricalProvenance.MaxSheetLength);
        entity.Property(e => e.Cells).HasMaxLength(HistoricalProvenance.MaxCellsLength);
        entity.Property(e => e.Value).HasMaxLength(HistoricalProvenance.MaxValueLength);
        entity.Property(e => e.Detail).HasMaxLength(HistoricalProvenance.MaxDetailLength);

        // An entity's rows (the page's lookup, and a run replacing its Read rows)
        entity.HasIndex(e => new { e.Entity, e.EntityKey });
        // What a source's tab made
        entity.HasIndex(e => new { e.SourceID, e.Sheet });
    }
}
