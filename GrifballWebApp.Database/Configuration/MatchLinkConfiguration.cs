using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchLinkConfiguration : IEntityTypeConfiguration<MatchLink>
{
    public void Configure(EntityTypeBuilder<MatchLink> entity)
    {
        entity.ToTable("MatchLinks", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.MatchID, e.SeasonMatchID });
        //entity.HasKey(e => e.MatchLinkID);

        //entity.HasOne(d => d.SeasonMatch)
        //    .WithOne(p => p.MatchLink)
        //    .HasForeignKey<SeasonMatch>(d => d.SeasonMatchID);

        //entity.HasOne(d => d.Match)
        //    .WithOne(p => p.MatchLink)
        //    .HasForeignKey<Match>(d => d.MatchID);

        //entity.HasIndex(d => d.MatchID).IsUnique();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MatchLink> entity);
}
