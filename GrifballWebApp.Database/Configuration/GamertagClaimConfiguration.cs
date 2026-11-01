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
        entity.Property(e => e.CurrentGamertag).HasMaxLength(50);
        entity.Property(e => e.RejectionReason).HasMaxLength(200);

        entity.HasOne(e => e.XboxUser)
            .WithMany()
            .HasForeignKey(e => e.XboxUserID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // No cascade: the placeholder's foreign key to Users already sets null (SQL Server refuses two paths). A merge
        // moves the claimant's claims to the user it is merged into; deleting a user removes theirs first.
        entity.HasOne(e => e.ClaimantUser)
            .WithMany()
            .HasForeignKey(e => e.ClaimantUserID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);

        // The claim outlives its placeholder (merged on approval) as a record of the decision
        entity.HasOne(e => e.PlaceholderUser)
            .WithMany()
            .HasForeignKey(e => e.PlaceholderUserID)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        // No cascade: the season already cascades to its claims (SQL Server refuses two paths); the import clears the
        // team of claims before it deletes a team
        entity.HasOne(e => e.Team)
            .WithMany()
            .HasForeignKey(e => e.TeamID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);

        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // One pending or approved suggestion per kind, account and gamertag (a filtered index, in GrifballContext: each
        // database writes the filter in its own quoting)
        entity.HasIndex(e => e.Status);
    }
}
