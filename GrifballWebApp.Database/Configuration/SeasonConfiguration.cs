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

        entity.Property(e => e.SeasonName).HasMaxLength(30).IsRequired();

        entity.HasIndex(e => e.SeasonName).IsUnique();

        // Configure audit fields
        AuditableEntityConfiguration<Season>.ConfigureAuditFields(entity);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Season> entity);
}
