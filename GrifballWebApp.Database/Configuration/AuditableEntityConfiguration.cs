using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public abstract class AuditableEntityConfiguration<T> where T : AuditableEntity
{
    public static void ConfigureAuditFields(EntityTypeBuilder<T> entity)
    {
        // Configure audit timestamp columns
        entity.Property(e => e.CreatedAt)
            .IsRequired();

        entity.Property(e => e.ModifiedAt)
            .IsRequired();

        // Configure optional foreign keys to User for audit tracking
        entity.Property(e => e.CreatedByID);
        entity.Property(e => e.ModifiedByID);

        // Configure relationships to User entity for audit tracking
        entity.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedByID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasOne(e => e.ModifiedBy)
            .WithMany()
            .HasForeignKey(e => e.ModifiedByID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}