using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> entity)
    {
        entity.ToTable("Matches", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => e.MatchID);

        // Value always comes from 343
        entity.Property(e => e.MatchID).ValueGeneratedNever();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Match> entity);
}
