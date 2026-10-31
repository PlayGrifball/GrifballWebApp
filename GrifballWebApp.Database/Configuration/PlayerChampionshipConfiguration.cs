using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class PlayerChampionshipConfiguration : IEntityTypeConfiguration<PlayerChampionship>
{
    public void Configure(EntityTypeBuilder<PlayerChampionship> entity)
    {
        entity.ToTable("PlayerChampionships", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.PlayerChampionshipID);

        entity.Property(e => e.SeasonName).HasMaxLength(100).IsRequired();
        entity.Property(e => e.LeagueName).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Tier).HasMaxLength(50).IsRequired();
        entity.Property(e => e.TeamName).HasMaxLength(100).IsRequired();
        entity.Property(e => e.GamertagUsed).HasMaxLength(50).IsRequired();

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.UserID);
        entity.HasIndex(e => new { e.SeasonName, e.TeamName });
    }
}
