using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class MatchedPlayerConfiguration : IEntityTypeConfiguration<MatchedPlayer>
{
    public void Configure(EntityTypeBuilder<MatchedPlayer> builder)
    {
        builder.ToTable("MatchedPlayers", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(x => x.DiscordUserID);

        // Value always comes from discord
        builder.Property(e => e.DiscordUserID).ValueGeneratedNever();

        builder.HasOne(x => x.MatchedTeam)
            .WithMany(x => x.Players)
            .HasForeignKey(x => x.MatchedTeamID);
    }
}
