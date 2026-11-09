using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Shared;

// Seeds the regions through their seed factory (Region.Seed)
[AggregateAccess(typeof(Region))]
public partial class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public const string NameNotBlank = "LTRIM(RTRIM(RegionName)) <> ''";

    public void Configure(EntityTypeBuilder<Region> entity)
    {
        entity.ToTable("Regions", "Other", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Other_Regions_NameNotBlank", NameNotBlank);
        });

        entity.HasKey(e => e.RegionID);

        entity.Property(e => e.RegionName).HasMaxLength(50).IsRequired();

        entity.HasIndex(e => e.RegionName).IsUnique();

        Region[] regions =
        {
            Region.Seed("West North America", regionID: 1),
            Region.Seed("Central North America", regionID: 2),
            Region.Seed("East North America", regionID: 3),
            Region.Seed("North Europe", regionID: 4),
            Region.Seed("South Europe", regionID: 5),
            Region.Seed("Australia", regionID: 6),
            // The league sheets' players lists only say "Europe"
            Region.Seed("Europe", regionID: 7),
        };

        entity.HasData(regions);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Region> entity);
}
