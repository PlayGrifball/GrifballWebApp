using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.League;

public class SeasonSchedulingRulesConfiguration : IEntityTypeConfiguration<SeasonSchedulingRules>
{
    public void Configure(EntityTypeBuilder<SeasonSchedulingRules> entity)
    {
        // Its own table: a season without a row has the defaults
        entity.ToTable("SeasonSchedulingRules", "Event");

        entity.HasKey(e => e.SeasonID);
        entity.Property(e => e.SeasonID).ValueGeneratedNever();

        // Gone with its season
        entity.HasOne<Season>()
            .WithOne()
            .HasForeignKey<SeasonSchedulingRules>(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
