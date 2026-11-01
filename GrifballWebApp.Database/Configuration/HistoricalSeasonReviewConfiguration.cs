using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class HistoricalSeasonReviewConfiguration : IEntityTypeConfiguration<HistoricalSeasonReview>
{
    public void Configure(EntityTypeBuilder<HistoricalSeasonReview> entity)
    {
        // Bookkeeping of its job (like HistoricalImportJobs): no row history
        entity.ToTable("HistoricalSeasonReviews", "Event");

        entity.HasKey(e => e.HistoricalSeasonReviewID);

        entity.Property(e => e.SeasonKey).HasMaxLength(200).IsRequired();
        entity.Property(e => e.SeasonName).HasMaxLength(200);

        entity.HasOne(e => e.Job)
            .WithMany(j => j.Reviews)
            .HasForeignKey(e => e.HistoricalImportJobID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.Status);
    }
}
