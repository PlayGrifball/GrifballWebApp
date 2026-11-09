using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.LateLeague;
public class MatchedKickVoteConfiguration : IEntityTypeConfiguration<MatchedKickVote>
{
    public void Configure(EntityTypeBuilder<MatchedKickVote> builder)
    {
        builder.ToTable("MatchedKickVotes", "Matchmaking", tb => tb.IsTemporal(true));

        // A vote of one player against another: a player may vote to kick several
        builder.HasKey(t => new { t.MatchId, t.VoterMatchedPlayerId, t.KickMatchedPlayerId });

        builder.HasOne(x => x.MatchedMatch)
            .WithMany(x => x.MatchedKickVotes)
            .HasForeignKey(x => x.MatchId);

        builder.HasOne(x => x.VoterMatchedPlayer)
            .WithMany(x => x.VoterMatchedKickVotes)
            .HasForeignKey(x => x.VoterMatchedPlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.KickMatchedPlayer)
            .WithMany(x => x.KickMatchedKickVotes)
            .HasForeignKey(x => x.KickMatchedPlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
