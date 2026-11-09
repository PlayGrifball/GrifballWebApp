using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;

public class HistoricalImportSettingsConfiguration : IEntityTypeConfiguration<HistoricalImportSettings>
{
    public void Configure(EntityTypeBuilder<HistoricalImportSettings> entity)
    {
        // Not temporal: the scheduler writes it on every run
        // One row: the scheduler's
        entity.ToTable("HistoricalImportSettings", "Event", tb =>
            tb.HasCheckConstraint("CK_Event_HistoricalImportSettings_Singleton", "HistoricalImportSettingsID = 1"));

        entity.HasKey(e => e.HistoricalImportSettingsID);
        // The single row has a fixed ID
        entity.Property(e => e.HistoricalImportSettingsID).ValueGeneratedNever();
        entity.Property(e => e.LastScheduledRunNote).HasMaxLength(300);
    }
}
