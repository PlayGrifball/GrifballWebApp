using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class ChatChoiceConfiguration : IEntityTypeConfiguration<ChatChoice>
{
    public void Configure(EntityTypeBuilder<ChatChoice> entity)
    {
        entity.ToTable("ChatChoices", "Auth", tb => tb.IsTemporal());

        // One choice a user
        entity.HasKey(e => e.UserID);
        entity.Property(e => e.UserID).ValueGeneratedNever();

        entity.Property(e => e.Model)
            .IsRequired()
            .HasMaxLength(ChatChoice.ModelMaxLength);
    }
}
