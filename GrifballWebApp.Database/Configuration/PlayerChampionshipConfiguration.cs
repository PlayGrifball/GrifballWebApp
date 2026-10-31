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

        entity.Property(e => e.Tier).HasMaxLength(50).IsRequired();
        entity.Property(e => e.GamertagUsed).HasMaxLength(50).IsRequired();

        // Restrict: deleting a user with championships is refused; a merge moves them first.
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade: the season already cascades to its titles (SQL Server refuses two paths). NO ACTION, checked at the
        // end of the statement, so deleting a season takes its teams and titles together; a team alone goes only once
        // ChampionshipService has taken its title away.
        entity.HasOne(e => e.Team)
            .WithMany(t => t.PlayerChampionships)
            .HasForeignKey(e => e.TeamID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // What the import upserts by: a player holds one title per season and tier.
        entity.HasIndex(e => new { e.SeasonID, e.Tier, e.UserID }).IsUnique();
        entity.HasIndex(e => e.UserID);
        entity.HasIndex(e => e.TeamID);
        entity.HasIndex(e => e.GamertagUsed);
    }
}
