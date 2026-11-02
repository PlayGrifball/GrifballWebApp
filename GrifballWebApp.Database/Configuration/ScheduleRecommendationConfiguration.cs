using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class ScheduleRecommendationConfiguration : IEntityTypeConfiguration<ScheduleRecommendation>
{
    public void Configure(EntityTypeBuilder<ScheduleRecommendation> entity)
    {
        // Not temporal: a job's output, written once (and stamped when applied); the season matches it sets keep their history
        entity.ToTable("ScheduleRecommendations", "Event");

        entity.HasKey(e => e.ScheduleRecommendationID);
        entity.Property(e => e.RequestedBy).HasMaxLength(256);
        entity.Property(e => e.AppliedBy).HasMaxLength(256);

        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasMany(e => e.Matches)
            .WithOne(m => m.ScheduleRecommendation)
            .HasForeignKey(m => m.ScheduleRecommendationID)
            .OnDelete(DeleteBehavior.Cascade);

        // A season's latest
        entity.HasIndex(e => new { e.SeasonID, e.CreatedAt });
    }
}

public class ScheduleRecommendationMatchConfiguration : IEntityTypeConfiguration<ScheduleRecommendationMatch>
{
    public void Configure(EntityTypeBuilder<ScheduleRecommendationMatch> entity)
    {
        entity.ToTable("ScheduleRecommendationMatches", "Event");

        entity.HasKey(e => e.ScheduleRecommendationMatchID);
        entity.HasIndex(e => new { e.ScheduleRecommendationID, e.SeasonMatchID }).IsUnique();
    }
}
