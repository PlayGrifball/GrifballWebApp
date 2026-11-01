using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class RecordedGameTeamConfiguration : IEntityTypeConfiguration<RecordedGameTeam>
{
    public void Configure(EntityTypeBuilder<RecordedGameTeam> entity)
    {
        entity.ToTable("GameTeams", "Recorded", tb => tb.IsTemporal());

        entity.HasKey(e => e.RecordedGameTeamID);

        entity.Property(e => e.Name).HasMaxLength(Team.TeamNameColumnLength);

        entity.HasOne(e => e.RecordedGame)
            .WithMany(g => g.Teams)
            .HasForeignKey(e => e.RecordedGameID)
            .OnDelete(DeleteBehavior.Cascade);

        // No cascade: deleting a team clears the link on the sides it has loaded (the import does before removing a team)
        entity.HasOne(e => e.Team)
            .WithMany()
            .HasForeignKey(e => e.TeamID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);

        entity.HasIndex(e => new { e.RecordedGameID, e.Side }).IsUnique();
        entity.HasIndex(e => e.TeamID);
    }
}
