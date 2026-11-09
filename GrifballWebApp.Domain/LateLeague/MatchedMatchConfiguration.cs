using GrifballWebApp.Domain.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.LateLeague;
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
            // A result is a Halo match (the queue's) or a recorded game (the archive's), never both; a reported match has one
            tb.HasCheckConstraint("CK_Matchmaking_MatchedMatches_OneKindOfResult",
                "MatchID IS NULL OR RecordedGameID IS NULL");
            tb.HasCheckConstraint("CK_Matchmaking_MatchedMatches_ReportedHasResult",
                "Status <> 2 OR MatchID IS NOT NULL OR RecordedGameID IS NOT NULL");
            // The vote message is posted in the match's thread (PostVoteMessage)
            tb.HasCheckConstraint("CK_Matchmaking_MatchedMatches_VoteMessageNeedsThread",
                "VoteMessageID IS NULL OR ThreadID IS NOT NULL");
        });

        builder.HasKey(x => x.Id);

        // Worked out from its teams and votes, not stored: without these, EF would take them for relationships of their own
        builder.Ignore(x => x.Players);
        builder.Ignore(x => x.CountedWinnerVotes);
        builder.Ignore(x => x.VotesNeeded);

        // A recorded game is one matchmaking game at most. Both are Restrict, as every foreign key into the aggregate: a
        // recorded game or season with a match is never deleted from under it, so the match's links change only through the
        // match itself (RemoveArchivedMatch discards the match before its recorded game goes)
        builder.HasOne(x => x.RecordedGame)
            .WithOne(g => g.MatchedMatch)
            .HasForeignKey<MatchedMatch>(x => x.RecordedGameID)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MatchmakingSeason)
            .WithMany(s => s.MatchedMatches)
            .HasForeignKey(x => x.MatchmakingSeasonID)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
