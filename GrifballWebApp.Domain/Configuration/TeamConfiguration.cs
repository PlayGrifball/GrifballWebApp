using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;
public partial class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> entity)
    {
        entity.ToTable("Teams", "Event", tb =>
        {
            tb.IsTemporal();
            // A record of the spreadsheet's is both halves of it, or none (Team.RecordArchive, ClearArchivedRecord)
            tb.HasCheckConstraint("CK_Event_Teams_RecordBothOrNeither",
                "(RecordedWins IS NULL AND RecordedLosses IS NULL) OR (RecordedWins IS NOT NULL AND RecordedLosses IS NOT NULL)");
        });

        entity.HasKey(e => e.TeamID);

        entity.HasOne(d => d.Season)
            .WithMany(p => p.Teams)
            .HasForeignKey(d => d.SeasonID);

        entity.HasOne(d => d.Captain)
            .WithOne(p => p.CaptainTeam)
            .HasForeignKey<Team>(d => d.CaptainID)
            .OnDelete(DeleteBehavior.NoAction);

        entity.Property(e => e.TeamName).HasMaxLength(Team.TeamNameColumnLength);

        // Team name must be unique for any given season
        entity.HasIndex(e => new { e.TeamName, e.SeasonID }).IsUnique();

        // Worked out from its roster, not a column
        entity.Ignore(e => e.Picks);
        entity.Ignore(e => e.IsRecordArchived);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Team> entity);
}
