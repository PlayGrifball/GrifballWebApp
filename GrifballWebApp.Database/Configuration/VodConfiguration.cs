using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class VodConfiguration : IEntityTypeConfiguration<Vod>
{
    private const int Auto = (int)VodMatchStatus.Auto;
    private const int Confirmed = (int)VodMatchStatus.Confirmed;
    private const int NeedsReview = (int)VodMatchStatus.NeedsReview;
    private const int Dismissed = (int)VodMatchStatus.Dismissed;

    /// <summary>A status the model knows.</summary>
    public static readonly string KnownStatus = $"MatchStatus IN ({Auto}, {Confirmed}, {NeedsReview}, {Dismissed})";

    /// <summary>A linked VOD (by the matcher or a sysadmin) is of a season.</summary>
    public static readonly string LinkedHasSeason = $"MatchStatus NOT IN ({Auto}, {Confirmed}) OR SeasonID IS NOT NULL";

    /// <summary>A VOD of a match is of a season (the match's: the model's callers see to that).</summary>
    public const string MatchHasSeason = "SeasonMatchID IS NULL OR SeasonID IS NOT NULL";

    public void Configure(EntityTypeBuilder<Vod> entity)
    {
        entity.ToTable("Vods", "Event", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Event_Vods_KnownStatus", KnownStatus);
            tb.HasCheckConstraint("CK_Event_Vods_LinkedHasSeason", LinkedHasSeason);
            tb.HasCheckConstraint("CK_Event_Vods_MatchHasSeason", MatchHasSeason);
        });

        entity.HasKey(e => e.VodID);

        // Worked out from its status, not stored
        entity.Ignore(e => e.IsVisible);
        entity.Ignore(e => e.IsUndecided);
        entity.Ignore(e => e.IsComplete);

        entity.Property(e => e.YouTubeVideoID).HasMaxLength(Vod.YouTubeVideoIDMaxLength).IsRequired();
        entity.HasIndex(e => e.YouTubeVideoID).IsUnique();
        entity.Property(e => e.Title).HasMaxLength(Vod.TitleMaxLength).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(Vod.DescriptionMaxLength);
        entity.Property(e => e.ChannelID).HasMaxLength(Vod.ChannelIDMaxLength).IsRequired();
        entity.Property(e => e.MatchNote).HasMaxLength(Vod.MatchNoteMaxLength);

        // No cascade from the season: SQL Server allows one cascade path to VodTeams (Season -> Teams -> VodTeams), and
        // seasons aren't deleted
        entity.HasOne(e => e.Season)
            .WithMany()
            .HasForeignKey(e => e.SeasonID)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // Schedules and brackets are regenerated with ExecuteDelete: the VOD keeps its season and teams
        entity.HasOne(e => e.SeasonMatch)
            .WithMany()
            .HasForeignKey(e => e.SeasonMatchID)
            .OnDelete(DeleteBehavior.SetNull);

        entity.HasIndex(e => e.MatchStatus);
    }
}
