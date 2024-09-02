using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonAvailabilityConfiguration : IEntityTypeConfiguration<SeasonAvailability>
{
    public void Configure(EntityTypeBuilder<SeasonAvailability> entity)
    {
        entity.ToTable("SeasonAvailability", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.SeasonID, e.AvailabilityOptionID });

        entity.HasOne(d => d.Season)
            .WithMany(p => p.SeasonAvailability)
            .HasForeignKey(d => d.SeasonID);

        entity.HasOne(d => d.AvailabilityOption)
            .WithMany(p => p.SeasonAvailability)
            .HasForeignKey(d => d.AvailabilityOptionID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonAvailability> entity);
}
