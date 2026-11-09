using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;

public class MatchmakingSeasonConfiguration : IEntityTypeConfiguration<MatchmakingSeason>
{
    public void Configure(EntityTypeBuilder<MatchmakingSeason> entity)
    {
        entity.ToTable("MatchmakingSeasons", "Matchmaking", tb =>
        {
            tb.IsTemporal();
            // Its games were played from its start to its end, never backwards (MatchmakingSeason.DateArchived)
            tb.HasCheckConstraint("CK_Matchmaking_MatchmakingSeasons_DatesInOrder", "StartsAt <= EndsAt");
            // A source link is the archive's: only an imported season (its key set) has one (MatchmakingSeason.Imported)
            tb.HasCheckConstraint("CK_Matchmaking_MatchmakingSeasons_SourceUrlOnlyImported",
                "HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL");
        });

        entity.HasKey(e => e.MatchmakingSeasonID);
        // Worked out from its fields, not stored
        entity.Ignore(e => e.IsImported);

        entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
        entity.Property(e => e.HistoricalSourceID).HasMaxLength(200);
        entity.Property(e => e.HistoricalSourceUrl).HasMaxLength(500);

        entity.HasIndex(e => e.HistoricalSourceID).IsUnique();

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
