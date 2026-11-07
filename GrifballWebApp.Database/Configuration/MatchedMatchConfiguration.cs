using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class MatchedMatchConfiguration : IEntityTypeConfiguration<MatchedMatch>
{
    public void Configure(EntityTypeBuilder<MatchedMatch> builder)
    {
        builder.ToTable("MatchedMatches", "Matchmaking", tb =>
        {
            tb.IsTemporal(true);
            // A match being played has no result on record yet; only a vote-decided one has a voted winner
            tb.HasCheckConstraint("CK_Matchmaking_MatchedMatches_ActiveHasNoResult",
                "Status <> 1 OR (MatchID IS NULL AND RecordedGameID IS NULL)");
            tb.HasCheckConstraint("CK_Matchmaking_MatchedMatches_WinnerSideOnlyWhenVoteDecided",
                "(Status = 3 AND WinnerSide IS NOT NULL) OR (Status <> 3 AND WinnerSide IS NULL)");
        });

        builder.HasKey(x => x.Id);

        // Worked out from its teams and votes, not stored: without these, EF would take them for relationships of their own
        builder.Ignore(x => x.Players);
        builder.Ignore(x => x.CountedWinnerVotes);
        builder.Ignore(x => x.VotesNeeded);

        // A recorded game is one matchmaking game at most; deleting it takes its match's link, not the match
        builder.HasOne(x => x.RecordedGame)
            .WithOne(g => g.MatchedMatch)
            .HasForeignKey<MatchedMatch>(x => x.RecordedGameID)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(x => x.MatchmakingSeason)
            .WithMany(s => s.MatchedMatches)
            .HasForeignKey(x => x.MatchmakingSeasonID)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
