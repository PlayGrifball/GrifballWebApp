using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class AvailabilityOptionConfiguration : IEntityTypeConfiguration<AvailabilityOption>
{
    public void Configure(EntityTypeBuilder<AvailabilityOption> entity)
    {
        entity.ToTable("AvailabilityOptions", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.AvailabilityOptionID);

        entity.HasIndex(e => new { e.DayOfWeek, e.Time }).IsUnique();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<AvailabilityOption> entity);
}
