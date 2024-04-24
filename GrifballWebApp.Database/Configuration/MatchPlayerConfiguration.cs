using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class MatchParticipantConfiguration : IEntityTypeConfiguration<MatchParticipant>
{
    public void Configure(EntityTypeBuilder<MatchParticipant> entity)
    {
        entity.ToTable("MatchParticipants", "Infinite", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.MatchID, e.XboxUserID });

        entity.HasOne(d => d.MatchTeam)
            .WithMany(p => p.MatchParticipants)
            .HasForeignKey(d => new { d.MatchID, d.TeamID });

        entity.HasOne(d => d.XboxUser)
            .WithMany(p => p.MatchParticipants)
            .HasForeignKey(d => d.XboxUserID);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<MatchParticipant> entity);
}
