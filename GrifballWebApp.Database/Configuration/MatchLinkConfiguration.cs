using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchLinkConfiguration : IEntityTypeConfiguration<MatchLink>
{
    public void Configure(EntityTypeBuilder<MatchLink> entity)
    {
        entity.ToTable("MatchLinks", "Event", tb => tb.IsTemporal());
        //entity.ToTable("MatchLinks", "Event");

        entity.HasKey(e => e.MatchLinkID);

        entity.HasOne(d => d.SeasonMatch)
            .WithMany(p => p.MatchLinks)
            .HasForeignKey(d => d.SeasonMatchID);

        //entity.HasOne(d => d.Match)
        //    .WithOne(p => p.MatchLink)
        //    .HasForeignKey<Match>(d => d.MatchID);

        // A infinite match can only be linked once
        entity.HasIndex(d => d.MatchID).IsUnique();

        // A season match should only have one game 1, one game 2, etc.
        entity.HasIndex(d => new { d.SeasonMatchID, d.MatchNumber }).IsUnique();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MatchLink> entity);
}
