using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class QueuedPlayerConfiguration : IEntityTypeConfiguration<QueuedPlayer>
{
    public void Configure(EntityTypeBuilder<QueuedPlayer> builder)
    {
        builder.ToTable("QueuedPlayers", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(e => e.UserID);

        // Value always comes from User table
        builder.Property(e => e.UserID).ValueGeneratedNever();
    }
}
