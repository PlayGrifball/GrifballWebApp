using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class XuidLookupConfiguration : IEntityTypeConfiguration<XuidLookup>
{
    public void Configure(EntityTypeBuilder<XuidLookup> entity)
    {
        // Bookkeeping of a background job (like HistoricalImportJobs): no row history
        entity.ToTable("XuidLookups", "Xbox");

        entity.HasKey(e => e.UserID);
        entity.Property(e => e.Error).HasMaxLength(200);

        entity.HasOne(e => e.User)
            .WithOne()
            .HasForeignKey<XuidLookup>(e => e.UserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.LookedUpAt);
    }
}
