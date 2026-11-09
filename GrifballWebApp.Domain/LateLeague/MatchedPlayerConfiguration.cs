using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.LateLeague;
public class MatchedPlayerConfiguration : IEntityTypeConfiguration<MatchedPlayer>
{
    public void Configure(EntityTypeBuilder<MatchedPlayer> builder)
    {
        builder.ToTable("MatchedPlayers", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(x => x.Id);

        // Value always comes from User table
        builder.Property(e => e.UserID).ValueGeneratedNever();

        builder.HasOne(x => x.MatchedTeam)
            .WithMany(x => x.Players)
            .HasForeignKey(x => x.MatchedTeamID);
    }
}
