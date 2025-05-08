using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class MatchedWinnerVoteConfiguration : IEntityTypeConfiguration<MatchedWinnerVote>
{
    public void Configure(EntityTypeBuilder<MatchedWinnerVote> builder)
    {
        builder.ToTable("MatchedWinnerVotes", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(t => new { t.MatchId, t.MatchedPlayerId });

        builder.HasOne(x => x.MatchedMatch)
            .WithMany(x => x.MatchedWinnerVotes)
            .HasForeignKey(x => x.MatchId);

        builder.HasOne(x => x.MatchedPlayer)
            .WithMany(x => x.MatchedWinnerVotes)
            .HasForeignKey(x => x.MatchedPlayerId);
    }
}
