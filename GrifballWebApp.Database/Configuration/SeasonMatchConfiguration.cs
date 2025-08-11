using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class SeasonMatchConfiguration : IEntityTypeConfiguration<SeasonMatch>
{
    public void Configure(EntityTypeBuilder<SeasonMatch> entity)
    {
        entity.ToTable("SeasonMatches", "Event", tb =>
        {
            tb.IsTemporal();

            // Make sure that at least 1 team is null. If both are not null then they must not be the same
            tb.HasCheckConstraint("CK_Event_SeasonMatches_MustBeDifferentTeams", @"
(HomeTeamID IS NULL) OR
(AwayTeamID IS NULL) OR
(HomeTeamID != AwayTeamID)
".Replace(Environment.NewLine, " ").Trim()); // Do not put new lines in SQL contraints, it wil cause PendingModelChangesWarning
        });

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

        //entity.HasOne(d => d.MatchLink)
        //    .WithOne(p => p.SeasonMatch)
        //    .HasForeignKey<MatchLink>(d => d.SeasonMatchID)
        //    .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<SeasonMatch> entity);
}
