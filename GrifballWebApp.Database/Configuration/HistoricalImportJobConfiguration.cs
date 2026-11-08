using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class HistoricalImportJobConfiguration : IEntityTypeConfiguration<HistoricalImportJob>
{
    public void Configure(EntityTypeBuilder<HistoricalImportJob> entity)
    {
        // Not temporal: progress is written often and the payload is megabytes
        // Each status has what it needs: a finish time once ended (and only then), the archive only until it ends, and only an
        // upload's import has one (HistoricalImportJob's moves)
        entity.ToTable("HistoricalImportJobs", "Event", tb =>
        {
            tb.HasCheckConstraint("CK_Event_HistoricalImportJobs_FinishedOnceEnded",
                "(Status IN (3, 4) AND FinishedAt IS NOT NULL) OR (Status IN (1, 2) AND FinishedAt IS NULL)");
            tb.HasCheckConstraint("CK_Event_HistoricalImportJobs_PayloadUntilEnded",
                "Payload IS NULL OR Status IN (1, 2)");
            tb.HasCheckConstraint("CK_Event_HistoricalImportJobs_PayloadOnlyForImports",
                "Payload IS NULL OR Kind = 1");
        });

        entity.HasKey(e => e.HistoricalImportJobID);
        // Computed from the status and kind
        entity.Ignore(e => e.IsAppliablePreview);

        entity.Property(e => e.Stage).HasMaxLength(HistoricalImportJob.MaxStageLength);
        entity.HasIndex(e => e.Status);
        // Jobs queued before these columns were imports of an upload, run by hand
        entity.Property(e => e.Kind).HasDefaultValue(HistoricalImportJobKind.Import);
        entity.Property(e => e.Trigger).HasDefaultValue(HistoricalImportJobTrigger.Manual);
    }
}
