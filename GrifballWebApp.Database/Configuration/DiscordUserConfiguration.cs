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

        entity.HasOne(x => x.QueuedPlayer)
            .WithOne(x => x.DiscordUser)
            .HasForeignKey<QueuedPlayer>(x => x.DiscordUserID)
            .IsRequired(false);

        entity.HasMany(x => x.MatchedPlayers)
            .WithOne(x => x.DiscordUser)
            .HasForeignKey(x => x.DiscordUserID);
    }
}
