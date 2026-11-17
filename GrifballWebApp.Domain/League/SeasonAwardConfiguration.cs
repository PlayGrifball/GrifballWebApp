using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.League;

public class SeasonAwardConfiguration : IEntityTypeConfiguration<SeasonAward>
{
    public void Configure(EntityTypeBuilder<SeasonAward> entity)
    {
        entity.ToTable("SeasonAwards", "Event", tb =>
        {
            tb.IsTemporal();
            // A player's or a team's: never no one's
            tb.HasCheckConstraint("CK_Event_SeasonAwards_PlayerOrTeam", "UserID IS NOT NULL OR TeamID IS NOT NULL");
        });

        entity.HasKey(e => e.SeasonAwardID);

        entity.Property(e => e.Name).HasMaxLength(SeasonAward.NameMaxLength).IsRequired();
        entity.Property(e => e.WinnerName).HasMaxLength(SeasonAward.WinnerNameMaxLength).IsRequired();
        entity.Property(e => e.Value).HasMaxLength(SeasonAward.ValueMaxLength);

        // A part of its season: made and changed only through it (Season.RecordArchivedAwards)
        entity.HasOne(e => e.Season)
            .WithMany(s => s.Awards)
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: deleting a user with awards is refused; a merge moves them first (Season.MergeAwards).
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // No cascade: the season already cascades to its awards (SQL Server refuses two paths). The import takes a
        // team's awards away before the team goes (Season.RemoveArchivedAwardsOf).
        entity.HasOne(e => e.Team)
            .WithMany()
            .HasForeignKey(e => e.TeamID)
            .OnDelete(DeleteBehavior.NoAction)
            .IsRequired(false);

        // What the import upserts by: a winner holds an award of a season once.
        entity.HasIndex(e => new { e.SeasonID, e.Name, e.WinnerName }).IsUnique();
        entity.HasIndex(e => e.UserID);
        entity.HasIndex(e => e.TeamID);
    }
}
