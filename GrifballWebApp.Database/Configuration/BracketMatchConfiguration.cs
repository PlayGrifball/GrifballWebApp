using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class BracketMatchConfiguration : IEntityTypeConfiguration<BracketMatch>
{
    public void Configure(EntityTypeBuilder<BracketMatch> entity)
    {
        entity.ToTable("BracketMatches", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.BracketMatchID);

        entity.HasOne(d => d.SeasonMatch)
            .WithOne(p => p.BracketMatch)
            .HasForeignKey<BracketMatch>(p => p.SeasonMatchID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<BracketMatch> entity);
}
