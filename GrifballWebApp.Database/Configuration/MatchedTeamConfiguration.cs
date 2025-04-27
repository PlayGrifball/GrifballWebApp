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

        builder.HasOne(x => x.HomeMatchedMatch)
            .WithOne(x => x.HomeTeam)
            .HasForeignKey<MatchedMatch>(x => x.Id)
            .IsRequired(false);

        builder.HasOne(x => x.AwayMatchedMatch)
            .WithOne(x => x.AwayTeam)
            .HasForeignKey<MatchedMatch>(x => x.Id)
            .IsRequired(false);
    }
}
