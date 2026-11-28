using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class UserSpartanConfiguration : IEntityTypeConfiguration<UserSpartan>
{
    public void Configure(EntityTypeBuilder<UserSpartan> entity)
    {
        entity.ToTable("UserSpartans", "Xbox", tb => tb.IsTemporal());

        entity.HasKey(e => e.UserId);
        entity.Property(e => e.ChoiceJson).HasMaxLength(UserSpartan.MaxChoiceLength);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserSpartanRenderConfiguration : IEntityTypeConfiguration<UserSpartanRender>
{
    public void Configure(EntityTypeBuilder<UserSpartanRender> entity)
    {
        // Bookkeeping of the render job (like XboxUserImages): rewritten on every render, whole files, no history kept
        entity.ToTable("UserSpartanRenders", "Xbox");

        entity.HasKey(e => e.UserId);
        entity.Property(e => e.LoadoutKey).HasMaxLength(100);
        entity.Property(e => e.Error).HasMaxLength(500);
        entity.Property(e => e.Weapon).HasMaxLength(64);

        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => new { e.State, e.RequestedAt });
    }
}

public class UserSpartanFileConfiguration : IEntityTypeConfiguration<UserSpartanFile>
{
    public void Configure(EntityTypeBuilder<UserSpartanFile> entity)
    {
        entity.ToTable("UserSpartanFiles", "Xbox");

        entity.HasKey(e => new { e.UserId, e.Name });
        entity.Property(e => e.Name).HasMaxLength(UserSpartanFile.MaxNameLength);
        entity.Property(e => e.ContentType).HasMaxLength(100);

        entity.HasOne(e => e.Render)
            .WithMany(r => r.Files)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
