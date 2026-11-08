using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class DuplicateDismissalConfiguration : IEntityTypeConfiguration<DuplicateDismissal>
{
    /// <summary>A pair is stored lower user ID first (DuplicateDismissal.InOrder): so once only, and never a user with itself.</summary>
    public const string InOrder = nameof(DuplicateDismissal.LowUserID) + " < " + nameof(DuplicateDismissal.HighUserID);

    public void Configure(EntityTypeBuilder<DuplicateDismissal> entity)
    {
        // Not temporal: a log of sysadmins' decisions, each row written once
        entity.ToTable("DuplicateDismissals", "Auth", tb => tb.HasCheckConstraint("CK_Auth_DuplicateDismissals_Order", InOrder));

        // The pair is the key: dismissed once
        entity.HasKey(e => new { e.LowUserID, e.HighUserID });
    }
}
