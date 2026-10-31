using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;

public class HaloInfiniteCredentialConfiguration : IEntityTypeConfiguration<HaloInfiniteCredential>
{
    public void Configure(EntityTypeBuilder<HaloInfiniteCredential> entity)
    {
        // Not temporal, unlike every other table: the token is refreshed every hour or so, and a history would only
        // keep old copies of a secret.
        entity.ToTable("HaloInfiniteCredentials", "Auth");

        entity.HasKey(e => e.Name);

        entity.Property(e => e.Name)
            .HasMaxLength(64);

        entity.Property(e => e.ProtectedValue)
            .IsRequired();
    }
}
