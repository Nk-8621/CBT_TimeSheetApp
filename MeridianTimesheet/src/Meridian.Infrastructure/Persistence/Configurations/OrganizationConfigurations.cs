using Meridian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meridian.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Carbynetech_Department");
        builder.HasKey(d => d.DepartmentId);
        builder.Property(d => d.Code).HasMaxLength(30).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(d => d.Code).IsUnique();

        builder.HasOne(d => d.Parent)
            .WithMany(d => d.Children)
            .HasForeignKey(d => d.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Carbynetech_Location");
        builder.HasKey(l => l.LocationId);
        builder.Property(l => l.Code).HasMaxLength(30).IsRequired();
        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(l => l.Code).IsUnique();
    }
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Carbynetech_Account");
        builder.HasKey(a => a.AccountId);
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.AccountType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(a => a.Name).IsUnique();

        builder.HasOne(a => a.Department)
            .WithMany()
            .HasForeignKey(a => a.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
