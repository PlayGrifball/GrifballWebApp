using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class DiscordMessageConfiguration : IEntityTypeConfiguration<DiscordMessage>
{
    public void Configure(EntityTypeBuilder<DiscordMessage> builder)
    {
        builder.ToTable("Messages", "Discord", tb => tb.IsTemporal());

        builder.HasKey(e => e.Id);

        // Value always comes from discord
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.HasOne(d => d.FromDiscordUser)
            .WithMany(p => p.SentMessages)
            .HasForeignKey(d => d.FromDiscordUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.ToDiscordUser)
            .WithMany(p => p.ReceivedMessages)
            .HasForeignKey(d => d.ToDiscordUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
