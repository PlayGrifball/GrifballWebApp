using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Domain.Players;

public class UserMergeConfiguration : IEntityTypeConfiguration<UserMerge>
{
    /// <summary>A user is never merged into itself (the merge refuses it).</summary>
    public const string DifferentUsers = "SourceUserID <> TargetUserID";

    /// <summary>Only an undone merge says who undid it and why.</summary>
    public const string UndoOnlyWhenUndone = "UndoneAt IS NOT NULL OR (UndoneByUserID IS NULL AND UndoNote IS NULL)";

    /// <summary>The manifest's counts.</summary>
    public const string RowCountsNotNegative = "RowsMoved >= 0 AND RowsRemoved >= 0 AND RowsAdded >= 0";

    public void Configure(EntityTypeBuilder<UserMerge> entity)
    {
        // Not temporal: it is itself the log of merges (and their undos), and the manifest can be large
        entity.ToTable("UserMerges", "Auth", tb =>
        {
            tb.HasCheckConstraint("CK_Auth_UserMerges_DifferentUsers", DifferentUsers);
            tb.HasCheckConstraint("CK_Auth_UserMerges_UndoOnlyWhenUndone", UndoOnlyWhenUndone);
            tb.HasCheckConstraint("CK_Auth_UserMerges_RowCountsNotNegative", RowCountsNotNegative);
        });

        entity.HasKey(e => e.UserMergeID);

        entity.Property(e => e.Reason).HasMaxLength(UserMerge.ReasonMaxLength);
        entity.Property(e => e.SourceUserName).HasMaxLength(UserMerge.UserNameMaxLength);
        entity.Property(e => e.TargetUserName).HasMaxLength(UserMerge.UserNameMaxLength);
        entity.Property(e => e.UndoNote).HasMaxLength(UserMerge.UndoNoteMaxLength);
        // nvarchar(max) / text
        entity.Property(e => e.Manifest).IsRequired();

        // Worked out from UndoneAt, not stored
        entity.Ignore(e => e.IsStanding);

        entity.HasIndex(e => e.MergedAt);
        entity.HasIndex(e => e.SourceUserID);
        entity.HasIndex(e => e.TargetUserID);
    }
}
