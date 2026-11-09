using GrifballWebApp.Domain.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Archive;

public class RecordedGameParticipantConfiguration : IEntityTypeConfiguration<RecordedGameParticipant>
{
    public void Configure(EntityTypeBuilder<RecordedGameParticipant> entity)
    {
        entity.ToTable("GameParticipants", "Recorded", tb => tb.IsTemporal());

        entity.HasKey(e => e.RecordedGameParticipantID);

        // No length: the same type as Xbox.XboxUsers.Gamertag, which it is UNION ALLed with in the stats (PlayerGameStatsQuery)
        entity.Property(e => e.Gamertag).IsRequired();

        entity.HasOne(e => e.RecordedGame)
            .WithMany(g => g.Participants)
            .HasForeignKey(e => e.RecordedGameID)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade: the game already cascades to its participants (SQL Server refuses two paths)
        entity.HasOne(e => e.RecordedGameTeam)
            .WithMany(t => t.Participants)
            .HasForeignKey(e => e.RecordedGameTeamID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);

        // Restrict: deleting a user with games is refused; a merge moves them first.
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        entity.HasIndex(e => e.UserID);

        // A player has one line in a game (RecordedGame.SetPlayers, RecordedGame.ReassignPlayer)
        entity.HasIndex(e => new { e.RecordedGameID, e.UserID }).IsUnique();
    }
}
