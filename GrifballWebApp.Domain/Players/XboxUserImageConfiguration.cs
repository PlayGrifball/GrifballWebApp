using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class XboxUserImageConfiguration : IEntityTypeConfiguration<XboxUserImage>
{
    public void Configure(EntityTypeBuilder<XboxUserImage> entity)
    {
        // Bookkeeping of the image jobs (like XuidLookups): whole images rewritten on every change would only grow a history
        entity.ToTable("XboxUserImages", "Xbox");

        entity.HasKey(e => new { e.XboxUserID, e.Kind });
        entity.Property(e => e.Source).HasMaxLength(500);
        entity.Property(e => e.ContentType).HasMaxLength(100);
        entity.Property(e => e.Error).HasMaxLength(200);

        entity.HasOne(e => e.XboxUser)
            .WithMany()
            .HasForeignKey(e => e.XboxUserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.Kind, e.CheckedAt });
    }
}
