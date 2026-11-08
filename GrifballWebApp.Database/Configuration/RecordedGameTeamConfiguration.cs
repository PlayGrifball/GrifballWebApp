using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class RecordedGameTeamConfiguration : IEntityTypeConfiguration<RecordedGameTeam>
{
    /// <summary>A result the model knows (a source's missing result is <see cref="Outcomes.DidNotFinish"/>).</summary>
    public static readonly string KnownOutcome =
        $"Outcome IN ({(int)Outcomes.Tie}, {(int)Outcomes.Won}, {(int)Outcomes.Lost}, {(int)Outcomes.DidNotFinish})";

    public void Configure(EntityTypeBuilder<RecordedGameTeam> entity)
    {
        entity.ToTable("GameTeams", "Recorded", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Recorded_GameTeams_KnownOutcome", KnownOutcome);
        });

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
