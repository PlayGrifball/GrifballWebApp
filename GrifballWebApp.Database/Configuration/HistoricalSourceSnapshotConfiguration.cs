using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class HistoricalSourceSnapshotConfiguration : IEntityTypeConfiguration<HistoricalSourceSnapshot>
{
    public void Configure(EntityTypeBuilder<HistoricalSourceSnapshot> entity)
    {
        // Downloaded content, only ever added: no row history (it would double megabytes of bytes)
        entity.ToTable("HistoricalSourceSnapshots", "Event");

        entity.HasKey(e => e.HistoricalSourceSnapshotID);

        entity.Property(e => e.HistoricalSourceID).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        entity.Property(e => e.ContentType).HasMaxLength(HistoricalSourceSnapshot.MaxContentTypeLength);
        // varbinary(max) on SQL Server, bytea on Postgres
        entity.Property(e => e.Content).IsRequired();

        entity.HasOne(e => e.Source)
            .WithMany(s => s.Snapshots)
            .HasForeignKey(e => e.HistoricalSourceID)
            .OnDelete(DeleteBehavior.Cascade);

        // A source's latest snapshot
        entity.HasIndex(e => new { e.HistoricalSourceID, e.FetchedAt });
    }
}
