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

        // None: their roles' default
        entity.Property(e => e.Model)
            .IsRequired(false)
            .HasMaxLength(ChatChoice.ModelMaxLength);

        // Off for the rows before it: a change waits
        entity.Property(e => e.AutoApprove)
            .HasDefaultValue(false);
    }
}
