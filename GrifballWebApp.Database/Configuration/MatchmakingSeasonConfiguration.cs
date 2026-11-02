using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class MatchmakingSeasonConfiguration : IEntityTypeConfiguration<MatchmakingSeason>
{
    public void Configure(EntityTypeBuilder<MatchmakingSeason> entity)
    {
        entity.ToTable("MatchmakingSeasons", "Matchmaking", tb => tb.IsTemporal());

        entity.HasKey(e => e.MatchmakingSeasonID);

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
