using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchBracketInfoConfiguration : IEntityTypeConfiguration<MatchBracketInfo>
{
    public void Configure(EntityTypeBuilder<MatchBracketInfo> entity)
    {
        entity.ToTable("MatchBracketInfo", "Event", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Event_MatchBracketInfo_RequireHomeSeedOrPreviousMatch", @"
(HomeTeamSeedNumber IS NOT NULL AND HomeTeamPreviousMatchBracketInfoID IS NULL) OR
(HomeTeamPreviousMatchBracketInfoID IS NOT NULL AND HomeTeamSeedNumber IS NULL)
");
            tb.HasCheckConstraint("CK_Event_MatchBracketInfo_RequireAwaySeedOrPreviousMatch", @"
(AwayTeamSeedNumber IS NOT NULL AND AwayTeamPreviousMatchBracketInfoID IS NULL) OR
(AwayTeamPreviousMatchBracketInfoID IS NOT NULL AND AwayTeamSeedNumber IS NULL)
");
        });

        entity.HasKey(e => e.MatchBracketInfoID);

        entity.HasOne(d => d.SeasonMatch)
            .WithOne(p => p.BracketMatch)
            .HasForeignKey<MatchBracketInfo>(p => p.SeasonMatchID);

        entity.HasOne(d => d.HomeTeamPreviousMatchBracketInfo)
            .WithMany(p => p.InverseHomeTeamPreviousMatchBracketInfo)
            .HasForeignKey(d => d.HomeTeamPreviousMatchBracketInfoID);

        entity.HasOne(d => d.AwayTeamPreviousMatchBracketInfo)
            .WithMany(p => p.InverseAwayTeamNextMatchBracketInfo)
            .HasForeignKey(d => d.AwayTeamPreviousMatchBracketInfoID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MatchBracketInfo> entity);
}
