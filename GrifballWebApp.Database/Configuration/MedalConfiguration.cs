using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MedalConfiguration : IEntityTypeConfiguration<Medal>
{
    public void Configure(EntityTypeBuilder<Medal> entity)
    {
        entity.ToTable("Medals", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => e.MedalID);

        // Value always comes from 343
        entity.Property(e => e.MedalID).ValueGeneratedNever();

        entity.HasOne(d => d.MedalDifficulty)
            .WithMany(p => p.Medals)
            .HasForeignKey(d => d.MedalDifficultyID);

        entity.HasOne(d => d.MedalType)
            .WithMany(p => p.Medals)
            .HasForeignKey(d => d.MedalTypeID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Medal> entity);
}
