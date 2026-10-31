using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

/// <summary>
/// A player's other Xbox accounts (different XUIDs) only, never renames of one account: those are the XboxUser's.
/// </summary>
public class PlayerAliasConfiguration : IEntityTypeConfiguration<PlayerAlias>
{
    public void Configure(EntityTypeBuilder<PlayerAlias> entity)
    {
        entity.ToTable("PlayerAliases", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.PlayerAliasID);

        entity.Property(e => e.Alias).HasMaxLength(50).IsRequired();
        entity.Property(e => e.NormalizedAlias).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Source).HasMaxLength(50);

        // Restrict: deleting a user with aliases is refused; a merge moves them first.
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasIndex(e => new { e.UserID, e.NormalizedAlias }).IsUnique();
        entity.HasIndex(e => e.NormalizedAlias);
    }
}
