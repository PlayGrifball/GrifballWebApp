using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MedalDifficultyConfiguration : IEntityTypeConfiguration<MedalDifficulty>
{
    public void Configure(EntityTypeBuilder<MedalDifficulty> entity)
    {
        entity.ToTable("MedalDifficulties", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => e.MedalDifficultyID);

        // Value always comes index + 1 from 343. Hopefully it never changes!
        entity.Property(e => e.MedalDifficultyID).ValueGeneratedNever();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MedalDifficulty> entity);
}
