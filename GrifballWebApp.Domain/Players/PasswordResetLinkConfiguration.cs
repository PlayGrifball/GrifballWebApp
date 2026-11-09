using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class PasswordResetLinkConfiguration : IEntityTypeConfiguration<PasswordResetLink>
{
    /// <summary>A link is used once at most (one time, kept) and only before it expires.</summary>
    public const string UsedBeforeExpiry = "UsedAt IS NULL OR UsedAt < ExpiresAt";

    public void Configure(EntityTypeBuilder<PasswordResetLink> entity)
    {
        entity.ToTable("PasswordResetLinks", "Auth", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Auth_PasswordResetLinks_UsedBeforeExpiry", UsedBeforeExpiry);
        });

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(PasswordResetLink.TokenMaxLength);

        entity.Property(e => e.ExpiresAt)
            .IsRequired();

        // A use saves only while the link is still unused (WHERE UsedAt IS NULL): two requests racing on one link can't
        // both use it
        entity.Property(e => e.UsedAt)
            .IsConcurrencyToken();

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Token is the token's hash (PasswordResetLink.HashToken), looked up by it
        entity.HasIndex(e => e.Token)
            .IsUnique();

        // Add index on ExpiresAt for cleanup operations
        entity.HasIndex(e => e.ExpiresAt);
    }
}
