using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class MatchedMatchConfiguration : IEntityTypeConfiguration<MatchedMatch>
{
    public void Configure(EntityTypeBuilder<MatchedMatch> builder)
    {
        builder.ToTable("MatchedMatches", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(x => x.Id);

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
