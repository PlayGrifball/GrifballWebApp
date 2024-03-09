using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> entity)
    {
        entity.ToTable("Regions", "ITS", tb => tb.IsTemporal());

        entity.HasKey(e => e.RegionID);

        entity.Property(e => e.RegionName).HasMaxLength(50).IsRequired();

        entity.HasIndex(e => e.RegionName).IsUnique();


        Region[] regions = {
            new()
            {
                RegionID = 1,
                RegionName = "West North America"
            },
            new()
            {
                RegionID = 2,
                RegionName = "Central North America"
            },
            new()
            {
                RegionID = 3,
                RegionName = "East North America"
            },
            new()
            {
                RegionID = 4,
                RegionName = "North Europe"
            },
            new()
            {
                RegionID = 5,
                RegionName = "South Europe"
            },
            new()
            {
                RegionID = 6,
                RegionName = "Australia"
            }
        };

        entity.HasData(regions);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Region> entity);
}
