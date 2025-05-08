using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class MatchedKickVoteConfiguration : IEntityTypeConfiguration<MatchedKickVote>
{
    public void Configure(EntityTypeBuilder<MatchedKickVote> builder)
    {
        builder.ToTable("MatchedKickVotes", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(t => new { t.MatchId, t.VoterMatchedPlayerId });

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
