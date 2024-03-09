using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonMatchConfiguration : IEntityTypeConfiguration<SeasonMatch>
{
    public void Configure(EntityTypeBuilder<SeasonMatch> entity)
    {
        entity.ToTable("SeasonMatches", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.SeasonMatchID);

        entity.HasOne(d => d.Season)
            .WithMany(p => p.SeasonMatches)
            .HasForeignKey(d => d.SeasonID);

        // TODO: Determine correct delete behavior.

        entity.HasOne(d => d.AwayTeam)
            .WithMany(p => p.AwayMatches)
            .HasForeignKey(d => d.AwayTeamID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(d => d.HomeTeam)
            .WithMany(p => p.HomeMatches)
            .HasForeignKey(d => d.HomeTeamID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(d => d.MatchLink)
            .WithOne(p => p.SeasonMatch)
            .HasForeignKey<MatchLink>(d => d.SeasonMatchID)
            .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonMatch> entity);
}
