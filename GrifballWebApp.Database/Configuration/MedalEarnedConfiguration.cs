using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MedalEarnedConfiguration : IEntityTypeConfiguration<MedalEarned>
{
    public void Configure(EntityTypeBuilder<MedalEarned> entity)
    {
        entity.ToTable("MedalEarned", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.MedalID, e.MatchID, e.XboxUserID });

        entity.HasOne(d => d.Medal)
            .WithMany(p => p.MedalEarned)
            .HasForeignKey(d => d.MedalID);

        entity.HasOne(d => d.MatchParticipant)
            .WithMany(p => p.MedalEarned)
            .HasForeignKey(d => new { d.MatchID, d.XboxUserID });

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MedalEarned> entity);
}
