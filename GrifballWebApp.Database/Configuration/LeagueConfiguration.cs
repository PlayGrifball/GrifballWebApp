using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

// Seeds the leagues through their seed factory (League.Seed)
[AggregateAccess(typeof(League))]
public class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public const string NameAndSlugNotBlank = "LTRIM(RTRIM(LeagueName)) <> '' AND LTRIM(RTRIM(Slug)) <> ''";

    public void Configure(EntityTypeBuilder<League> entity)
    {
        entity.ToTable("Leagues", "Event", tb =>
        {
            tb.IsTemporal();
            tb.HasCheckConstraint("CK_Event_Leagues_NameAndSlugNotBlank", NameAndSlugNotBlank);
        });

        entity.HasKey(e => e.LeagueID);

        entity.Property(e => e.LeagueName).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Slug).HasMaxLength(50).IsRequired();

        entity.HasIndex(e => e.LeagueName).IsUnique();
        entity.HasIndex(e => e.Slug).IsUnique();

        League[] leagues =
        {
            League.Seed("AGLA", "agla", leagueID: 1),
            League.Seed("GWL", "gwl", leagueID: 3),
            League.Seed("IGL", "igl", leagueID: 4),
            League.Seed("Community/Side", "side", leagueID: 5),
        };

        entity.HasData(leagues);
    }
}
