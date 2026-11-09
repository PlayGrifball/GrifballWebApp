using GrifballWebApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Configuration;
public class MatchedTeamConfiguration : IEntityTypeConfiguration<MatchedTeam>
{
    void IEntityTypeConfiguration<MatchedTeam>.Configure(EntityTypeBuilder<MatchedTeam> builder)
    {
        builder.ToTable("MatchedTeams", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(x => x.MatchedTeamId);

        // By the names the team gives: its links back to the match are private
        builder.HasOne<MatchedMatch>(MatchedTeam.HomeMatchedMatchNavigation)
            .WithOne(x => x.HomeTeam)
            .HasForeignKey<MatchedMatch>(x => x.HomeTeamId)
            .IsRequired(false);

        builder.HasOne<MatchedMatch>(MatchedTeam.AwayMatchedMatchNavigation)
            .WithOne(x => x.AwayTeam)
            .HasForeignKey<MatchedMatch>(x => x.AwayTeamId)
            .IsRequired(false);
    }
}
