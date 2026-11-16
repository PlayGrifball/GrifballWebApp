using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class VoiceConsentConfiguration : IEntityTypeConfiguration<VoiceConsent>
{
    public void Configure(EntityTypeBuilder<VoiceConsent> entity)
    {
        // Temporal: when each account opted in and out
        entity.ToTable("VoiceConsents", "User", tb => tb.IsTemporal());

        // One a Discord account, by Discord's ID
        entity.HasKey(e => e.DiscordUserID);
        entity.Property(e => e.DiscordUserID).ValueGeneratedNever();
    }
}
