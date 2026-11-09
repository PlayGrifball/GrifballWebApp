using GrifballWebApp.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.League;
public partial class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> entity)
    {
        entity.ToTable("Seasons", "Event", tb =>
        {
            tb.IsTemporal();
            // Dates in an order that can happen (Season.Schedule): signups close no earlier than they open, the draft is no
            // later than the season's start, and the season ends no earlier than it starts. Signups may close after the
            // draft or the start (late signups), and equal dates are fine (an imported season's are all its start).
            tb.HasCheckConstraint("CK_Event_Seasons_DatesInOrder",
                "SignupsOpen <= SignupsClose AND DraftStart <= SeasonStart AND SeasonStart <= SeasonEnd");
            // A source link is the archive's: only an imported season (its key set) has one (Season.Imported, UpdateArchived)
            tb.HasCheckConstraint("CK_Event_Seasons_SourceUrlOnlyImported",
                "HistoricalSourceUrl IS NULL OR HistoricalSourceID IS NOT NULL");
        });

        entity.HasKey(e => e.SeasonID);
        // Worked out from its fields, not stored
        entity.Ignore(e => e.IsImported);
        entity.Ignore(e => e.ChampionTier);

        entity.Property(e => e.SeasonName).HasMaxLength(100).IsRequired();
        entity.Property(e => e.Division).HasMaxLength(50);
        entity.Property(e => e.HistoricalSourceID).HasMaxLength(100);
        entity.Property(e => e.HistoricalSourceUrl).HasMaxLength(500);
        // Existing seasons are league seasons: a default, not an UPDATE of the temporal table
        entity.Property(e => e.Format).HasDefaultValue(SeasonFormat.LeagueSeason);

        // Season names are unique within a league: two leagues can each have a "Winter League 2019".
        entity.HasIndex(e => new { e.LeagueID, e.SeasonName }).IsUnique();
        // Unique where set (the filter is the provider's own syntax, set in GrifballContext).
        entity.HasIndex(e => e.HistoricalSourceID).IsUnique();

        entity.HasOne(e => e.GameVersion)
            .WithMany(g => g.Seasons)
            .HasForeignKey(e => e.GameVersionID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        entity.HasOne(e => e.League)
            .WithMany(l => l.Seasons)
            .HasForeignKey(e => e.LeagueID)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(true);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<Season> entity);
}
