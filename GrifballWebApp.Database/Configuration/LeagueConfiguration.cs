using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class LeagueConfiguration : IEntityTypeConfiguration<League>
{
    public void Configure(EntityTypeBuilder<League> entity)
    {
        entity.ToTable("Leagues", "Event", tb => tb.IsTemporal());

        entity.HasKey(e => e.LeagueID);

        entity.Property(e => e.LeagueName).HasMaxLength(50).IsRequired();
        entity.Property(e => e.Slug).HasMaxLength(50).IsRequired();

        entity.HasIndex(e => e.LeagueName).IsUnique();
        entity.HasIndex(e => e.Slug).IsUnique();

        League[] leagues =
        {
            new() { LeagueID = 1, LeagueName = "AGLA", Slug = "agla" },
            new() { LeagueID = 2, LeagueName = "Late League", Slug = "late-league" },
            new() { LeagueID = 3, LeagueName = "GWL", Slug = "gwl" },
            new() { LeagueID = 4, LeagueName = "IGL", Slug = "igl" },
            new() { LeagueID = 5, LeagueName = "Community/Side", Slug = "side" },
        };

        entity.HasData(leagues);
    }
}
