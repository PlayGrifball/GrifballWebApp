using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class DiscordUserConfiguration : IEntityTypeConfiguration<DiscordUser>
{
    public void Configure(EntityTypeBuilder<DiscordUser> entity)
    {
        entity.ToTable("Discord", "User", tb => tb.IsTemporal());

        entity.HasKey(e => e.DiscordUserID);

        // Value always comes from discord
        entity.Property(e => e.DiscordUserID).ValueGeneratedNever();

        entity.HasOne(d => d.XboxUser)
            .WithMany()
            .HasForeignKey(d => d.XboxUserID);
    }
}
