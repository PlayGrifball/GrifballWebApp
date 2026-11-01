using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class DuplicateDismissalConfiguration : IEntityTypeConfiguration<DuplicateDismissal>
{
    public void Configure(EntityTypeBuilder<DuplicateDismissal> entity)
    {
        // Not temporal: a log of sysadmins' decisions, each row written once
        entity.ToTable("DuplicateDismissals", "Auth", tb => tb.HasCheckConstraint("CK_Auth_DuplicateDismissals_Order", "LowUserID < HighUserID"));

        entity.HasKey(e => new { e.LowUserID, e.HighUserID });
    }
}
