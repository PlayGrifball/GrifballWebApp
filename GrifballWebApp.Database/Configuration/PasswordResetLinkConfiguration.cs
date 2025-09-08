using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class PasswordResetLinkConfiguration : IEntityTypeConfiguration<PasswordResetLink>
{
    public void Configure(EntityTypeBuilder<PasswordResetLink> entity)
    {
        entity.ToTable("PasswordResetLinks", "Auth", tb => tb.IsTemporal());

        entity.HasKey(e => e.Id);

        entity.Property(e => e.Token)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(e => e.ExpiresAt)
            .IsRequired();

        entity.Property(e => e.IsUsed)
            .IsRequired()
            .HasDefaultValue(false);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Add index on Token for faster lookups
        entity.HasIndex(e => e.Token)
            .IsUnique();

        // Add index on ExpiresAt for cleanup operations
        entity.HasIndex(e => e.ExpiresAt);
    }
}