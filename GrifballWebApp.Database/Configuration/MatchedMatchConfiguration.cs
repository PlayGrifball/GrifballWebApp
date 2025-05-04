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
    }
}
