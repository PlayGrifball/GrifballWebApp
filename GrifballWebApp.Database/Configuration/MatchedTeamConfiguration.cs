using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class MatchedTeamConfiguration : IEntityTypeConfiguration<MatchedTeam>
{
    void IEntityTypeConfiguration<MatchedTeam>.Configure(EntityTypeBuilder<MatchedTeam> builder)
    {
        builder.ToTable("MatchedTeams", "Matchmaking", tb => tb.IsTemporal(true));

        builder.HasKey(x => x.MatchedTeamId);

        // By name: the links back to the match are private to the team
        builder.HasOne<MatchedMatch>("HomeMatchedMatch")
            .WithOne(x => x.HomeTeam)
            .HasForeignKey<MatchedMatch>(x => x.HomeTeamId)
            .IsRequired(false);

        builder.HasOne<MatchedMatch>("AwayMatchedMatch")
            .WithOne(x => x.AwayTeam)
            .HasForeignKey<MatchedMatch>(x => x.AwayTeamId)
            .IsRequired(false);
    }
}
