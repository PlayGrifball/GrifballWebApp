using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class RecordedGameConfiguration : IEntityTypeConfiguration<RecordedGame>
{
    private const int HistoricalImport = (int)RecordedGameOrigin.HistoricalImport;
    private const int Manual = (int)RecordedGameOrigin.Manual;

    /// <summary>An origin the model knows.</summary>
    public static readonly string KnownOrigin = $"Origin IN ({HistoricalImport}, {Manual})";

    /// <summary>A game entered by hand counts for its season through its series game only, never a season of its own.</summary>
    public static readonly string ManualHasNoSeason = $"Origin <> {Manual} OR SeasonID IS NULL";

    public void Configure(EntityTypeBuilder<RecordedGame> entity)
    {
        entity.ToTable("Games", "Recorded", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Recorded_Games_KnownOrigin", KnownOrigin);
            tb.HasCheckConstraint("CK_Recorded_Games_ManualHasNoSeason", ManualHasNoSeason);
        });

        entity.HasKey(e => e.RecordedGameID);

        // Worked out from its origin, not stored
        entity.Ignore(e => e.IsManual);

        entity.Property(e => e.SourceKey).HasMaxLength(RecordedGame.SourceKeyMaxLength).IsRequired();
        entity.Property(e => e.Map).HasMaxLength(RecordedGame.MapMaxLength);
        entity.Property(e => e.Gametype).HasMaxLength(RecordedGame.GametypeMaxLength);
        // Every game before manual match entry was imported
        entity.Property(e => e.Origin).HasDefaultValue(RecordedGameOrigin.HistoricalImport);

        entity.HasIndex(e => e.SourceKey).IsUnique();
        entity.HasIndex(e => e.SeasonID);

        entity.HasOne(e => e.GameVersion)
            .WithMany()
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict);

        // No cascade: the season already cascades to its series games through its season matches (SQL Server refuses two
        // paths). Deleting a season with recorded games is refused until they are removed.
        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .IsRequired(false);
    }
}
