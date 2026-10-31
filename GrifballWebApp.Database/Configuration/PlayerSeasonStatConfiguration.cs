using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class PlayerSeasonStatConfiguration : IEntityTypeConfiguration<PlayerSeasonStat>
{
    public void Configure(EntityTypeBuilder<PlayerSeasonStat> entity)
    {
        entity.ToTable("PlayerSeasonStats", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.PlayerSeasonStatID);

        entity.Property(e => e.Gamertag).HasMaxLength(50).IsRequired();
        entity.Property(e => e.TeamName).HasMaxLength(100);
        entity.Property(e => e.Division).HasMaxLength(50);
        entity.Property(e => e.TradeNote).HasMaxLength(100);
        entity.Property(e => e.RoleNote).HasMaxLength(50);

        entity.Property(e => e.WinPercent).HasColumnType("decimal(6, 3)");
        entity.Property(e => e.KdRatio).HasColumnType("decimal(6, 3)");
        entity.Property(e => e.AverageLifeSeconds).HasColumnType("decimal(6, 2)");

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => new { e.UserID, e.SeasonID, e.StintIndex, e.IsPlayoffs });
        entity.HasIndex(e => e.Gamertag);
    }
}
