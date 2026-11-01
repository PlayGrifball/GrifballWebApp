using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class VodConfiguration : IEntityTypeConfiguration<Vod>
{
    public void Configure(EntityTypeBuilder<Vod> entity)
    {
        entity.ToTable("Vods", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.VodID);

        entity.Property(e => e.YouTubeVideoID).HasMaxLength(20).IsRequired();
        entity.HasIndex(e => e.YouTubeVideoID).IsUnique();
        entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Description).HasMaxLength(5000);
        entity.Property(e => e.ChannelID).HasMaxLength(40).IsRequired();
        entity.Property(e => e.MatchNote).HasMaxLength(500);

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
