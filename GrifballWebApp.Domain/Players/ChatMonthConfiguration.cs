using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class ChatMonthConfiguration : IEntityTypeConfiguration<ChatMonth>
{
    /// <summary>A month's spend only grows from nothing (ChatMonth.Record refuses a negative reply).</summary>
    public const string NotNegative = "CostMicros >= 0 AND InputTokens >= 0 AND OutputTokens >= 0 AND Replies >= 0";

    public void Configure(EntityTypeBuilder<ChatMonth> entity)
    {
        // No row history: every paid reply changes the row (docs/database.md, "Row history")
        entity.ToTable("ChatMonths", "Auth", tb => tb.HasCheckConstraint("CK_Auth_ChatMonths_NotNegative", NotNegative));

        entity.HasKey(e => new { e.UserID, e.Month });

        // A reply's save adds to the total only while no other reply changed it since it was read (WHERE Version = ...)
        entity.Property(e => e.Version)
            .IsConcurrencyToken();

        entity.Ignore(e => e.Spent);
    }
}
