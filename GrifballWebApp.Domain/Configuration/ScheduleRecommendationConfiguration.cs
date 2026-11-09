using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;

public class ScheduleRecommendationConfiguration : IEntityTypeConfiguration<ScheduleRecommendation>
{
    public void Configure(EntityTypeBuilder<ScheduleRecommendation> entity)
    {
        // Not temporal: a job's output, written once (and stamped when applied); the season matches it sets keep their history
        entity.ToTable("ScheduleRecommendations", "Event", tb =>
        {
            // The job's fitness (SeasonScheduleEvaluator: 1 / (1 + penalties)) and generations, as Propose refuses others
            tb.HasCheckConstraint("CK_Event_ScheduleRecommendations_FitnessInRange", "Fitness >= 0 AND Fitness <= 1");
            tb.HasCheckConstraint("CK_Event_ScheduleRecommendations_GenerationsNotNegative", "Generations >= 0");
            // Applied once: who applied it only once it is (who may be unknown: nobody signed in)
            tb.HasCheckConstraint("CK_Event_ScheduleRecommendations_AppliedByOnlyWhenApplied", "AppliedAt IS NOT NULL OR AppliedBy IS NULL");
        });

        entity.HasKey(e => e.ScheduleRecommendationID);
        entity.Property(e => e.RequestedBy).HasMaxLength(256);
        entity.Property(e => e.AppliedBy).HasMaxLength(256);

        // Worked out from its state and its matches, not stored
        entity.Ignore(e => e.IsApplied);
        entity.Ignore(e => e.ConflictCount);

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

        // Worked out from its flags, not stored
        entity.Ignore(e => e.HasConflict);
    }
}
