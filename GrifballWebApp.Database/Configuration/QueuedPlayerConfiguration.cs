using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class QueuedPlayerConfiguration : IEntityTypeConfiguration<QueuedPlayer>
{
    public void Configure(EntityTypeBuilder<QueuedPlayer> builder)
    {
        builder.ToTable("QueuedPlayers", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(e => e.DiscordUserID);

        // Value always comes from discord
        builder.Property(e => e.DiscordUserID).ValueGeneratedNever();
    }
}
