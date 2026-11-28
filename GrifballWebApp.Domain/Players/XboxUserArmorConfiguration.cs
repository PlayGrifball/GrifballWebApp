using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class XboxUserArmorConfiguration : IEntityTypeConfiguration<XboxUserArmor>
{
    public void Configure(EntityTypeBuilder<XboxUserArmor> entity)
    {
        // Bookkeeping of the armor job (like XboxUserImages): rewritten on every change, no history kept
        entity.ToTable("XboxUserArmor", "Xbox");

        entity.HasKey(e => e.XboxUserID);
        entity.Property(e => e.Error).HasMaxLength(200);
        entity.Property(e => e.CoreId).HasMaxLength(300);
        entity.Property(e => e.CorePath).HasMaxLength(300);
        entity.Property(e => e.ThemePath).HasMaxLength(300);
        entity.Property(e => e.CoatingPath).HasMaxLength(300);
        entity.Property(e => e.HelmetPath).HasMaxLength(300);
        entity.Property(e => e.VisorPath).HasMaxLength(300);
        entity.Property(e => e.GlovePath).HasMaxLength(300);
        entity.Property(e => e.HelmetAttachmentPath).HasMaxLength(300);
        entity.Property(e => e.ChestAttachmentPath).HasMaxLength(300);
        entity.Property(e => e.KneePadPath).HasMaxLength(300);
        entity.Property(e => e.LeftShoulderPadPath).HasMaxLength(300);
        entity.Property(e => e.RightShoulderPadPath).HasMaxLength(300);
        entity.Property(e => e.HipAttachmentPath).HasMaxLength(300);
        entity.Property(e => e.WristAttachmentPath).HasMaxLength(300);
        entity.Property(e => e.ArmorFxPath).HasMaxLength(300);
        entity.Property(e => e.MythicFxPath).HasMaxLength(300);
        entity.Property(e => e.ActionPosePath).HasMaxLength(300);
        entity.Property(e => e.StancePath).HasMaxLength(300);
        entity.Property(e => e.BodyType).HasMaxLength(300);
        entity.Property(e => e.LeftArm).HasMaxLength(300);
        entity.Property(e => e.RightArm).HasMaxLength(300);
        entity.Property(e => e.LeftLeg).HasMaxLength(300);
        entity.Property(e => e.RightLeg).HasMaxLength(300);
        entity.Property(e => e.WeaponCoatingsJson).HasMaxLength(4000);

        entity.HasOne(e => e.XboxUser)
            .WithMany()
            .HasForeignKey(e => e.XboxUserID)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasIndex(e => e.CheckedAt);
    }
}
