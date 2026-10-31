using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        // Not temporal, unlike every other table: its rows are written a few times each and deleted after a week,
        // so their history would only grow. Not auditable either: the app writes them, not a user.
        builder.ToTable("Outbox", "Discord");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Kind).HasMaxLength(100);
        builder.Property(e => e.GroupKey).HasMaxLength(100);
        builder.Property(e => e.LastError).HasMaxLength(2000);

        // The dispatcher's claim: due rows by status and time.
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });

        // A group's order, and the claim's look for an earlier row of the group still to send.
        builder.HasIndex(e => new { e.GroupKey, e.Sequence }).IsUnique();
    }
}
