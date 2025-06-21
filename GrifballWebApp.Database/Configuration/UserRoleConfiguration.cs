using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> entity)
    {
        entity.ToTable("UserRoles", "Auth", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.UserId, e.RoleId });

        entity.HasOne(d => d.User)
            .WithMany(p => p.UserRoles)
            .HasForeignKey(d => d.UserId);

        entity.HasOne(d => d.Role)
            .WithMany(p => p.UserRoles)
            .HasForeignKey(d => d.RoleId);
    }
}

public class UserLoginConfiguration : IEntityTypeConfiguration<UserLogin>
{
    public void Configure(EntityTypeBuilder<UserLogin> entity)
    {
        entity.ToTable("UserLogins", "Auth", tb => tb.IsTemporal());

        entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

        entity.HasOne(d => d.User)
            .WithMany(p => p.UserLogins)
            .HasForeignKey(d => d.UserId);
    }
}

public class UserClaimConfiguration : IEntityTypeConfiguration<UserClaim>
{
    public void Configure(EntityTypeBuilder<UserClaim> entity)
    {
        entity.ToTable("UserClaims", "Auth", tb => tb.IsTemporal());

        entity.HasOne(d => d.User)
            .WithMany(p => p.UserClaims)
            .HasForeignKey(d => d.UserId);
    }
}
