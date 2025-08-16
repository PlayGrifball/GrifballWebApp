using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class MatchRescheduleConfiguration : IEntityTypeConfiguration<MatchReschedule>
{
    public void Configure(EntityTypeBuilder<MatchReschedule> builder)
    {
        builder.HasKey(mr => mr.MatchRescheduleID);

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

        // Relationship to SeasonMatch
        builder.HasOne(mr => mr.SeasonMatch)
            .WithMany()
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
        builder.HasIndex(mr => mr.SeasonMatchID);
        builder.HasIndex(mr => mr.Status);
        builder.HasIndex(mr => mr.RequestedAt);
        builder.HasIndex(mr => mr.DiscordThreadID);
    }
}