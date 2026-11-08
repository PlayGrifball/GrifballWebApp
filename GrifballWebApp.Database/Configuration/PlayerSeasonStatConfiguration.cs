using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class PlayerSeasonStatConfiguration : IEntityTypeConfiguration<PlayerSeasonStat>
{
    public void Configure(EntityTypeBuilder<PlayerSeasonStat> entity)
    {
        entity.ToTable("PlayerSeasonStats", "Event", tb =>
        {
            tb.IsTemporal();
            // A stint is counted from 1 (PlayerSeasonStat.FromSheet)
            tb.HasCheckConstraint("CK_Event_PlayerSeasonStats_StintFromOne", "StintIndex >= 1");
        });

        entity.HasKey(e => e.PlayerSeasonStatID);
        ConfigureTotals(entity);

        entity.Property(e => e.TradeNote).HasMaxLength(100);
        entity.Property(e => e.RoleNote).HasMaxLength(50);

        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade: the season already cascades to its stats (SQL Server refuses two paths). Deleting a team clears
        // the link on the stats it has loaded; the database refuses to orphan others.
        entity.HasOne(e => e.Team)
            .WithMany(t => t.PlayerSeasonStats)
            .HasForeignKey(e => e.TeamID)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // One row per player, stint and phase of a season: what the import upserts by
        entity.HasIndex(e => new { e.SeasonID, e.UserID, e.StintIndex, e.IsPlayoffs }).IsUnique();
        entity.HasIndex(e => e.UserID);
        entity.HasIndex(e => e.TeamID);
        entity.HasIndex(e => e.Gamertag);
    }

    /// <summary>The columns every stored-totals table shares (<see cref="PlayerStatTotals"/>), configured one way.</summary>
    public static void ConfigureTotals<T>(EntityTypeBuilder<T> entity) where T : PlayerStatTotals
    {
        // Computed from the columns
        entity.Ignore(e => e.Totals);

        entity.Property(e => e.Gamertag).HasMaxLength(50).IsRequired();

        entity.Property(e => e.WinPercent).HasColumnType("decimal(6, 3)");
        entity.Property(e => e.KdRatio).HasColumnType("decimal(6, 3)");
        entity.Property(e => e.AverageLifeSeconds).HasColumnType("decimal(6, 2)");

        // Restrict: deleting a user with stats is refused; a merge moves them first.
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
