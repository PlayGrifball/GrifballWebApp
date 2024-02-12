using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class XboxUserConfiguration : IEntityTypeConfiguration<XboxUser>
{
    public void Configure(EntityTypeBuilder<XboxUser> entity)
    {
        entity.ToTable("XboxUsers", "Xbox", tb => tb.IsTemporal());

        entity.HasKey(e => e.XUID);

        // Value always comes from 343
        entity.Property(e => e.XUID).ValueGeneratedNever();

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<XboxUser> entity);
}
