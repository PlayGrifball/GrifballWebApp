using GrifballWebApp.Domain.League;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.LateLeague;

public class MatchmakingPlayerStatConfiguration : IEntityTypeConfiguration<MatchmakingPlayerStat>
{
    public void Configure(EntityTypeBuilder<MatchmakingPlayerStat> entity)
    {
        entity.ToTable("PlayerStats", "Matchmaking", tb => tb.IsTemporal());

        entity.HasKey(e => e.MatchmakingPlayerStatID);
        PlayerSeasonStatConfiguration.ConfigureTotals(entity);

        entity.HasOne(e => e.MatchmakingSeason)
            .WithMany(s => s.PlayerStats)
            .HasForeignKey(e => e.MatchmakingSeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per player of a matchmaking season: what the import upserts by
        entity.HasIndex(e => new { e.MatchmakingSeasonID, e.UserID }).IsUnique();
        entity.HasIndex(e => e.UserID);
    }
}
