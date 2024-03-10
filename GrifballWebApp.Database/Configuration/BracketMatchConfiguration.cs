using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class BracketMatchConfiguration : IEntityTypeConfiguration<BracketMatch>
{
    public void Configure(EntityTypeBuilder<BracketMatch> entity)
    {
        entity.ToTable("BracketMatches", "Event", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Event_BracketMatches_RequireHomeSeedOrPreviousMatch", @"
(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousBracketMatchID IS NULL) OR
(HomeTeamPreviousBracketMatchID IS NOT NULL AND HomeTeamSeedNumber IS NULL)
");
            tb.HasCheckConstraint("CK_Event_BracketMatches_RequireAwaySeedOrPreviousMatch", @"
(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousBracketMatchID IS NULL) OR
(AwayTeamPreviousBracketMatchID IS NOT NULL AND AwayTeamSeedNumber IS NULL)
");
        });

        entity.HasKey(e => e.BracketMatchID);

        entity.HasOne(d => d.SeasonMatch)
            .WithOne(p => p.BracketMatch)
            .HasForeignKey<BracketMatch>(p => p.SeasonMatchID);

        entity.HasOne(d => d.HomeTeamPreviousBracketMatch)
            .WithOne(p => p.HomeTeamNextBracketMatch)
            .HasForeignKey<BracketMatch>(d => d.HomeTeamPreviousBracketMatchID);

        entity.HasOne(d => d.AwayTeamPreviousBracketMatch)
            .WithOne(p => p.AwayTeamNextBracketMatch)
            .HasForeignKey<BracketMatch>(d => d.AwayTeamPreviousBracketMatchID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<BracketMatch> entity);
}
