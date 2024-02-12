using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MedalTypeConfiguration : IEntityTypeConfiguration<MedalType>
{
    public void Configure(EntityTypeBuilder<MedalType> entity)
    {
        entity.ToTable("MedalTypes", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => e.MedalTypeID);

        // Value always comes index + 1 from 343. Hopefully it never changes!
        entity.Property(e => e.MedalTypeID).ValueGeneratedNever();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MedalType> entity);
}
