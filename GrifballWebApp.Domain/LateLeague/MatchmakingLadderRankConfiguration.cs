using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.LateLeague;

public class MatchmakingLadderRankConfiguration : IEntityTypeConfiguration<MatchmakingLadderRank>
{
    public void Configure(EntityTypeBuilder<MatchmakingLadderRank> entity)
    {
        entity.ToTable("LadderRanks", "Matchmaking", tb =>
        {
            tb.IsTemporal();
            // A place on the ladder starts at 1 (MatchmakingLadderRank.Record)
            tb.HasCheckConstraint("CK_Matchmaking_LadderRanks_PositionFromOne", "Position IS NULL OR Position >= 1");
            // Wins and losses are counts
            tb.HasCheckConstraint("CK_Matchmaking_LadderRanks_RecordNotNegative", "(Wins IS NULL OR Wins >= 0) AND (Losses IS NULL OR Losses >= 0)");
        });

        entity.HasKey(e => e.MatchmakingLadderRankID);
        // Computed from the columns
        entity.Ignore(e => e.Standing);

        entity.Property(e => e.Gamertag).HasMaxLength(MatchmakingLadderRank.GamertagMaxLength).IsRequired();
        entity.Property(e => e.League).HasMaxLength(MatchmakingLadderRank.LeagueMaxLength);
        entity.Property(e => e.Division).HasMaxLength(MatchmakingLadderRank.DivisionMaxLength);

        entity.HasOne(e => e.MatchmakingSeason)
            .WithMany(s => s.LadderRanks)
            .HasForeignKey(e => e.MatchmakingSeasonID)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: deleting a user with ladder rows is refused; a merge moves them first.
        entity.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserID)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per player of a matchmaking season: what the import upserts by
        entity.HasIndex(e => new { e.MatchmakingSeasonID, e.UserID }).IsUnique();
        entity.HasIndex(e => e.UserID);
    }
}
