using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Archive;

public class HistoricalSeasonReviewConfiguration : IEntityTypeConfiguration<HistoricalSeasonReview>
{
    public void Configure(EntityTypeBuilder<HistoricalSeasonReview> entity)
    {
        // Bookkeeping of its job (like HistoricalImportJobs): no row history
        // A decision has its time, and only a decided review has one; only an approval queues an import
        entity.ToTable("HistoricalSeasonReviews", "Event", tb =>
        {
            tb.HasCheckConstraint("CK_Event_HistoricalSeasonReviews_DecidedOnceDecided",
                "(Status IN (3, 4) AND DecidedAt IS NOT NULL) OR (Status IN (1, 2) AND DecidedAt IS NULL)");
            tb.HasCheckConstraint("CK_Event_HistoricalSeasonReviews_ApprovalJobOnlyWhenApproved",
                "ApprovalJobID IS NULL OR Status = 3");
        });

        entity.HasKey(e => e.HistoricalSeasonReviewID);

        entity.Property(e => e.SeasonKey).HasMaxLength(HistoricalSeasonReview.MaxSeasonKeyLength).IsRequired();
        entity.Property(e => e.SeasonName).HasMaxLength(HistoricalSeasonReview.MaxSeasonNameLength);

        entity.HasOne(e => e.Job)
            .WithMany(j => j.Reviews)
            .HasForeignKey(e => e.HistoricalImportJobID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.Status);
    }
}
