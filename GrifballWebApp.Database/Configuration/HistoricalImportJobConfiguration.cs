using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class HistoricalImportJobConfiguration : IEntityTypeConfiguration<HistoricalImportJob>
{
    public void Configure(EntityTypeBuilder<HistoricalImportJob> entity)
    {
        // Not temporal: progress is written often and the payload is megabytes
        entity.ToTable("HistoricalImportJobs", "Event");

        entity.HasKey(e => e.HistoricalImportJobID);

        entity.Property(e => e.Stage).HasMaxLength(200);
        entity.HasIndex(e => e.Status);
    }
}
