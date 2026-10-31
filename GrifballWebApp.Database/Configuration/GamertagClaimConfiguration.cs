using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class GamertagClaimConfiguration : IEntityTypeConfiguration<GamertagClaim>
{
    public void Configure(EntityTypeBuilder<GamertagClaim> entity)
    {
        entity.ToTable("GamertagClaims", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.GamertagClaimID);

        entity.Property(e => e.Gamertag).HasMaxLength(50).IsRequired();
        entity.Property(e => e.NormalizedGamertag).HasMaxLength(50).IsRequired();
        entity.Property(e => e.TeamName).HasMaxLength(100);

        entity.HasOne(e => e.XboxUser)
            .WithMany()
            .HasForeignKey(e => e.XboxUserID)
            .OnDelete(DeleteBehavior.Cascade);

        // The claim outlives its placeholder (merged on approval) as a record of the decision
        entity.HasOne(e => e.PlaceholderUser)
            .WithMany()
            .HasForeignKey(e => e.PlaceholderUserID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // One suggestion per kind, account and gamertag: a re-run of the import doesn't repeat one, rejected or not
        entity.HasIndex(e => new { e.Kind, e.XboxUserID, e.NormalizedGamertag }).IsUnique();
        entity.HasIndex(e => e.Status);
    }
}
