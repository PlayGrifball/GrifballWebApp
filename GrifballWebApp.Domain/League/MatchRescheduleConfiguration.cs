using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.League;

public class MatchRescheduleConfiguration : IEntityTypeConfiguration<MatchReschedule>
{
    public void Configure(EntityTypeBuilder<MatchReschedule> builder)
    {
        builder.HasKey(mr => mr.MatchRescheduleID);

        // In the Event schema with the season matches it moves (dbo by mistake until #297), with a history of each request as
        // every other table has. Pending until a commissioner decides it: then who and when are both known, and never before.
        builder.ToTable("MatchReschedules", "Event", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Event_MatchReschedules_DecidedHasDecider",
                "(Status = 0 AND ApprovedByUserID IS NULL AND ProcessedAt IS NULL) OR (Status <> 0 AND ApprovedByUserID IS NOT NULL AND ProcessedAt IS NOT NULL)");
        });

        // Computed from Status (the IsPending rule on a loaded request), not a column
        builder.Ignore(mr => mr.AwaitsDecision);

        builder.Property(mr => mr.Reason)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(mr => mr.CommissionerNotes)
            .HasMaxLength(1000);

        builder.Property(mr => mr.RequestedAt)
            .IsRequired();

        builder.Property(mr => mr.Status)
            .IsRequired()
            .HasConversion<int>();

        // Relationship to SeasonMatch: its requests, which only MatchReschedule.Request adds to
        builder.HasOne(mr => mr.SeasonMatch)
            .WithMany(sm => sm.Reschedules)
            .HasForeignKey(mr => mr.SeasonMatchID)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to RequestedByUser
        builder.HasOne(mr => mr.RequestedByUser)
            .WithMany()
            .HasForeignKey(mr => mr.RequestedByUserID)
            .OnDelete(DeleteBehavior.Restrict);

        // Relationship to ApprovedByUser
        builder.HasOne(mr => mr.ApprovedByUser)
            .WithMany()
            .HasForeignKey(mr => mr.ApprovedByUserID)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        // SeasonMatchID: one pending request per match, a unique filtered index (GrifballContext, in each database's quoting)
        builder.HasIndex(mr => mr.Status);
        builder.HasIndex(mr => mr.RequestedAt);
        builder.HasIndex(mr => mr.DiscordThreadID);
    }
}