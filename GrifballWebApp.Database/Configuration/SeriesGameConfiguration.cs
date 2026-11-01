using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class SeriesGameConfiguration : IEntityTypeConfiguration<SeriesGame>
{
    public void Configure(EntityTypeBuilder<SeriesGame> entity)
    {
        entity.ToTable("SeriesGames", "Event", tb =>
        {
            tb.IsTemporal();
            // A series game is one game: a Waypoint match or a recorded game, never both or neither
            tb.HasCheckConstraint("CK_Event_SeriesGames_OneGame",
                "(MatchID IS NOT NULL AND RecordedGameID IS NULL) OR (MatchID IS NULL AND RecordedGameID IS NOT NULL)");
        });

        entity.HasKey(e => e.SeriesGameID);

        entity.HasOne(d => d.SeasonMatch)
            .WithMany(p => p.SeriesGames)
            .HasForeignKey(d => d.SeasonMatchID);

        entity.HasOne(d => d.Match)
            .WithOne(p => p.SeriesGame)
            .HasForeignKey<SeriesGame>(d => d.MatchID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        entity.HasOne(d => d.RecordedGame)
            .WithOne(p => p.SeriesGame)
            .HasForeignKey<SeriesGame>(d => d.RecordedGameID)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        // A game is in one series at most (unique where set: the filters are the provider's own syntax, set in GrifballContext)
        entity.HasIndex(d => d.MatchID).IsUnique();
        entity.HasIndex(d => d.RecordedGameID).IsUnique();

        // A series has one game 1, one game 2, ...
        entity.HasIndex(d => new { d.SeasonMatchID, d.GameNumber }).IsUnique();
    }
}
