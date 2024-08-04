using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class TeamAvailabilityConfiguration : IEntityTypeConfiguration<TeamAvailability>
{
    public void Configure(EntityTypeBuilder<TeamAvailability> entity)
    {
        entity.ToTable("TeamAvailability", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.TeamID, e.DayOfWeek, e.Time });

        entity.HasOne(d => d.Team)
            .WithMany(p => p.TeamAvailability)
            .HasForeignKey(d => d.TeamID);

        entity.HasOne(d => d.AvailabilityGridOption)
            .WithMany(p => p.TeamAvailability)
            .HasForeignKey(d => new { d.DayOfWeek, d.Time });

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<TeamAvailability> entity);
}
