﻿using GrifballWebApp.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GrifballWebApp.Database.Configuration;
public partial class XboxUserConfiguration : IEntityTypeConfiguration<XboxUser>
{
    public void Configure(EntityTypeBuilder<XboxUser> entity)
    {
        entity.ToTable("XboxUsers", "Xbox", tb => tb.IsTemporal());

        entity.HasKey(e => e.XboxUserID);

        // Value always comes from 343
        entity.Property(e => e.XboxUserID).ValueGeneratedNever();

        entity.HasOne(d => d.Person)
            .WithOne(p => p.XboxUser)
            .HasForeignKey<Person>(d => d.XboxUserID)
            .IsRequired(false);

        OnConfigurePartial(entity);
    }

    partial void OnConfigurePartial(EntityTypeBuilder<XboxUser> entity);
}
