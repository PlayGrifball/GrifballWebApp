using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchPlayerConfiguration : IEntityTypeConfiguration<MatchParticipant>
{
    public void Configure(EntityTypeBuilder<MatchParticipant> entity)
    {
        entity.ToTable("MatchParticipants", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.MatchID, e.XboxUserID });

        entity.HasOne(d => d.Match)
            .WithMany(p => p.MatchParticipants)
            .HasForeignKey(d => d.MatchID);

        entity.HasOne(d => d.XboxUser)
            .WithMany(p => p.MatchParticipants)
            .HasForeignKey(d => d.XboxUserID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MatchParticipant> entity);
}
